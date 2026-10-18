package dispatcher

import "github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck"

// reportedChangedNames caps the changed names one surface check lists, since a namespace change can
// touch thousands of them; ChangedNameCount still says how many there are.
const reportedChangedNames = 20

// compileCheckSurfaceCheck says what compiling (or replaying) an assembly that others reference
// did to its public surface, and which dependents this run compiled because of that.
type compileCheckSurfaceCheck struct {
	Assembly string `json:"Assembly"`
	// Kept: every dependent compiles against the same API as under the last Unity build.
	Kept bool `json:"Kept"`
	// Verdict: how that was decided - one of the Surface* values of the compilecheck package
	// ("same bytes", "same API", "changed", "bytes differ", "could not compare: ...", "could not read: ...").
	Verdict string `json:"Verdict"`
	// ChangedNames: the first identifiers a dependent has to write to be affected (at most
	// reportedChangedNames); ChangedNameCount is how many there are in all. Empty when kept or unnamed.
	ChangedNames     []string `json:"ChangedNames"`
	ChangedNameCount int      `json:"ChangedNameCount"`
	// Unnamed: why no name tells the change, so every dependent is affected. Empty when kept or named.
	Unnamed string `json:"Unnamed"`
	// Dependents: the assemblies this run compiled that this change counted for, in the run's order.
	Dependents []compileCheckDependentCompile `json:"Dependents"`
	// UnaffectedDependents: how many dependents were left out because they write none of the names.
	UnaffectedDependents int `json:"UnaffectedDependents"`
}

// compileCheckDependentCompile is one dependent compiled for a reference's change: the Name that
// selected it when the change was narrowed by names, or the Reason the names were not applied.
type compileCheckDependentCompile struct {
	Assembly string `json:"Assembly"`
	Name     string `json:"Name"`
	Reason   string `json:"Reason"`
}

// compileCheckSurfaceChecks lists one surface check per assembly whose public surface this run
// compared, with the dependents compiled and left out because of it. The list is never nil.
func compileCheckSurfaceChecks(result compilecheck.Result) []compileCheckSurfaceCheck {
	checks := make([]compileCheckSurfaceCheck, 0)
	indexByAssembly := map[string]int{}
	for _, unit := range result.Units {
		// Why an empty verdict is left out: the surface was not compared, because the unit failed or
		// nothing in the plan references it. Asked is not the filter, since a verdict reached without
		// the worker (bytes differ, could not compare or read) is what makes every dependent compile.
		if unit.Surface.Reason == "" {
			continue
		}
		indexByAssembly[unit.Assembly] = len(checks)
		checks = append(checks, newCompileCheckSurfaceCheck(unit))
	}
	addCompileCheckDependentCompiles(checks, indexByAssembly, result.Units)
	addCompileCheckUnaffectedDependents(checks, indexByAssembly, result.NameSkips)

	return checks
}

func newCompileCheckSurfaceCheck(unit compilecheck.UnitResult) compileCheckSurfaceCheck {
	names := unit.Surface.ChangedNames
	if len(names) > reportedChangedNames {
		names = names[:reportedChangedNames]
	}

	return compileCheckSurfaceCheck{
		Assembly: unit.Assembly,
		Kept:     unit.Surface.Kept,
		Verdict:  unit.Surface.Reason,
		// Why the names are copied: a kept or unnamed verdict carries none and must still
		// serialize as an empty list.
		ChangedNames:     append(make([]string, 0, len(names)), names...),
		ChangedNameCount: len(unit.Surface.ChangedNames),
		Unnamed:          unit.Surface.Unnamed,
		Dependents:       make([]compileCheckDependentCompile, 0),
	}
}

// addCompileCheckDependentCompiles files each dependent under the references whose change made it
// compile. Why one entry per (dependent, reference): a response file that lists the same project
// reference twice makes the scheduler record that reference twice for the dependent.
func addCompileCheckDependentCompiles(checks []compileCheckSurfaceCheck, indexByAssembly map[string]int, units []compilecheck.UnitResult) {
	seen := map[[2]string]bool{}
	for _, unit := range units {
		for _, reference := range unit.ChangedReferences {
			index, found := indexByAssembly[reference.Assembly]
			pair := [2]string{unit.Assembly, reference.Assembly}
			// Why a reference without a verdict is skipped rather than failing the run: a changed
			// reference always has one by design, and dropping the entry keeps the response consistent.
			if !found || seen[pair] {
				continue
			}
			seen[pair] = true
			checks[index].Dependents = append(checks[index].Dependents, compileCheckDependentCompile{
				Assembly: unit.Assembly, Name: reference.Name, Reason: reference.Reason,
			})
		}
	}
}

// addCompileCheckUnaffectedDependents counts the dependents left out under each reference, once per
// (dependent, reference) for the same reason as addCompileCheckDependentCompiles.
func addCompileCheckUnaffectedDependents(checks []compileCheckSurfaceCheck, indexByAssembly map[string]int, skips []compilecheck.NameSkip) {
	seen := map[[2]string]bool{}
	for _, skip := range skips {
		for _, reference := range skip.ChangedReferences {
			index, found := indexByAssembly[reference]
			pair := [2]string{skip.Assembly, reference}
			if !found || seen[pair] {
				continue
			}
			seen[pair] = true
			checks[index].UnaffectedDependents++
		}
	}
}
