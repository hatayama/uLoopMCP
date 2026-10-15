package resultstore

import (
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"
)

// projectStore is a store over an output directory inside a project root, laid out the way the
// check lays it out, with Alpha's outputs already written.
type projectStore struct {
	root  string
	store Store[testDiagnostic]
}

// newProjectStore lays out a project root with an output directory holding Alpha's outputs.
func newProjectStore(t *testing.T) projectStore {
	t.Helper()
	root := t.TempDir()
	outputDir := filepath.Join("Library", "out")
	makeDirAt(t, filepath.Join(root, outputDir))
	project := projectStore{root: root, store: Open[testDiagnostic](root, outputDir)}
	writeOutputFile(t, project.outputPath(), "Alpha"+assemblyExtension, "assembly")
	writeOutputFile(t, project.outputPath(), "Alpha"+referenceAssemblyExtension, "surface")

	return project
}

// outputPath names the output directory on disk.
func (project projectStore) outputPath() string {
	return filepath.Join(project.root, "Library", "out")
}

// generatedPath names Alpha's generator output directory on disk.
func (project projectStore) generatedPath() string {
	return filepath.Join(project.root, project.store.GeneratedDirectory("Alpha"))
}

// recordCompile records a compile of Alpha under key, failing the test when it cannot.
func (project projectStore) recordCompile(t *testing.T, key string, succeeded bool) {
	t.Helper()
	result := CompileResult[testDiagnostic]{
		Succeeded:   succeeded,
		Diagnostics: []testDiagnostic{{Severity: "warning", Code: "CS0168", File: "Assets/A.cs", Line: 1, Column: 1}},
	}
	if err := project.store.RecordCompile("Alpha", key, result, alphaOutputs()); err != nil {
		t.Fatalf("failed to record the compile: %v", err)
	}
}

// recordBaseline records a baseline of Alpha under key, with one generated file on disk.
func (project projectStore) recordBaseline(t *testing.T, key string) BaselineInputs {
	t.Helper()
	writeFileAt(t, filepath.Join(project.generatedPath(), "Gen", "G.g.cs"), "class G { }")
	inputs := BaselineInputs{
		InputKey:     key,
		NonSourceKey: KeyDigest{Key: "non-source", Parts: map[string]string{"compiler": "c"}},
		WorkerKey:    "worker",
		Files:        map[string]BaselineFile{"Assets/A.cs": {ContentHash: "a", Fingerprint: "f"}},
	}
	if err := project.store.RecordBaseline("Alpha", inputs); err != nil {
		t.Fatalf("failed to record the baseline: %v", err)
	}

	return inputs
}

// Verifies a recorded baseline comes back with what it was recorded with, the diagnostics of the
// compile it describes, and its generator output named relative to the project root.
func TestLoadBaselineHandsBackWhatWasRecorded(t *testing.T) {
	project := newProjectStore(t)
	project.recordCompile(t, "input", true)
	inputs := project.recordBaseline(t, "input")

	baseline, reason := project.store.LoadBaseline("Alpha")

	if reason != "" {
		t.Fatalf("reason = %q, want a usable baseline", reason)
	}
	if baseline.InputKey != inputs.InputKey || baseline.WorkerKey != inputs.WorkerKey ||
		!reflect.DeepEqual(baseline.NonSourceKey, inputs.NonSourceKey) || !reflect.DeepEqual(baseline.Files, inputs.Files) {
		t.Errorf("baseline = %+v, want what was recorded: %+v", baseline, inputs)
	}
	if baseline.GeneratedDirectory != "Library/out/Alpha.generated" {
		t.Errorf("generated directory = %q", baseline.GeneratedDirectory)
	}
	if len(baseline.Diagnostics) != 1 || baseline.Diagnostics[0].Code != "CS0168" {
		t.Errorf("diagnostics = %+v, want the recorded compile's", baseline.Diagnostics)
	}
	if !project.store.GeneratorOutputUnchanged(baseline) {
		t.Error("expected the generator output to match right after recording")
	}
}

