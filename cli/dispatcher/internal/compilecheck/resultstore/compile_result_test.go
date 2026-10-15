package resultstore

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// alphaOutputs names the two files a compile of Alpha writes in the output directory.
func alphaOutputs() []string {
	return []string{"Alpha" + assemblyExtension, "Alpha" + referenceAssemblyExtension}
}

// newRecordedOutputs prepares an output directory holding the outputs a compile of Alpha left behind.
func newRecordedOutputs(t *testing.T) string {
	t.Helper()
	directoryPath := t.TempDir()
	writeOutputFile(t, directoryPath, "Alpha"+assemblyExtension, "assembly")
	writeOutputFile(t, directoryPath, "Alpha"+referenceAssemblyExtension, "surface")

	return directoryPath
}

// directoryStore opens the records kept directly in directoryPath.
func directoryStore(directoryPath string) Store[testDiagnostic] {
	return Open[testDiagnostic](directoryPath, "")
}

// storedResult is the result the tests record and expect to read back unchanged.
func storedResult() CompileResult[testDiagnostic] {
	return CompileResult[testDiagnostic]{
		Succeeded: false,
		Diagnostics: []testDiagnostic{{
			Severity: "error",
			Code:     "CS0103",
			Message:  "The name 'Missing' does not exist in the current context",
			File:     filepath.Join("Assets", "Alpha", "Alpha.cs"),
			Line:     3,
			Column:   9,
		}},
	}
}

// recordResult stores one compile of Alpha's outcome under a key.
func recordResult(t *testing.T, directoryPath string, key string) {
	t.Helper()
	if err := directoryStore(directoryPath).RecordCompile("Alpha", KeyIdentity{Key: key}, storedResult(), alphaOutputs()); err != nil {
		t.Fatalf("failed to write the manifest: %v", err)
	}
}

// blockManifestPath puts a non-empty directory where a unit's manifest belongs, so renaming the
// finished manifest into place fails on every platform.
func blockManifestPath(t *testing.T, manifestFilePath string) {
	t.Helper()
	writeFileAt(t, filepath.Join(manifestFilePath, "occupied"), "")
}

// Verifies a recorded compile is handed back with the diagnostics and the outcome it had, rather
// than as a success: an assembly that failed must keep failing until something about it changes.
func TestReusableResultReplaysTheRecordedDiagnostics(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordResult(t, directoryPath, "key")

	result, reusable := directoryStore(directoryPath).Reusable("Alpha", "key")

	if !reusable {
		t.Fatal("expected the recorded result to be reusable")
	}
	if result.Succeeded {
		t.Error("a failed compile must not come back as a successful one")
	}
	if len(result.Diagnostics) != 1 || result.Diagnostics[0].Code != "CS0103" {
		t.Errorf("diagnostics = %+v, want the recorded CS0103", result.Diagnostics)
	}
}

// Verifies a result recorded for other inputs is not reused.
func TestReusableResultRejectsAnotherKey(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordResult(t, directoryPath, "key")

	if _, reusable := directoryStore(directoryPath).Reusable("Alpha", "other key"); reusable {
		t.Error("a result recorded for other inputs should not be reused")
	}
}

// Verifies an output that no longer holds what the compile wrote stops the reuse: the assemblies
// below this one read those bytes, so replaying diagnostics over them would check nothing.
func TestReusableResultRejectsAChangedOutput(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordResult(t, directoryPath, "key")

	writeOutputFile(t, directoryPath, "Alpha"+referenceAssemblyExtension, "another surface")

	if _, reusable := directoryStore(directoryPath).Reusable("Alpha", "key"); reusable {
		t.Error("a changed output should stop the reuse")
	}
}

// Verifies a missing output stops the reuse.
func TestReusableResultRejectsAMissingOutput(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordResult(t, directoryPath, "key")

	if err := os.Remove(filepath.Join(directoryPath, "Alpha"+assemblyExtension)); err != nil {
		t.Fatalf("failed to remove the output: %v", err)
	}

	if _, reusable := directoryStore(directoryPath).Reusable("Alpha", "key"); reusable {
		t.Error("a missing output should stop the reuse")
	}
}

// Verifies a manifest that cannot be read is treated as no manifest at all rather than as a
// failure: the assembly simply compiles again.
func TestReusableResultRejectsAnUnreadableManifest(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	writeOutputFile(t, directoryPath, "Alpha.result.json", "{not json")

	if _, reusable := directoryStore(directoryPath).Reusable("Alpha", "key"); reusable {
		t.Error("an unreadable manifest should not be reused")
	}
}

