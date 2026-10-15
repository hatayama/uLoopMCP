package dispatcher

import (
	"strconv"
	"testing"
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
