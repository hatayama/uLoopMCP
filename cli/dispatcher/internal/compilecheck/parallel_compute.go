package compilecheck

import (
	"sync"
	"sync/atomic"
)

// planReadConcurrency is how many reads planning runs at once.
// Why 8: reading a large project's directories with 8 readers took well under half the time of
// one, and 16 took no less than 8; a small CI runner is not crowded by 8 either.
const planReadConcurrency = 8

// computeInParallel calls compute for every index from 0 to count-1, up to planReadConcurrency at a
// time, and returns the results in index order once every call has finished.
// compute must not write to anything another call reads or writes; the caller then walks the
// results in order, so whatever decides between them stays as it was when they were computed one by
// one.
func computeInParallel[T any](count int, compute func(index int) T) []T {
	results := make([]T, count)
	var next atomic.Int64
	var workers sync.WaitGroup
	for range min(count, planReadConcurrency) {
		workers.Go(func() {
			for {
				index := int(next.Add(1) - 1)
				if index >= count {
					return
				}
				results[index] = compute(index)
			}
		})
	}
	workers.Wait()

	return results
}
