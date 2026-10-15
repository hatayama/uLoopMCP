package compilecheck

import (
	"fmt"
	"strings"
	"time"
)

// unitProgress is where each unit of a run stands on its way to a final state.
type unitProgress struct {
	// queued marks a unit waiting in ready, started one whose compile has been launched at least
	// once, finished one whose latest compile is back but not yet decided on, and final one whose
	// final state is decided.
	queued, started, finished, final []bool
	// assumed lists, per unit, the references that had no final state when its latest compile
	// started, and that it read from Unity's build on the assumption that they keep its surface.
	assumed [][]int
	// stale marks a unit whose latest compile assumed a reference that then changed its surface,
	// and staleCause names the first such reference, or -1.
	stale      []bool
	staleCause []int
	// deferred marks a unit that did not start early because its record might still be replayed.
	deferred []bool
	// pendingKept holds the surface verdict of a unit's latest compile until its final state is
	// decided, and discarded how long the compile it threw away took.
	pendingKept []bool
	discarded   []time.Duration
}

// newUnitProgress prepares the progress of count units, none of which has started.
func newUnitProgress(count int) unitProgress {
	progress := unitProgress{
		queued:      make([]bool, count),
		started:     make([]bool, count),
		finished:    make([]bool, count),
		final:       make([]bool, count),
		assumed:     make([][]int, count),
		stale:       make([]bool, count),
		staleCause:  make([]int, count),
		deferred:    make([]bool, count),
		pendingKept: make([]bool, count),
		discarded:   make([]time.Duration, count),
	}
	for index := range progress.staleCause {
		progress.staleCause[index] = -1
	}

	return progress
}

// enqueue puts a unit at the end of the ready queue.
func (state *schedule) enqueue(index int) {
	state.queued[index] = true
	state.ready = append(state.ready, index)
}

// takeNext removes the next unit to start from the ready queue: a unit whose references are all final
// before one queued early, then the one with the most sources, and among those the one first in the
// plan.
// Why a unit with final references goes first: a unit queued early may reference one still waiting
// in the queue, and starting it ahead of that reference would compile it against Unity's file, only
// to compile it again once the reference changed its surface.
// Why the most sources first: how long a compile takes follows its source count closely, and
// starting the longest compiles first keeps them from finishing last while the other jobs sit idle.
// Why plan order breaks a tie rather than the order units became ready: it keeps the start order the
// same from run to run.
func (state *schedule) takeNext() int {
	best := 0
	for position := 1; position < len(state.ready); position++ {
		if state.startsBefore(state.ready[position], state.ready[best]) {
			best = position
		}
	}
	index := state.ready[best]
	state.ready = append(state.ready[:best], state.ready[best+1:]...)
	state.queued[index] = false

	return index
}

// startsBefore reports whether unit a should start before unit b when both are ready.
func (state *schedule) startsBefore(a int, b int) bool {
	if (state.pending[a] == 0) != (state.pending[b] == 0) {
		return state.pending[a] == 0
	}
	if state.sourceCounts[a] != state.sourceCounts[b] {
		return state.sourceCounts[a] > state.sourceCounts[b]
	}

	return a < b
}

// mayStartEarly reports whether a unit with references still lacking a final state may start before
// them: it has to compile whatever they turn out to be, every reference still missing is a dependent
// rather than one selected for its own sources, and nothing has ruled it out.
// Why a reference selected for its own sources is waited for: its sources changed, so it is likely
// to change its surface, and starting before it would mostly compile against the wrong file.
// Why a dependent nothing changed under waits too: it may still be skipped once the rest finish.
func (state *schedule) mayStartEarly(index int) bool {
	mustCompile := state.ownSources[index] || state.changedInputs[index] > 0

	return mustCompile && state.pendingOwn[index] == 0 && !state.blocked[index] && !state.deferred[index]
}

// offer queues a unit that has not started when it may start now: when every reference has a final
// state, or when it may start early and its record could not be replayed by waiting.
// Why the record is asked about before starting early: a compile overwrites the unit's record, so
// starting early would throw away a record the same inputs replay once the references finish, and
// every later run over those inputs would compile the unit twice again. Once the record says no it
// says no for good - fewer unknown references only make it stricter - so the answer is not asked again.
func (run *schedulerRun) offer(index int) {
	state := run.state
	if state.started[index] || state.queued[index] || state.final[index] {
		return
	}
	if state.pending[index] == 0 {
		state.enqueue(index)

		return
	}
	if !state.mayStartEarly(index) {
		return
	}
	unit, unknown := run.startingUnit(index)
	if run.compiler.recordMayStillApply(run.plan, unit, run.namedUnits(unknown)) {
		state.deferred[index] = true

		return
	}
	state.enqueue(index)
}

// releaseAndOffer tells the units waiting on one that just got its final state, and queues those that
// may start now.
func (run *schedulerRun) releaseAndOffer(index int, changed bool) {
	for _, dependent := range run.state.release(index, changed) {
		run.offer(dependent)
	}
}

