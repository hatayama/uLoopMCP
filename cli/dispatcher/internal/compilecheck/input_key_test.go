package compilecheck

import (
	"crypto/sha256"
	"encoding/hex"
	"os"
	"path/filepath"
	"reflect"
	"sort"
	"testing"
	"time"
)

// inputKeyProject is a project tree holding every kind of input one unit's key is built from, so a
// test can move exactly one of them and watch what the key does.
type inputKeyProject struct {
	root  string
	paths EditorCompilerPaths
	plan  BuildPlan
}

// newInputKeyProject writes the files of a two-assembly project: Alpha, whose key the tests read,
// and Beta, which Alpha references and this run compiles. Gamma is referenced but not compiled, so
// it stands for an assembly the last Unity build left behind.
func newInputKeyProject(t *testing.T) *inputKeyProject {
	t.Helper()
	root := t.TempDir()
	outputDir := filepath.Join("Library", "uloop", "compile-check", "aaaa.dag")

	writeProjectFile(t, root, filepath.Join("Editor", "csc.dll"), "compiler")
	writeProjectFile(t, root, filepath.Join("Editor", "dotnet"), "host")
	writeProjectFile(t, root, filepath.Join(planDagDirectory, "Alpha.rsp"), "-out:\"Alpha.dll\"\n")
	writeProjectFile(t, root, filepath.Join("Assets", "Alpha", "Alpha.cs"), "class Alpha {}")
	writeProjectFile(t, root, filepath.Join("Assets", "Analyzers", "Banned.txt"), "T:System.DateTime")
	writeProjectFile(t, root, filepath.Join("Packages", "Analyzer.dll"), "analyzer")
	writeProjectFile(t, root, filepath.Join(planDagDirectory, "Gamma.ref.dll"), "gamma surface")
	writeProjectFile(t, root, filepath.Join(outputDir, "Beta.ref.dll"), "beta surface")

	alpha := ResponseFile{
		AssemblyName:  "Alpha",
		Path:          filepath.Join(root, planDagDirectory, "Alpha.rsp"),
		OutputPath:    filepath.Join(planDagDirectory, "Alpha.dll"),
		RefOutputPath: filepath.Join(planDagDirectory, "Alpha"+referenceAssemblyExtension),
		References: []string{
			filepath.Join(planDagDirectory, "Beta"+referenceAssemblyExtension),
			filepath.Join(planDagDirectory, "Gamma"+referenceAssemblyExtension),
		},
		Analyzers:      []string{filepath.Join("Packages", "Analyzer.dll")},
		AdditionalFile: filepath.Join("Assets", "Analyzers", "Banned.txt"),
	}
	beta := ResponseFile{
		AssemblyName:  "Beta",
		Path:          filepath.Join(root, planDagDirectory, "Beta.rsp"),
		OutputPath:    filepath.Join(planDagDirectory, "Beta.dll"),
		RefOutputPath: filepath.Join(planDagDirectory, "Beta"+referenceAssemblyExtension),
	}

	return &inputKeyProject{
		root: root,
		paths: EditorCompilerPaths{
			DotnetHostPath:  filepath.Join(root, "Editor", "dotnet"),
			CompilerDllPath: filepath.Join(root, "Editor", "csc.dll"),
		},
		plan: BuildPlan{
			DagDir:    planDagDirectory,
			OutputDir: outputDir,
			Units: []CompileUnit{
				{Assembly: alpha, Sources: []string{filepath.Join("Assets", "Alpha", "Alpha.cs")}},
				{Assembly: beta},
			},
		},
	}
}

// key computes the key of the unit under test.
func (project *inputKeyProject) key(t *testing.T) string {
	t.Helper()
	keyResult, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
	key := keyResult.Key
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}
	if key == "" {
		t.Fatal("expected a non-empty key")
	}

	return key
}

// writeProjectFile writes one file of the fixture, creating the directories above it.
func writeProjectFile(t *testing.T, root string, relativePath string, content string) {
	t.Helper()
	path := filepath.Join(root, relativePath)
	if err := os.MkdirAll(filepath.Dir(path), outputDirPermissions); err != nil {
		t.Fatalf("failed to create the directory of %s: %v", relativePath, err)
	}
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatalf("failed to write %s: %v", relativePath, err)
	}
}

