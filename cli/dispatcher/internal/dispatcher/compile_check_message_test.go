package dispatcher

import (
	"encoding/json"
	"reflect"
	"strconv"
	"strings"
	"testing"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck"
)

const compileCheckFastCheckedReminder = "run with --full before finishing to cover assembly-wide rules."

// compileCheckMessageNames returns count distinct assembly names, since the summary counts names.
func compileCheckMessageNames(count int) []string {
	names := []string{}
	for index := range count {
		names = append(names, "Assembly"+strconv.Itoa(index))
	}

	return names
}

// Verifies that every count the summary puts before a noun uses the singular for exactly one and
// the plural for zero and for more than one.
func TestCompileCheckMessageUsesTheSingularOnlyForACountOfOne(t *testing.T) {
	cases := []struct {
		name     string
		response compileCheckResponse
		want     string
	}{
		{
			name:     "no compiled assembly leaves the first noun to the next count",
			response: compileCheckResponse{ReusedAssemblies: compileCheckMessageNames(2)},
			want:     "Reused 2 assemblies with 0 errors and 0 warnings.",
		},
		{
			name:     "one compiled assembly",
			response: compileCheckResponse{CompiledAssemblies: compileCheckMessageNames(1)},
			want:     "Compiled 1 assembly with 0 errors and 0 warnings.",
		},
		{
			name:     "two compiled assemblies",
			response: compileCheckResponse{CompiledAssemblies: compileCheckMessageNames(2)},
			want:     "Compiled 2 assemblies with 0 errors and 0 warnings.",
		},
		{
			name:     "one error",
			response: compileCheckResponse{CompiledAssemblies: compileCheckMessageNames(2), ErrorCount: 1},
			want:     "Compiled 2 assemblies with 1 error and 0 warnings.",
		},
		{
			name:     "two errors",
			response: compileCheckResponse{CompiledAssemblies: compileCheckMessageNames(2), ErrorCount: 2},
			want:     "Compiled 2 assemblies with 2 errors and 0 warnings.",
		},
		{
			name:     "one warning",
			response: compileCheckResponse{CompiledAssemblies: compileCheckMessageNames(2), WarningCount: 1},
			want:     "Compiled 2 assemblies with 0 errors and 1 warning.",
		},
		{
			name:     "two warnings",
			response: compileCheckResponse{CompiledAssemblies: compileCheckMessageNames(2), WarningCount: 2},
			want:     "Compiled 2 assemblies with 0 errors and 2 warnings.",
		},
		{
			name: "one blocked assembly",
			response: compileCheckResponse{
				CompiledAssemblies: compileCheckMessageNames(2),
				BlockedAssemblies:  compileCheckMessageNames(1),
			},
			want: "Compiled 2 assemblies with 0 errors and 0 warnings." +
				" 1 assembly was not compiled because an assembly it references has errors.",
		},
		{
			name: "two blocked assemblies",
			response: compileCheckResponse{
				CompiledAssemblies: compileCheckMessageNames(2),
				BlockedAssemblies:  compileCheckMessageNames(2),
			},
			want: "Compiled 2 assemblies with 0 errors and 0 warnings." +
				" 2 assemblies were not compiled because an assembly they reference has errors.",
		},
		{
			name: "one fast-checked assembly",
			response: compileCheckResponse{
				CompiledAssemblies:    compileCheckMessageNames(2),
				FastCheckedAssemblies: compileCheckMessageNames(1),
			},
			want: "Compiled 2 assemblies and fast-checked 1 with 0 errors and 0 warnings." +
				" 1 assembly was checked on its changed files only; " + compileCheckFastCheckedReminder,
		},
		{
			name: "two fast-checked assemblies",
			response: compileCheckResponse{
				CompiledAssemblies:    compileCheckMessageNames(2),
				FastCheckedAssemblies: compileCheckMessageNames(2),
			},
			want: "Compiled 2 assemblies and fast-checked 2 with 0 errors and 0 warnings." +
				" 2 assemblies were checked on their changed files only; " + compileCheckFastCheckedReminder,
		},
	}
	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			if got := compileCheckMessage(testCase.response); got != testCase.want {
				t.Fatalf("unexpected summary\n got: %q\nwant: %q", got, testCase.want)
			}
		})
	}
}

// assertCompileCheckNextActions fails the test unless response names exactly want, in order.
func assertCompileCheckNextActions(t *testing.T, response compileCheckResponse, want []string) {
	t.Helper()
	if got := compileCheckNextActions(response); !reflect.DeepEqual(got, want) {
		t.Fatalf("unexpected next actions\n got: %q\nwant: %q", got, want)
	}
}

// Verifies that a run with errors asks only for the errors to be fixed and the check rerun.
func TestCompileCheckNextActionsTellsToFixErrorsFirst(t *testing.T) {
	assertCompileCheckNextActions(t, compileCheckResponse{
		ErrorCount:         1,
		CompiledAssemblies: compileCheckMessageNames(1),
	}, []string{compileCheckFixErrorsAction})
}

// Verifies that a run with errors leaves out the --full reminder even when it fast-checked an
// assembly, because the run after the fix gives that reminder again.
func TestCompileCheckNextActionsLeavesOutFullWhileErrorsRemain(t *testing.T) {
	assertCompileCheckNextActions(t, compileCheckResponse{
		ErrorCount:            1,
		FastCheckedAssemblies: compileCheckMessageNames(1),
	}, []string{compileCheckFixErrorsAction})
}

// Verifies that a clean run that only fast-checked an assembly, as a body edit of a large leaf
// assembly does, asks for --full first and then for uloop compile, in that order.
func TestCompileCheckNextActionsAsksForFullAfterAFastCheck(t *testing.T) {
	assertCompileCheckNextActions(t, compileCheckResponse{
		FastCheckedAssemblies: compileCheckMessageNames(1),
	}, []string{compileCheckFullRunAction, compileCheckUnityCompileAction})
}

// Verifies that a clean run that only reused a previous result still points at uloop compile,
// because a reused result differs from the Unity build just as a compiled one does.
func TestCompileCheckNextActionsPointsAtUnityCompileForAReusedResult(t *testing.T) {
	assertCompileCheckNextActions(t, compileCheckResponse{
		ReusedAssemblies: compileCheckMessageNames(1),
	}, []string{compileCheckUnityCompileAction})
}

// Verifies that a clean run without a fast check points only at uloop compile.
func TestCompileCheckNextActionsPointsAtUnityCompileOnSuccess(t *testing.T) {
	assertCompileCheckNextActions(t, compileCheckResponse{
		CompiledAssemblies: compileCheckMessageNames(1),
	}, []string{compileCheckUnityCompileAction})
}

// Verifies that a run where nothing changed since the Unity build names no next action and still
// serializes the field as an empty list rather than null.
func TestCompileCheckNextActionsAreEmptyWhenNothingChanged(t *testing.T) {
	response := buildCompileCheckResponse(compilecheck.Result{
		DagDir: "Library/Bee/artifacts/1234.dag", Skipped: 12,
	}, "/projects/sample")

	if len(response.NextActions) != 0 {
		t.Fatalf("expected no next action for an unchanged project, got %q", response.NextActions)
	}
	encoded, err := json.Marshal(response)
	if err != nil {
		t.Fatalf("marshal the response: %v", err)
	}
	if !strings.Contains(string(encoded), `"NextActions":[]`) {
		t.Fatalf("expected NextActions to serialize as an empty list, got %s", encoded)
	}
}
