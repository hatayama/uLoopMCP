package compilecheck

import (
	"testing"
	"time"
)

// runConcurrentlyReturnWait is how long a test waits to see whether runConcurrently has returned.
const runConcurrentlyReturnWait = 100 * time.Millisecond

// startRunConcurrently runs the tasks in the background and reports on the returned channel when
// runConcurrently returns.
func startRunConcurrently(tasks ...func()) <-chan struct{} {
	returned := make(chan struct{})
	go func() {
		runConcurrently(tasks...)
		close(returned)
	}()

	return returned
}

// Verifies runConcurrently does not return while one task is still running, and returns once every
// task has finished.
func TestRunConcurrentlyWaitsForEveryTask(t *testing.T) {
	release := make(chan struct{})
	finished := make(chan struct{}, 2)
	returned := startRunConcurrently(
		func() { finished <- struct{}{} },
		func() {
			<-release
			finished <- struct{}{}
		},
	)

	select {
	case <-returned:
		t.Fatal("runConcurrently returned while a task was still running")
	case <-time.After(runConcurrentlyReturnWait):
	}

	close(release)
	select {
	case <-returned:
	case <-time.After(10 * time.Second):
		t.Fatal("runConcurrently did not return after every task finished")
	}
	if len(finished) != 2 {
		t.Fatalf("finished tasks = %d, want 2", len(finished))
	}
}

// Verifies runConcurrently runs its tasks at the same time rather than one after another: a task
// that waits for another to start would never finish if they ran in order.
func TestRunConcurrentlyRunsTasksAtTheSameTime(t *testing.T) {
	secondStarted := make(chan struct{})
	returned := startRunConcurrently(
		func() { <-secondStarted },
		func() { close(secondStarted) },
	)

	select {
	case <-returned:
	case <-time.After(10 * time.Second):
		t.Fatal("runConcurrently ran its tasks one after another")
	}
}

// Verifies runConcurrently with no tasks returns at once.
func TestRunConcurrentlyReturnsAtOnceWithNoTasks(t *testing.T) {
	select {
	case <-startRunConcurrently():
	case <-time.After(10 * time.Second):
		t.Fatal("runConcurrently with no tasks did not return")
	}
}