// touchProjectFile gives a file a newer modification time without changing what it holds.
func touchProjectFile(t *testing.T, root string, relativePath string) {
	t.Helper()
	path := filepath.Join(root, relativePath)
	newer := time.Now().Add(time.Hour)
	if err := os.Chtimes(path, newer, newer); err != nil {
		t.Fatalf("failed to touch %s: %v", relativePath, err)
	}
}

// Verifies the same inputs produce the same key, so an unchanged assembly is recognized as one.
func TestComputeInputKeyIsStableForUnchangedInputs(t *testing.T) {
	project := newInputKeyProject(t)

	if first, second := project.key(t), project.key(t); first != second {
		t.Errorf("the key should not move on its own, got %s then %s", first, second)
	}
}

// Verifies a source edit moves the key, and that restoring the content brings the original key
// back even though the file is now newer than it was.
func TestComputeInputKeyFollowsSourceContentRatherThanItsTimestamp(t *testing.T) {
	project := newInputKeyProject(t)
	source := filepath.Join("Assets", "Alpha", "Alpha.cs")
	original := project.key(t)

	writeProjectFile(t, project.root, source, "class Alpha { void Added() {} }")
	edited := project.key(t)
	if edited == original {
		t.Error("editing a source should move the key")
	}

	writeProjectFile(t, project.root, source, "class Alpha {}")
	touchProjectFile(t, project.root, source)
	if restored := project.key(t); restored != original {
		t.Error("restoring a source's content should bring its key back")
	}
}

// Verifies a reference this run produced is keyed by content: recompiling the assembly it belongs
// to writes the same bytes with a new timestamp, and that must not count as a changed input.
func TestComputeInputKeyKeysAProducedReferenceByContent(t *testing.T) {
	project := newInputKeyProject(t)
	reference := filepath.Join(project.plan.OutputDir, "Beta"+referenceAssemblyExtension)
	original := project.key(t)

	writeProjectFile(t, project.root, reference, "beta surface")
	touchProjectFile(t, project.root, reference)
	if rewritten := project.key(t); rewritten != original {
		t.Error("rewriting a produced reference with the same bytes should leave the key alone")
	}

	writeProjectFile(t, project.root, reference, "beta surface with more")
	if changed := project.key(t); changed == original {
		t.Error("a produced reference with different bytes should move the key")
	}
}

// Verifies a reference this run does not produce is keyed by its timestamp, so a Unity build that
// rewrote it counts as a changed input whatever it wrote.
func TestComputeInputKeyKeysAnExternalReferenceByItsTimestamp(t *testing.T) {
	project := newInputKeyProject(t)
	original := project.key(t)

	touchProjectFile(t, project.root, filepath.Join(planDagDirectory, "Gamma"+referenceAssemblyExtension))

	if touched := project.key(t); touched == original {
		t.Error("a reference from the last Unity build should be keyed by its timestamp")
	}
}

// Verifies the response file Bee wrote, and the companion file beside it, are part of the key: they
// carry the defines, the analyzer list and the path mapping csc compiles with.
func TestComputeInputKeyFollowsTheResponseFiles(t *testing.T) {
	project := newInputKeyProject(t)
	responseFile := filepath.Join(planDagDirectory, "Alpha.rsp")
	companionFile := filepath.Join(planDagDirectory, "Alpha"+companionFileExtension)
	original := project.key(t)

	writeProjectFile(t, project.root, responseFile, "-out:\"Alpha.dll\"\n-define:EXTRA\n")
	edited := project.key(t)
	if edited == original {
		t.Error("a changed response file should move the key")
	}

	writeProjectFile(t, project.root, companionFile, "/pathmap:\"X\"=.\n")
	withCompanion := project.key(t)
	if withCompanion == edited {
		t.Error("a companion response file that appeared should move the key")
	}

	writeProjectFile(t, project.root, companionFile, "/pathmap:\"Y\"=.\n")
	if changedCompanion := project.key(t); changedCompanion == withCompanion {
		t.Error("a changed companion response file should move the key")
	}
}