// Verifies a manifest written by another version of this check is not reused.
func TestReusableResultRejectsAnotherFormatVersion(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordResult(t, directoryPath, "key")
	setManifestFormatVersion(t, directoryPath, "Alpha", resultManifestFormatVersion+1)

	if _, reusable := directoryStore(directoryPath).Reusable("Alpha", "key"); reusable {
		t.Error("a manifest of another format version should not be reused")
	}
}

// Verifies a manifest of format version 1, whose outputs were compiled from sources in ordinal
// rather than response file order, is not reused under the same key.
func TestReusableResultRejectsAManifestCompiledInOrdinalSourceOrder(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordResult(t, directoryPath, "key")
	setManifestFormatVersion(t, directoryPath, "Alpha", 1)

	if _, reusable := directoryStore(directoryPath).Reusable("Alpha", "key"); reusable {
		t.Error("a manifest of format version 1 should not be reused")
	}
}

// setManifestFormatVersion rewrites the format version of an assembly's manifest, leaving the rest.
func setManifestFormatVersion(t *testing.T, directoryPath string, assembly string, version int) {
	t.Helper()
	path := manifestPath(directoryPath, assembly)
	content, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("failed to read the manifest: %v", err)
	}
	var manifest unitResultManifest[testDiagnostic]
	if err := json.Unmarshal(content, &manifest); err != nil {
		t.Fatalf("failed to parse the manifest: %v", err)
	}
	manifest.FormatVersion = version
	rewritten, err := json.Marshal(manifest)
	if err != nil {
		t.Fatalf("failed to write the manifest back: %v", err)
	}
	if err := os.WriteFile(path, rewritten, 0o600); err != nil {
		t.Fatalf("failed to write the manifest back: %v", err)
	}
}

// Verifies a compile that wrote no outputs still records what it reported, so the next run can tell
// a failure it already knows from one it has not seen.
func TestRecordCompileRecordsACompileThatWroteNothing(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	for _, name := range alphaOutputs() {
		if err := os.Remove(filepath.Join(directoryPath, name)); err != nil {
			t.Fatalf("failed to remove %s: %v", name, err)
		}
	}
	recordResult(t, directoryPath, "key")

	result, reusable := directoryStore(directoryPath).Reusable("Alpha", "key")

	if !reusable {
		t.Fatal("expected a compile that wrote nothing to be reusable")
	}
	if len(result.Diagnostics) != 1 {
		t.Errorf("diagnostics = %+v, want the recorded one", result.Diagnostics)
	}
}

// Verifies a manifest that cannot be renamed into place is reported as a write failure and leaves
// no temporary file behind.
func TestRecordCompileReportsAFailedRename(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	blockManifestPath(t, manifestPath(directoryPath, "Alpha"))

	err := directoryStore(directoryPath).RecordCompile("Alpha", KeyIdentity{Key: "key"}, storedResult(), alphaOutputs())

	if err == nil || !strings.HasPrefix(err.Error(), "failed to write "+manifestPath(directoryPath, "Alpha")+":") {
		t.Fatalf("err = %v", err)
	}
	matches, globErr := filepath.Glob(filepath.Join(directoryPath, "*"+resultManifestTempSuffix))
	if globErr != nil || len(matches) != 0 {
		t.Fatalf("temporary manifests left behind: %v (glob error %v)", matches, globErr)
	}
}

// Verifies a manifest cannot be written into an output directory that does not exist, which is
// reported with the manifest path.
func TestRecordCompileReportsAMissingOutputDirectory(t *testing.T) {
	directoryPath := filepath.Join(t.TempDir(), "missing")

	err := directoryStore(directoryPath).RecordCompile("Alpha", KeyIdentity{Key: "key"}, storedResult(), alphaOutputs())

	if err == nil || !strings.HasPrefix(err.Error(), "failed to write "+manifestPath(directoryPath, "Alpha")+":") {
		t.Fatalf("err = %v", err)
	}
}

// Verifies an output that exists but cannot be read is reported instead of being treated as absent.
func TestHashOutputsReportsAnUnreadableOutput(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	unreadablePath := filepath.Join(directoryPath, "Alpha"+assemblyExtension)
	if err := os.Remove(unreadablePath); err != nil {
		t.Fatalf("failed to remove the assembly: %v", err)
	}
	makeDirAt(t, unreadablePath)

	_, complete, err := hashOutputs(directoryPath, alphaOutputs(), true)

	if err == nil || complete || !strings.Contains(err.Error(), unreadablePath) {
		t.Fatalf("complete=%v err=%v, want an error naming %s", complete, err, unreadablePath)
	}
}