// Verifies the generator output stops matching its baseline when a generated file is changed,
// removed, or added.
func TestGeneratorOutputUnchangedNoticesEveryChange(t *testing.T) {
	for name, damage := range map[string]func(t *testing.T, generatedPath string){
		"changed": func(t *testing.T, generatedPath string) {
			writeFileAt(t, filepath.Join(generatedPath, "Gen", "G.g.cs"), "class G { int x; }")
		},
		"removed": func(t *testing.T, generatedPath string) {
			if err := os.RemoveAll(generatedPath); err != nil {
				t.Fatalf("failed to remove the generator output: %v", err)
			}
		},
		"added": func(t *testing.T, generatedPath string) {
			writeFileAt(t, filepath.Join(generatedPath, "Gen", "H.g.cs"), "class H { }")
		},
	} {
		t.Run(name, func(t *testing.T) {
			project := newProjectStore(t)
			project.recordCompile(t, "input", true)
			project.recordBaseline(t, "input")
			baseline, reason := project.store.LoadBaseline("Alpha")
			if reason != "" {
				t.Fatalf("reason = %q, want a usable baseline", reason)
			}

			damage(t, project.generatedPath())

			if project.store.GeneratorOutputUnchanged(baseline) {
				t.Error("expected the damaged generator output not to match")
			}
		})
	}
}

// Verifies a baseline is not handed out once the compile it describes is no longer what is on disk.
func TestLoadBaselineRefusesABaselineWhoseCompileIsGone(t *testing.T) {
	for name, damage := range map[string]func(t *testing.T, project projectStore){
		"compile result removed": func(t *testing.T, project projectStore) {
			if err := os.Remove(manifestPath(project.outputPath(), "Alpha")); err != nil {
				t.Fatalf("failed to remove the compile result: %v", err)
			}
		},
		"compile result unreadable": func(t *testing.T, project projectStore) {
			writeOutputFile(t, project.outputPath(), "Alpha"+resultManifestExtension, "{not json")
		},
		"compile of another key": func(t *testing.T, project projectStore) {
			project.recordCompile(t, "other", true)
		},
		"failed compile": func(t *testing.T, project projectStore) {
			project.recordCompile(t, "input", false)
		},
		"output changed": func(t *testing.T, project projectStore) {
			writeOutputFile(t, project.outputPath(), "Alpha"+assemblyExtension, "changed by hand")
		},
	} {
		t.Run(name, func(t *testing.T) {
			project := newProjectStore(t)
			project.recordCompile(t, "input", true)
			project.recordBaseline(t, "input")

			damage(t, project)

			if _, reason := project.store.LoadBaseline("Alpha"); reason != "baseline compile is no longer on disk" {
				t.Errorf("reason = %q", reason)
			}
		})
	}
}

// Verifies a missing baseline is explained from the compile result recorded without it.
func TestLoadBaselineExplainsAMissingBaseline(t *testing.T) {
	for name, testCase := range map[string]struct {
		record func(t *testing.T, project projectStore)
		want   string
	}{
		"no compile result": {
			record: func(*testing.T, projectStore) {},
			want:   "no baseline: no full compile of this assembly is on record",
		},
		"unreadable compile result": {
			record: func(t *testing.T, project projectStore) {
				writeOutputFile(t, project.outputPath(), "Alpha"+resultManifestExtension, "{not json")
			},
			want: "no baseline: no full compile of this assembly is on record",
		},
		"failed compile": {
			record: func(t *testing.T, project projectStore) { project.recordCompile(t, "input", false) },
			want:   "no baseline: the last full compile had errors; the first full compile without errors records one",
		},
		"successful compile": {
			record: func(t *testing.T, project projectStore) { project.recordCompile(t, "input", true) },
			want:   "no baseline: the last full compile did not record one",
		},
	} {
		t.Run(name, func(t *testing.T) {
			project := newProjectStore(t)
			testCase.record(t, project)

			if _, reason := project.store.LoadBaseline("Alpha"); reason != testCase.want {
				t.Errorf("reason = %q, want %q", reason, testCase.want)
			}
		})
	}
}

// Verifies a baseline that cannot be written is reported with the baseline's path.
func TestRecordBaselineReportsAMissingOutputDirectory(t *testing.T) {
	root := t.TempDir()
	store := Open[testDiagnostic](root, "missing")

	err := store.RecordBaseline("Alpha", BaselineInputs{InputKey: "input"})

	want := "failed to write " + baselineRecordPath(filepath.Join(root, "missing"), "Alpha") + ":"
	if err == nil || !strings.HasPrefix(err.Error(), want) {
		t.Fatalf("err = %v, want it to start with %q", err, want)
	}
}