// Verifies the files an analyzer reads are keyed by content: a banned-symbol list or a ruleset the
// project edits changes which diagnostics csc reports, so reusing the previous ones would lie.
func TestComputeInputKeyFollowsTheContentOfAnalyzerInputFiles(t *testing.T) {
	ruleset := filepath.Join("Assets", "Default.ruleset")
	bannedSymbols := filepath.Join("Analyzers", "Banned.txt")
	project := newInputKeyProject(t)
	writeProjectFile(t, project.root, ruleset, "<RuleSet/>")
	writeProjectFile(t, project.root, bannedSymbols, "T:System.Console")
	project.plan.Units[0].Assembly.OtherFlags = []string{
		`-ruleset:"Assets/Default.ruleset"`,
		"-additionalfile:Analyzers/Banned.txt",
	}
	original := project.key(t)

	touchProjectFile(t, project.root, ruleset)
	if touched := project.key(t); touched != original {
		t.Error("touching a ruleset without editing it should leave the key alone")
	}

	writeProjectFile(t, project.root, ruleset, "<RuleSet Name=\"Other\"/>")
	editedRuleset := project.key(t)
	if editedRuleset == original {
		t.Error("an edited ruleset should move the key")
	}

	writeProjectFile(t, project.root, bannedSymbols, "T:System.Console\nT:System.DateTime")
	if editedList := project.key(t); editedList == editedRuleset {
		t.Error("an edited analyzer input file should move the key")
	}
}

// Verifies the /additionalfile csc reads is keyed by content as well.
func TestComputeInputKeyFollowsTheContentOfTheAdditionalFile(t *testing.T) {
	project := newInputKeyProject(t)
	original := project.key(t)

	writeProjectFile(t, project.root, filepath.Join("Assets", "Analyzers", "Banned.txt"), "T:System.IO.File")

	if edited := project.key(t); edited == original {
		t.Error("an edited additional file should move the key")
	}
}

// Verifies a flag naming a file this check does not follow refuses to produce a key at all, so the
// assembly compiles every time rather than being reused against an input nothing watches.
func TestComputeInputKeyRefusesAFlagWhoseFileItDoesNotFollow(t *testing.T) {
	for _, flag := range []string{`/keyfile:"Keys/Sign.snk"`, "-keyfile:Keys/Sign.snk", "/LinkResource:x.res"} {
		project := newInputKeyProject(t)
		project.plan.Units[0].Assembly.OtherFlags = []string{flag}

		keyResult, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
		key := keyResult.Key
		if err == nil {
			t.Errorf("%s names a file that is not followed, so it should refuse a key", flag)
		}
		if key != "" {
			t.Errorf("%s should produce no key, got %q", flag, key)
		}
	}
}

// Verifies a flag that only names where csc writes does not stop a key from being built.
func TestComputeInputKeyAcceptsFlagsThatOnlyNameOutputs(t *testing.T) {
	project := newInputKeyProject(t)
	project.plan.Units[0].Assembly.OtherFlags = []string{"-doc:Alpha.xml", "/errorlog:Alpha.json", "-nowarn:0649"}

	project.key(t)
}

// Verifies the compiler itself is part of the key: an Editor upgrade compiles the same sources into
// different diagnostics.
func TestComputeInputKeyFollowsTheCompiler(t *testing.T) {
	project := newInputKeyProject(t)
	original := project.key(t)

	touchProjectFile(t, project.root, filepath.Join("Editor", "csc.dll"))

	if touched := project.key(t); touched == original {
		t.Error("a compiler with a new timestamp should move the key")
	}
}

// Verifies a missing input refuses a key rather than keying the assembly on what is left, which
// would reuse diagnostics for a compile that cannot even run.
func TestComputeInputKeyRefusesAKeyWhenAnInputIsMissing(t *testing.T) {
	project := newInputKeyProject(t)
	if err := os.Remove(
		filepath.Join(project.root, planDagDirectory, "Gamma"+referenceAssemblyExtension)); err != nil {
		t.Fatalf("failed to remove the reference: %v", err)
	}

	if _, err := computeInputKey(
		project.root, project.paths, project.plan, project.plan.Units[0]); err == nil {
		t.Error("expected a missing reference to refuse a key")
	}
}

