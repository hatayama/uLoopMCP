package dispatcher

import (
	"fmt"
	"reflect"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck"
)

func surfaceCheckTestNames(count int) []string {
	names := make([]string, 0, count)
	for index := 0; index < count; index++ {
		names = append(names, fmt.Sprintf("Name%02d", index))
	}

	return names
}

func surfaceCheckFor(t *testing.T, checks []compileCheckSurfaceCheck, assembly string) compileCheckSurfaceCheck {
	t.Helper()
	for _, check := range checks {
		if check.Assembly == assembly {
			return check
		}
	}
	t.Fatalf("expected a surface check for %s, got %+v", assembly, checks)

	return compileCheckSurfaceCheck{}
}

// Verifies that a unit whose surface was not compared (it failed, or nothing references it) gets
// no surface check.
func TestCompileCheckSurfaceChecksLeaveOutUnitsNotCompared(t *testing.T) {
	checks := compileCheckSurfaceChecks(compilecheck.Result{Units: []compilecheck.UnitResult{
		{Assembly: "Failed"},
		{Assembly: "Leaf", Succeeded: true},
	}})

	if len(checks) != 0 {
		t.Fatalf("expected no surface check, got %+v", checks)
	}
}

// Verifies that a fast-checked unit's verdict is reported in the same shape as a compiled one's.
func TestCompileCheckSurfaceChecksIncludeFastCheckedUnits(t *testing.T) {
	checks := compileCheckSurfaceChecks(compilecheck.Result{Units: []compilecheck.UnitResult{{
		Assembly: "Core", Succeeded: true, FastChecked: true,
		Surface: compilecheck.SurfaceVerdict{Kept: true, Reason: compilecheck.SurfaceSameAPI},
	}}})

	check := surfaceCheckFor(t, checks, "Core")
	if !check.Kept || check.Verdict != compilecheck.SurfaceSameAPI {
		t.Fatalf("expected a kept same API check for the fast-checked unit, got %+v", check)
	}
}

// Verifies that a kept surface reports its verdict with empty lists and nothing compiled for it.
func TestCompileCheckSurfaceChecksReportAKeptSurface(t *testing.T) {
	checks := compileCheckSurfaceChecks(compilecheck.Result{Units: []compilecheck.UnitResult{{
		Assembly: "Core", Succeeded: true,
		Surface: compilecheck.SurfaceVerdict{Kept: true, Reason: compilecheck.SurfaceSameBytes},
	}}})

	want := compileCheckSurfaceCheck{
		Assembly: "Core", Kept: true, Verdict: compilecheck.SurfaceSameBytes,
		ChangedNames: []string{}, Dependents: []compileCheckDependentCompile{},
	}
	if got := surfaceCheckFor(t, checks, "Core"); !reflect.DeepEqual(got, want) {
		t.Fatalf("expected %+v, got %+v", want, got)
	}
}

// Verifies that a named change lists its names, the dependents compiled with the name that selected
// each, and how many dependents were left out; one dependent is reused, which reports the same way.
func TestCompileCheckSurfaceChecksListDependentsSelectedByName(t *testing.T) {
	checks := compileCheckSurfaceChecks(compilecheck.Result{
		Units: []compilecheck.UnitResult{
			{
				Assembly: "Core", Succeeded: true,
				Surface: compilecheck.SurfaceVerdict{Reason: compilecheck.SurfaceChanged, Asked: true, ChangedNames: []string{"Player", "Score"}},
			},
			{Assembly: "Game", Succeeded: true, ChangedReferences: []compilecheck.ChangedReference{{Assembly: "Core", Name: "Player"}}},
			{Assembly: "Ui", Succeeded: true, Reused: true, ChangedReferences: []compilecheck.ChangedReference{{Assembly: "Core", Name: "Score"}}},
		},
		NameSkips: []compilecheck.NameSkip{
			{Assembly: "Audio", ChangedReferences: []string{"Core"}},
			{Assembly: "Input", ChangedReferences: []string{"Core"}},
			{Assembly: "Net", ChangedReferences: []string{"Core"}},
		},
	})

	want := compileCheckSurfaceCheck{
		Assembly: "Core", Verdict: compilecheck.SurfaceChanged,
		ChangedNames: []string{"Player", "Score"}, ChangedNameCount: 2,
		Dependents: []compileCheckDependentCompile{
			{Assembly: "Game", Name: "Player"},
			{Assembly: "Ui", Name: "Score"},
		},
		UnaffectedDependents: 3,
	}
	if got := surfaceCheckFor(t, checks, "Core"); !reflect.DeepEqual(got, want) {
		t.Fatalf("expected %+v, got %+v", want, got)
	}
}