// startAndSettle decides the final state of what finished and starts what is ready, until neither
// moves anything.
// Why the two alternate: a skip decided while starting units gives a reference its final state,
// which can let a unit that already finished be decided, and deciding one can make others ready.
// Why it decides before it starts: the units a finished one releases then compete for the free jobs
// with the ones already waiting, instead of queuing behind whatever started first.
func (run *schedulerRun) startAndSettle(jobs int) error {
	if _, err := run.settleFinished(); err != nil {
		return err
	}
	for {
		if err := run.startReadyUnits(jobs); err != nil {
			return err
		}
		settled, err := run.settleFinished()
		if err != nil {
			return err
		}
		if !settled {
			return nil
		}
	}
}

// settleFinished decides the final state of every finished unit whose references all have one, until
// none is left, and reports whether it decided anything.
func (run *schedulerRun) settleFinished() (bool, error) {
	state := run.state
	progressed := false
	for settled := true; settled; {
		settled = false
		for index := range run.plan.Units {
			if !state.finished[index] || state.final[index] || state.pending[index] != 0 {
				continue
			}
			settled = true
			progressed = true
			if err := run.settle(index); err != nil {
				return progressed, err
			}
		}
	}

	return progressed, nil
}

// settle decides what a finished unit's latest compile counts for, now that every reference it has
// in the plan has a final state: nothing when one it assumed failed or was blocked, a compile again
// when one it assumed changed its surface, and its result otherwise.
// Why a compile again rather than a correction: the reference assembly it read is the wrong one, and
// what csc reports against the right one cannot be derived from what it reported against Unity's.
func (run *schedulerRun) settle(index int) error {
	state := run.state
	switch {
	case state.blocked[index]:
		state.finished[index] = false
		if err := run.markFinal(index); err != nil {
			return err
		}
		state.blockDependents(index)
		run.releaseAndOffer(index, false)
	case state.stale[index]:
		state.discarded[index] = run.results[index].Duration
		run.results[index] = UnitResult{}
		state.finished[index] = false
		state.stale[index] = false
		state.assumed[index] = nil
		state.enqueue(index)
	default:
		return run.adopt(index)
	}

	return nil
}

// adopt makes a finished unit's latest compile its final state and tells the units below it.
// Why a replayed result blocks the same way a compiled one does: it says the assembly has errors,
// and whether this run learned that from csc or from what the last run recorded changes nothing about
// the reference assembly the units below it would have to read.
func (run *schedulerRun) adopt(index int) error {
	state := run.state
	result := run.results[index]
	result.AssumedReferences = run.namesOf(state.assumed[index])
	result.WaitedForRecord = state.deferred[index]
	if cause := state.staleCause[index]; cause >= 0 {
		result.CompiledAgainAfter = run.plan.Units[cause].Assembly.AssemblyName
		result.DiscardedDuration = state.discarded[index]
	}
	run.results[index] = result
	if err := run.markFinal(index); err != nil {
		return err
	}
	run.compiled[index] = true
	state.keptSurface[index] = state.pendingKept[index]
	if !result.Succeeded {
		state.blockDependents(index)
	}
	run.releaseAndOffer(index, !state.keptSurface[index])

	return nil
}

// markFinal gives a unit its final state, which every unit may get only once every reference it has
// in the plan has one.
// Why a breach is an error rather than ignored: a unit decided early tells the units below it what
// it turned out as before that is settled, and the run then reports results nobody can trust. As an
// error it stops the run the way a plan it cannot finish does, after the running compiles return.
func (run *schedulerRun) markFinal(index int) error {
	if run.state.pending[index] != 0 {
		return fmt.Errorf("compile-check finished scheduling %s before every assembly it references finished",
			run.plan.Units[index].Assembly.AssemblyName)
	}
	run.state.final[index] = true

	return nil
}

// unfinishedError reports the units a run that ran out of work never decided on, or nil when there
// are none.
// Why it is an error: only references that wait on each other leave units behind, and returning
// without them would report a clean run that never compiled them.
func (run *schedulerRun) unfinishedError() error {
	left := []string{}
	for index, final := range run.state.final {
		if !final {
			left = append(left, run.plan.Units[index].Assembly.AssemblyName)
		}
	}
	if len(left) == 0 {
		return nil
	}

	return fmt.Errorf("compile-check could not finish scheduling %d assemblies: %s",
		len(left), strings.Join(left, ", "))
}

// namesOf names the given units of the plan, in the order given; nil for none.
func (run *schedulerRun) namesOf(indices []int) []string {
	if len(indices) == 0 {
		return nil
	}
	names := make([]string, 0, len(indices))
	for _, index := range indices {
		names = append(names, run.plan.Units[index].Assembly.AssemblyName)
	}

	return names
}

// namedUnits is the set of the names of the given units of the plan.
func (run *schedulerRun) namedUnits(indices []int) map[string]bool {
	names := make(map[string]bool, len(indices))
	for _, index := range indices {
		names[run.plan.Units[index].Assembly.AssemblyName] = true
	}

	return names
}

// containsIndex reports whether indices holds index.
func containsIndex(indices []int, index int) bool {
	for _, candidate := range indices {
		if candidate == index {
			return true
		}
	}

	return false
}