// Verifies only the named outputs are hashed, even when another output is on disk.
func TestHashOutputsHashesOnlyTheNamedOutputs(t *testing.T) {
	directoryPath := newRecordedOutputs(t)

	outputs, complete, err := hashOutputs(directoryPath, []string{"Alpha" + assemblyExtension}, true)

	if err != nil || !complete || len(outputs) != 1 || outputs["Alpha"+assemblyExtension] == "" {
		t.Fatalf("outputs=%v complete=%v err=%v", outputs, complete, err)
	}
}

// Verifies forgetting an assembly removes its manifest, its baseline, and its saved generator output,
// so nothing describes a compile whose outputs are gone.
func TestForgetRemovesTheManifestTheBaselineAndTheGeneratorOutput(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordResult(t, directoryPath, "key")
	writeOutputFile(t, directoryPath, "Alpha"+baselineRecordExtension, "{}")
	generatedPath := filepath.Join(directoryPath, "Alpha"+generatedDirectoryExtension, "Nested")
	if err := os.MkdirAll(generatedPath, testDirectoryPermissions); err != nil {
		t.Fatalf("failed to create the generated directory: %v", err)
	}
	writeOutputFile(t, generatedPath, "G.cs", "class G {}")

	if err := directoryStore(directoryPath).Forget("Alpha"); err != nil {
		t.Fatalf("expected the records to be removed, got error: %v", err)
	}

	for _, name := range []string{
		"Alpha" + resultManifestExtension, "Alpha" + baselineRecordExtension, "Alpha" + generatedDirectoryExtension,
	} {
		if _, err := os.Stat(filepath.Join(directoryPath, name)); !os.IsNotExist(err) {
			t.Errorf("expected %s to be removed, got %v", name, err)
		}
	}
}

// recordIdentity stores one compile of Alpha's outcome under the shape and project reference
// identities of the key it ran on.
func recordIdentity(
	t *testing.T, directoryPath string, shape string, projectReferences map[string]ReferenceIdentity,
) {
	t.Helper()
	identity := KeyIdentity{Key: "key", Shape: shape, ProjectReferences: projectReferences}
	if err := directoryStore(directoryPath).RecordCompile("Alpha", identity, storedResult(), alphaOutputs()); err != nil {
		t.Fatalf("failed to write the manifest: %v", err)
	}
}

// unityLine is the identity of a reference read from Unity's build.
func unityLine(hash string) ReferenceIdentity {
	return ReferenceIdentity{Kind: UnityReference, Hash: hash}
}

// producedLine is the identity of a reference read from this run's output.
func producedLine(hash string) ReferenceIdentity {
	return ReferenceIdentity{Kind: ProducedReference, Hash: hash}
}

// shapeOf hands MayStillApply a shape, counting how many times it was asked for one.
func shapeOf(shape string, asked *int) func() (string, error) {
	return func() (string, error) {
		*asked++

		return shape, nil
	}
}

// Verifies a record still may apply when the only difference is an unknown reference it read from
// this run's output: what that reference finishes as decides the key, and it has not finished.
func TestMayStillApplyWhenOnlyUnknownReferencesDiffer(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape",
		map[string]ReferenceIdentity{"Beta": producedLine("produced beta"), "Gamma": unityLine("gamma")})
	asked := 0

	mayApply := directoryStore(directoryPath).MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": unityLine("unity beta"), "Gamma": unityLine("gamma")},
		map[string]bool{"Beta": true}, shapeOf("shape", &asked))

	if !mayApply {
		t.Error("a record differing only in an unknown reference read from this run's output should still apply")
	}
}

// Verifies an unknown reference the record read from Unity's build must still match Unity's file:
// whatever it finishes as, a moved Unity file can never give the recorded line back. The same
// record with Unity's file as recorded still applies.
func TestMayStillApplyRefusesAnUnknownReferenceWhoseUnityFileMoved(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape", map[string]ReferenceIdentity{"Beta": unityLine("unity beta")})
	asked := 0
	store := directoryStore(directoryPath)
	unknown := map[string]bool{"Beta": true}

	if !store.MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": unityLine("unity beta")}, unknown, shapeOf("shape", &asked)) {
		t.Fatal("a record whose unknown reference is still Unity's file as recorded should still apply")
	}
	if store.MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": unityLine("rebuilt unity beta")}, unknown, shapeOf("shape", &asked)) {
		t.Error("a record whose unknown reference read a Unity file that has moved should not apply")
	}
}

// Verifies a record no longer applies when a reference that is not unknown has another identity,
// and that the shape, which reads every source, is not asked for then.
func TestMayStillApplyRefusesAChangedKnownReference(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape",
		map[string]ReferenceIdentity{"Beta": producedLine("beta"), "Gamma": unityLine("gamma")})
	asked := 0

	mayApply := directoryStore(directoryPath).MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": producedLine("beta"), "Gamma": unityLine("another gamma")},
		map[string]bool{"Beta": true}, shapeOf("shape", &asked))

	if mayApply {
		t.Error("a record whose known reference changed should not apply")
	}
	if asked != 0 {
		t.Errorf("the shape was asked for %d times, want none once a known reference differs", asked)
	}
}