// Verifies a flag placed in Bee's second response file is read exactly like one in the first: those
// lines reach csc verbatim, so an analyzer input named there decides the diagnostics just the same.
func TestComputeInputKeyFollowsAnalyzerInputsNamedInTheCompanionResponseFile(t *testing.T) {
	ruleset := filepath.Join("Assets", "Companion.ruleset")
	project := newInputKeyProject(t)
	writeProjectFile(t, project.root, ruleset, "<RuleSet/>")
	project.plan.Units[0].Assembly.CompanionFlags = []string{`-ruleset:"Assets/Companion.ruleset"`}
	original := project.key(t)

	touchProjectFile(t, project.root, ruleset)
	if touched := project.key(t); touched != original {
		t.Error("touching a ruleset named in the companion file should leave the key alone")
	}

	writeProjectFile(t, project.root, ruleset, "<RuleSet Name=\"Other\"/>")
	if edited := project.key(t); edited == original {
		t.Error("editing a ruleset named in the companion file should move the key")
	}
}

// Verifies a flag naming an unfollowed file refuses a key from the companion response file too,
// rather than letting the assembly be reused against an input nothing watches.
func TestComputeInputKeyRefusesAnUnfollowedFlagInTheCompanionResponseFile(t *testing.T) {
	project := newInputKeyProject(t)
	project.plan.Units[0].Assembly.CompanionFlags = []string{`/keyfile:"Keys/Sign.snk"`}

	keyResult, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
	key := keyResult.Key
	if err == nil {
		t.Error("a keyfile in the companion response file should refuse a key")
	}
	if key != "" {
		t.Errorf("expected no key, got %q", key)
	}
}

// Verifies a missing primary response file refuses a key rather than being keyed on its absence.
// Bee writes one for every assembly it builds, so a run that cannot find it is looking at a state
// this check cannot describe - unlike the companion file, which Bee legitimately leaves out.
func TestComputeInputKeyRefusesAKeyWhenTheResponseFileIsGone(t *testing.T) {
	project := newInputKeyProject(t)
	if _, err := computeInputKey(
		project.root, project.paths, project.plan, project.plan.Units[0]); err != nil {
		t.Fatalf("the fixture should produce a key, got %v", err)
	}

	if err := os.Remove(project.plan.Units[0].Assembly.Path); err != nil {
		t.Fatal(err)
	}

	keyResult, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
	key := keyResult.Key
	if err == nil {
		t.Error("a missing response file should refuse a key")
	}
	if key != "" {
		t.Errorf("expected no key, got %q", key)
	}
}

// nonSourceKey computes the non-source key of the unit under test.
func (project *inputKeyProject) nonSourceKey(t *testing.T) nonSourceIdentity {
	t.Helper()
	key, err := computeNonSourceKey(project.root, project.paths, project.plan, project.plan.Units[0])
	if err != nil {
		t.Fatalf("expected a non-source key, got error: %v", err)
	}

	return key
}

// Verifies the non-source key ignores what the sources hold, so a body edit leaves it alone.
func TestNonSourceKeyIgnoresSourceContent(t *testing.T) {
	project := newInputKeyProject(t)
	original := project.nonSourceKey(t)

	writeProjectFile(t, project.root, filepath.Join("Assets", "Alpha", "Alpha.cs"), "class Alpha { void M() {} }")

	if edited := project.nonSourceKey(t); edited.Key != original.Key {
		t.Error("editing a source should leave the non-source key alone")
	}
}

// Verifies the non-source key moves when a source is added or removed, so a changed source set
// falls back to a full compile.
func TestNonSourceKeyChangesWhenSourceAddedOrRemoved(t *testing.T) {
	project := newInputKeyProject(t)
	original := project.nonSourceKey(t)
	added := filepath.Join("Assets", "Alpha", "Added.cs")
	writeProjectFile(t, project.root, added, "class Added {}")

	project.plan.Units[0].Sources = append(project.plan.Units[0].Sources, added)
	if withAdded := project.nonSourceKey(t); withAdded.Key == original.Key {
		t.Error("adding a source should move the non-source key")
	}

	project.plan.Units[0].Sources = []string{added}
	if withRemoved := project.nonSourceKey(t); withRemoved.Key == original.Key {
		t.Error("removing a source should move the non-source key")
	}
}