// Verifies that a dependent compiled because the names could not be applied carries that reason.
func TestCompileCheckSurfaceChecksKeepTheReasonNamesWereNotApplied(t *testing.T) {
	checks := compileCheckSurfaceChecks(compilecheck.Result{Units: []compilecheck.UnitResult{
		{
			Assembly: "Core", Succeeded: true,
			Surface: compilecheck.SurfaceVerdict{Reason: compilecheck.SurfaceChanged, ChangedNames: []string{"Player"}},
		},
		{Assembly: "Game", Succeeded: true, ChangedReferences: []compilecheck.ChangedReference{{Assembly: "Core", Reason: "no saved generator output"}}},
	}})

	want := []compileCheckDependentCompile{{Assembly: "Game", Reason: "no saved generator output"}}
	if got := surfaceCheckFor(t, checks, "Core").Dependents; !reflect.DeepEqual(got, want) {
		t.Fatalf("expected %+v, got %+v", want, got)
	}
}

// Verifies that an unnamed change reports the worker's reason and every dependent compiled for it.
func TestCompileCheckSurfaceChecksListEveryDependentOfAnUnnamedChange(t *testing.T) {
	units := []compilecheck.UnitResult{{
		Assembly: "Core", Succeeded: true,
		Surface: compilecheck.SurfaceVerdict{Reason: compilecheck.SurfaceChanged, Asked: true, Unnamed: "Type of Core.Player"},
	}}
	want := make([]compileCheckDependentCompile, 0, 19)
	for index := 0; index < 19; index++ {
		assembly := fmt.Sprintf("Dependent%02d", index)
		units = append(units, compilecheck.UnitResult{
			Assembly: assembly, Succeeded: true, ChangedReferences: []compilecheck.ChangedReference{{Assembly: "Core"}},
		})
		want = append(want, compileCheckDependentCompile{Assembly: assembly})
	}

	check := surfaceCheckFor(t, compileCheckSurfaceChecks(compilecheck.Result{Units: units}), "Core")
	if check.Unnamed != "Type of Core.Player" || len(check.ChangedNames) != 0 || check.ChangedNameCount != 0 {
		t.Fatalf("expected the unnamed reason and no names, got %+v", check)
	}
	if !reflect.DeepEqual(check.Dependents, want) {
		t.Fatalf("expected all 19 dependents, got %+v", check.Dependents)
	}
}

// Verifies that a verdict reached without asking the worker (bytes differ) is still reported, since
// it is what makes every dependent compile.
func TestCompileCheckSurfaceChecksIncludeVerdictsTheWorkerDidNotGive(t *testing.T) {
	checks := compileCheckSurfaceChecks(compilecheck.Result{Units: []compilecheck.UnitResult{
		{
			Assembly: "Core", Succeeded: true,
			Surface: compilecheck.SurfaceVerdict{Reason: compilecheck.SurfaceBytesDiffer, Unnamed: compilecheck.SurfaceBytesDiffer},
		},
		{Assembly: "Game", Succeeded: true, ChangedReferences: []compilecheck.ChangedReference{{Assembly: "Core"}}},
	}})

	check := surfaceCheckFor(t, checks, "Core")
	if check.Unnamed != check.Verdict || len(check.Dependents) != 1 {
		t.Fatalf("expected the bytes differ verdict with its dependent, got %+v", check)
	}
}

// Verifies that only the first 20 changed names are listed while the count says how many there are.
func TestCompileCheckSurfaceChecksCapTheChangedNames(t *testing.T) {
	names := surfaceCheckTestNames(25)
	checks := compileCheckSurfaceChecks(compilecheck.Result{Units: []compilecheck.UnitResult{
		{
			Assembly: "Core", Succeeded: true,
			Surface: compilecheck.SurfaceVerdict{Reason: compilecheck.SurfaceChanged, ChangedNames: names},
		},
		{Assembly: "Game", Succeeded: true, ChangedReferences: []compilecheck.ChangedReference{{Assembly: "Core", Name: "Name00"}}},
	}})

	check := surfaceCheckFor(t, checks, "Core")
	if !reflect.DeepEqual(check.ChangedNames, names[:20]) || check.ChangedNameCount != 25 {
		t.Fatalf("expected the first 20 of 25 names, got %d names and count %d", len(check.ChangedNames), check.ChangedNameCount)
	}
}

// Verifies that a dependent whose response file lists the same project reference twice, and so is
// recorded twice under that reference, is listed and counted once.
func TestCompileCheckSurfaceChecksCountADependentOncePerReference(t *testing.T) {
	checks := compileCheckSurfaceChecks(compilecheck.Result{
		Units: []compilecheck.UnitResult{
			{
				Assembly: "Core", Succeeded: true,
				Surface: compilecheck.SurfaceVerdict{Reason: compilecheck.SurfaceChanged, ChangedNames: []string{"Player"}},
			},
			{Assembly: "Game", Succeeded: true, ChangedReferences: []compilecheck.ChangedReference{
				{Assembly: "Core", Name: "Player"}, {Assembly: "Core", Name: "Player"},
			}},
		},
		NameSkips: []compilecheck.NameSkip{{Assembly: "Audio", ChangedReferences: []string{"Core", "Core"}}},
	})

	check := surfaceCheckFor(t, checks, "Core")
	if len(check.Dependents) != 1 || check.UnaffectedDependents != 1 {
		t.Fatalf("expected one dependent and one unaffected, got %+v", check)
	}
}