// Verifies a known reference must match in kind as well as in hash: a line read from Unity's build
// never matches one read from this run's output.
func TestMayStillApplyRefusesAKnownReferenceOfAnotherKind(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape", map[string]ReferenceIdentity{"Beta": producedLine("beta")})
	asked := 0

	if directoryStore(directoryPath).MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": unityLine("beta")}, map[string]bool{}, shapeOf("shape", &asked)) {
		t.Error("a known reference read from another file should not apply")
	}
}

// Verifies the shape is asked for exactly once when everything before it matches, and decides the
// answer then.
func TestMayStillApplyAsksForTheShapeOnceEverythingElseMatches(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape", map[string]ReferenceIdentity{"Beta": producedLine("beta")})
	store := directoryStore(directoryPath)
	current := map[string]ReferenceIdentity{"Beta": producedLine("beta")}

	sameAsked := 0
	if !store.MayStillApply("Alpha", current, map[string]bool{}, shapeOf("shape", &sameAsked)) {
		t.Error("a record matching in everything should still apply")
	}
	otherAsked := 0
	if store.MayStillApply("Alpha", current, map[string]bool{}, shapeOf("another shape", &otherAsked)) {
		t.Error("a record of another shape should not apply")
	}
	if sameAsked != 1 || otherAsked != 1 {
		t.Errorf("the shape was asked for %d and %d times, want once each", sameAsked, otherAsked)
	}
}

// Verifies a shape that cannot be computed means the record does not apply.
func TestMayStillApplyRefusesAShapeThatCannotBeComputed(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape", map[string]ReferenceIdentity{"Beta": producedLine("beta")})

	if directoryStore(directoryPath).MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": producedLine("beta")}, map[string]bool{},
		func() (string, error) { return "shape", os.ErrNotExist }) {
		t.Error("a record whose shape cannot be computed should not apply")
	}
}

// Verifies a record no longer applies when the unit references another set of project assemblies,
// whether one was added or one was dropped, even when the difference is an unknown reference.
func TestMayStillApplyRefusesAnotherSetOfReferences(t *testing.T) {
	cases := map[string]map[string]ReferenceIdentity{
		"a reference the record lacks": {
			"Beta": producedLine("beta"), "Gamma": producedLine("gamma"), "Delta": producedLine("delta"),
		},
		"a reference no longer there": {"Beta": producedLine("beta")},
	}
	for name, current := range cases {
		t.Run(name, func(t *testing.T) {
			directoryPath := newRecordedOutputs(t)
			recordIdentity(t, directoryPath, "shape",
				map[string]ReferenceIdentity{"Beta": producedLine("beta"), "Gamma": producedLine("gamma")})
			asked := 0
			unknown := map[string]bool{"Gamma": true, "Delta": true}

			if directoryStore(directoryPath).MayStillApply("Alpha", current, unknown, shapeOf("shape", &asked)) {
				t.Error("a record over another set of project references should not apply")
			}
		})
	}
}

// Verifies a record no longer applies once an output it hashed holds other bytes.
func TestMayStillApplyRefusesChangedOutputs(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape", map[string]ReferenceIdentity{"Beta": producedLine("beta")})
	writeOutputFile(t, directoryPath, "Alpha"+referenceAssemblyExtension, "another surface")
	asked := 0

	if directoryStore(directoryPath).MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": producedLine("beta")}, map[string]bool{}, shapeOf("shape", &asked)) {
		t.Error("a record whose outputs changed should not apply")
	}
	if asked != 0 {
		t.Errorf("the shape was asked for %d times, want none once an output differs", asked)
	}
}

// Verifies a record of format version 2, which kept no shape, neither may apply nor is reused.
func TestMayStillApplyRefusesAnotherFormatVersion(t *testing.T) {
	directoryPath := newRecordedOutputs(t)
	recordIdentity(t, directoryPath, "shape", map[string]ReferenceIdentity{"Beta": producedLine("beta")})
	setManifestFormatVersion(t, directoryPath, "Alpha", 2)
	asked := 0

	if directoryStore(directoryPath).MayStillApply("Alpha",
		map[string]ReferenceIdentity{"Beta": producedLine("beta")}, map[string]bool{}, shapeOf("shape", &asked)) {
		t.Error("a record of format version 2 should not apply")
	}
	if _, reusable := directoryStore(directoryPath).Reusable("Alpha", "key"); reusable {
		t.Error("a record of format version 2 should not be reused")
	}
}
