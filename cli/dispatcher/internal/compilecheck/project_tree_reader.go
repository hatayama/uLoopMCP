package compilecheck

import (
	"os"
	"path/filepath"
	"sync"
)

// projectTreeReadConcurrency is how many directories the walk reads at once.
// Why 8: reading a large project's directories with 8 readers took well under half the time of
// one, and 16 took no less than 8; a small CI runner is not crowded by 8 either.
const projectTreeReadConcurrency = 8

// projectTreeListing is one directory as read from the disk, or the error reading it gave.
type projectTreeListing struct {
	directory projectTreeDirectory
	err       error
}

// readProjectTreeListing reads one directory and lists it without the names Unity ignores.
// Why the entries of a failed read are dropped: os.ReadDir can return part of a directory together
// with its error, and the walk stops at that error without looking at any of them.
func readProjectTreeListing(path string) projectTreeListing {
	entries, err := os.ReadDir(path)
	if err != nil {
		return projectTreeListing{err: err}
	}

	directory := projectTreeDirectory{byName: map[string]projectTreeEntry{}}
	for _, entry := range entries {
		if isUnityIgnoredName(entry.Name()) {
			continue
		}
		listed := projectTreeEntry{
			name:        entry.Name(),
			isDirectory: entry.IsDir(),
			isRegular:   entry.Type().IsRegular(),
		}
		directory.entries = append(directory.entries, listed)
		directory.byName[listed.name] = listed
	}

	return projectTreeListing{directory: directory}
}

// readProjectTreeListings reads the given directories and every directory below them that the walk
// would descend into, several at a time, and returns once every read has finished. The result is
// keyed by the path each directory was read under. A directory two roots share is read once.
func readProjectTreeListings(rootPaths []string) map[string]projectTreeListing {
	reader := &projectTreeReader{
		listings: map[string]projectTreeListing{},
		queued:   map[string]bool{},
	}
	reader.wake = sync.NewCond(&reader.mu)
	for _, rootPath := range rootPaths {
		reader.enqueue(rootPath)
	}

	var workers sync.WaitGroup
	for range projectTreeReadConcurrency {
		workers.Go(reader.work)
	}
	workers.Wait()

	return reader.listings
}

// projectTreeReader is the shared queue of directories the read-ahead still has to read.
type projectTreeReader struct {
	mu       sync.Mutex
	wake     *sync.Cond
	queue    []string
	pending  int // directories queued or being read
	listings map[string]projectTreeListing
	queued   map[string]bool
}

// enqueue adds a directory unless it was queued before. The caller holds mu, or no worker runs yet.
func (reader *projectTreeReader) enqueue(path string) {
	if reader.queued[path] {
		return
	}
	reader.queued[path] = true
	reader.queue = append(reader.queue, path)
	reader.pending++
}

// work reads queued directories until none is queued or being read.
func (reader *projectTreeReader) work() {
	for {
		path, found := reader.next()
		if !found {
			return
		}
		reader.finish(path, readProjectTreeListing(path))
	}
}

// next takes a queued directory, waiting while other workers may still queue more.
func (reader *projectTreeReader) next() (string, bool) {
	reader.mu.Lock()
	defer reader.mu.Unlock()
	for len(reader.queue) == 0 && reader.pending > 0 {
		reader.wake.Wait()
	}
	if len(reader.queue) == 0 {
		return "", false
	}
	path := reader.queue[0]
	reader.queue = reader.queue[1:]

	return path, true
}

// finish stores one listing and queues the directories below it that the walk would descend into.
// Why nothing below a failed read is queued: the walk stops there, so none of it is needed.
func (reader *projectTreeReader) finish(path string, listing projectTreeListing) {
	reader.mu.Lock()
	defer reader.mu.Unlock()
	reader.listings[path] = listing
	if listing.err == nil {
		for _, entry := range listing.directory.entries {
			if entry.isDirectory {
				reader.enqueue(filepath.Join(path, entry.name))
			}
		}
	}
	reader.pending--
	reader.wake.Broadcast()
}
