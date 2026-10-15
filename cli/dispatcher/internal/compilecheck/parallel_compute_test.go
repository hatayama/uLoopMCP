package compilecheck

import (
	"reflect"
	"sync"
	"testing"
	"time"
)

// Verifies each result lands at the index it was computed for, whatever order the computations
// finish in.
func TestComputeInParallelPlacesEachResultAtItsIndex(t *testing.T) {
	const count = 50
	results := computeInParallel(count, func(index int) int {
		// Why the early indexes wait longer: they then finish after later ones, which a helper that
		// appended results as they arrived would put out of place.
		time.Sleep(time.Duration(count-index) * 100 * time.Microsecond)

		return index * 10
	})

	want := make([]int, count)
	for index := range want {
		want[index] = index * 10
	}
	if !reflect.DeepEqual(results, want) {
		t.Fatalf("results = %v, want %v", results, want)
	}
}

// Verifies no computation runs for a count of zero and the result is empty.
func TestComputeInParallelReturnsNothingForZero(t *testing.T) {
	called := false
	results := computeInParallel(0, func(int) int {
		called = true

		return 0
	})

	if called || len(results) != 0 {
		t.Fatalf("results = %v, called = %v, want no results and no call", results, called)
	}
}

// Verifies the helper runs computations side by side but never more than its limit at once.
func TestComputeInParallelKeepsWithinItsLimit(t *testing.T) {
	var mu sync.Mutex
	running := 0
	most := 0
	computeInParallel(planReadConcurrency*4, func(int) struct{} {
		mu.Lock()
		running++
		most = max(most, running)
		mu.Unlock()
		time.Sleep(5 * time.Millisecond)
		mu.Lock()
		running--
		mu.Unlock()

		return struct{}{}
	})

	if most > planReadConcurrency || most < 2 {
		t.Fatalf("at most %d computations ran at once, want between 2 and %d", most, planReadConcurrency)
	}
}