// Verifies the non-source key follows the inputs other than source contents, a reference and the
// response file, and names which kind of input moved.
func TestNonSourceKeyChangesWithReferenceAndRsp(t *testing.T) {
	project := newInputKeyProject(t)
	original := project.nonSourceKey(t)

	touchProjectFile(t, project.root, filepath.Join(planDagDirectory, "Gamma"+referenceAssemblyExtension))
	touched := project.nonSourceKey(t)
	if touched.Key == original.Key {
		t.Error("a rebuilt reference should move the non-source key")
	}
	if kinds := changedInputKinds(touched, original); !reflect.DeepEqual(kinds, []string{"references"}) {
		t.Errorf("changed kinds = %v, want [references]", kinds)
	}

	writeProjectFile(t, project.root, filepath.Join(planDagDirectory, "Alpha.rsp"), "-out:\"Alpha.dll\"\n-define:EXTRA\n")
	edited := project.nonSourceKey(t)
	if edited.Key == touched.Key {
		t.Error("a changed response file should move the non-source key")
	}
	if kinds := changedInputKinds(edited, touched); !reflect.DeepEqual(kinds, []string{"response file"}) {
		t.Errorf("changed kinds = %v, want [response file]", kinds)
	}
}

// Verifies the input key still digests every input into one stream in the order it always did, so
// manifests written before the non-source key was split into parts stay reusable.
func TestInputKeyDigestsEveryPartInOneStream(t *testing.T) {
	project := newInputKeyProject(t)
	unit := project.plan.Units[0]
	result, err := computeInputKey(project.root, project.paths, project.plan, unit)
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}

	digest := sha256.New()
	if err := writeCompilerIdentity(digest, project.paths); err != nil {
		t.Fatalf("compiler: %v", err)
	}
	if err := writeResponseFileIdentity(digest, unit.Assembly); err != nil {
		t.Fatalf("response file: %v", err)
	}
	if err := writeSourceIdentity(digest, project.root, unit.Sources, map[string]string{}); err != nil {
		t.Fatalf("sources: %v", err)
	}
	if err := writeReferenceIdentity(digest, project.root, project.plan, unit); err != nil {
		t.Fatalf("references: %v", err)
	}
	if err := writeAnalyzerIdentity(digest, project.root, unit.Assembly.Analyzers); err != nil {
		t.Fatalf("analyzers: %v", err)
	}
	if err := writeAnalyzerInputIdentity(digest, project.root, unit.Assembly); err != nil {
		t.Fatalf("analyzer inputs: %v", err)
	}
	writeOutputIdentity(digest, unit.Assembly)

	if want := hex.EncodeToString(digest.Sum(nil)); result.Key != want {
		t.Errorf("input key = %s, want %s", result.Key, want)
	}
}

// Verifies the input key returns the sha256 of each source it read, keyed as unit.Sources spells it.
func TestInputKeyReturnsHashesOfContentItRead(t *testing.T) {
	project := newInputKeyProject(t)
	source := filepath.Join("Assets", "Alpha", "Alpha.cs")

	result, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}

	sum := sha256.Sum256([]byte("class Alpha {}"))
	expected := map[string]string{source: hex.EncodeToString(sum[:])}
	if !reflect.DeepEqual(result.SourceHashes, expected) {
		t.Errorf("expected source hashes %v, got %v", expected, result.SourceHashes)
	}
}

// identity computes the whole input key result of the unit under test.
func (project *inputKeyProject) identity(t *testing.T) inputKeyResult {
	t.Helper()
	keyResult, err := computeInputKey(project.root, project.paths, project.plan, project.plan.Units[0])
	if err != nil {
		t.Fatalf("expected a key, got error: %v", err)
	}

	return keyResult
}

// assertShapeKept fails when a change that should only move one project reference moved the shape,
// or did not move the key and that reference's identity.
func assertShapeKept(t *testing.T, before inputKeyResult, after inputKeyResult, reference string) {
	t.Helper()
	if after.Key == before.Key {
		t.Error("the change should move the key")
	}
	if after.ProjectReferences[reference] == before.ProjectReferences[reference] {
		t.Errorf("the change should move the identity of %s", reference)
	}
	if after.Shape != before.Shape {
		t.Error("the change should leave the shape alone")
	}
}

// Verifies the shape leaves out the content of a reference this run produced, which the identity of
// that reference carries instead: a record must stay comparable while that reference is unknown.
func TestComputeInputKeyShapeIgnoresTheIdentityOfAProducedReference(t *testing.T) {
	project := newInputKeyProject(t)
	before := project.identity(t)

	writeProjectFile(t, project.root,
		filepath.Join(project.plan.OutputDir, "Beta"+referenceAssemblyExtension), "beta surface with more")

	assertShapeKept(t, before, project.identity(t), "Beta")
}

// Verifies the shape leaves out the timestamp of Unity's file for a reference this run compiles but
// reads from Unity's build, which the identity of that reference carries instead.
func TestComputeInputKeyShapeIgnoresTheIdentityOfAKeptReference(t *testing.T) {
	project := newInputKeyProject(t)
	unityReference := filepath.Join(planDagDirectory, "Beta"+referenceAssemblyExtension)
	writeProjectFile(t, project.root, unityReference, "unity beta")
	project.plan.Units[0].KeptSurfaceReferences = map[string]bool{"Beta": true}
	before := project.identity(t)

	touchProjectFile(t, project.root, unityReference)

	assertShapeKept(t, before, project.identity(t), "Beta")
}

// Verifies the key, the shape and the project reference identities do not depend on whether a
// referenced assembly is in the plan: when it is and kept Unity's surface, the compile reads the
// same file as when it is not, so a record must stay comparable as the reference enters or leaves.
func TestComputeInputKeyShapeDoesNotDependOnThePlan(t *testing.T) {
	project := newInputKeyProject(t)
	outsidePlan := project.identity(t)

	project.plan.Units = append(project.plan.Units, CompileUnit{Assembly: ResponseFile{AssemblyName: "Gamma"}})
	project.plan.Units[0].KeptSurfaceReferences = map[string]bool{"Gamma": true}
	insidePlan := project.identity(t)

	if insidePlan.Key != outsidePlan.Key {
		t.Error("the key should not depend on whether a kept reference is in the plan")
	}
	if insidePlan.Shape != outsidePlan.Shape {
		t.Error("the shape should not depend on whether a kept reference is in the plan")
	}
	if !reflect.DeepEqual(insidePlan.ProjectReferences, outsidePlan.ProjectReferences) {
		t.Errorf("project references = %v, want %v", insidePlan.ProjectReferences, outsidePlan.ProjectReferences)
	}
}

// Verifies a reference that is not a project assembly - a package or system dll - stays in the
// shape: it is never unknown while the run compiles, so the shape is what follows it.
func TestComputeInputKeyShapeFollowsAReferenceOutsideTheProject(t *testing.T) {
	project := newInputKeyProject(t)
	library := filepath.Join("Packages", "Library.dll")
	writeProjectFile(t, project.root, library, "library")
	project.plan.Units[0].Assembly.References = append(project.plan.Units[0].Assembly.References, library)
	before := project.identity(t)

	touchProjectFile(t, project.root, library)
	after := project.identity(t)

	if after.Shape == before.Shape {
		t.Error("a reference outside the project should move the shape")
	}
	if !reflect.DeepEqual(after.ProjectReferences, before.ProjectReferences) {
		t.Error("a reference outside the project should leave the project reference identities alone")
	}
}

// Verifies the shape follows the content of the sources and the project reference identities do not.
func TestComputeInputKeyShapeFollowsTheSources(t *testing.T) {
	project := newInputKeyProject(t)
	before := project.identity(t)

	writeProjectFile(t, project.root, filepath.Join("Assets", "Alpha", "Alpha.cs"), "class Alpha { int Added; }")
	after := project.identity(t)

	if after.Shape == before.Shape {
		t.Error("an edited source should move the shape")
	}
	if !reflect.DeepEqual(after.ProjectReferences, before.ProjectReferences) {
		t.Error("an edited source should leave the project reference identities alone")
	}
}

// Verifies the project reference identities name every project assembly the unit references, in the
// plan or not, and nothing else.
func TestComputeInputKeyNamesEveryProjectReference(t *testing.T) {
	project := newInputKeyProject(t)
	library := filepath.Join("Packages", "Library.dll")
	writeProjectFile(t, project.root, library, "library")
	project.plan.Units[0].Assembly.References = append(project.plan.Units[0].Assembly.References, library)

	identity := project.identity(t)

	names := []string{}
	for name := range identity.ProjectReferences {
		names = append(names, name)
	}
	sort.Strings(names)
	if !reflect.DeepEqual(names, []string{"Beta", "Gamma"}) {
		t.Errorf("project references = %v, want [Beta Gamma]", names)
	}
}
