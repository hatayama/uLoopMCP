package automation

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

const (
	unityPackageReleaseTestWaitingFor  = "Project runner release uloop-project-runner-v3.10.0 is not published with complete assets yet; the Unity package release v3.14.0 waits for it."
	unityPackageReleaseTestTagRef      = "refs/tags/v3.14.0"
	unityPackageReleaseTestPublished   = `{"tag_name": "v3.14.0", "draft": false, "assets": [{"name": "io.github.hatayama.uloopmcp-3.14.0.tgz", "size": 2048}]}`
	unityPackageReleaseTestUnsigned    = `{"tag_name": "v3.14.0", "draft": false, "assets": [{"name": "notes.txt", "size": 10}]}`
	unityPackageReleaseTestEmptyAsset  = `{"tag_name": "v3.14.0", "draft": false, "assets": [{"name": "io.github.hatayama.uloopmcp-3.14.0.tgz", "size": 0}]}`
	unityPackageReleaseTestPlanVersion = `{"Packages/src": "3.14.0", "cli/dispatcher": "3.8.1"}`
)

func writeUnityPackageReleaseManifest(t *testing.T, content string) string {
	t.Helper()
	repoRoot := t.TempDir()
	if err := os.WriteFile(filepath.Join(repoRoot, ".release-please-manifest.json"), []byte(content), 0o600); err != nil {
		t.Fatalf("write manifest: %v", err)
	}
	return repoRoot
}

func runPlanUnityPackageReleaseForTest(fake *fakeUnityPackageReleaseCommands, repoRoot string, dryRun bool) (int, string, string) {
	var stdout bytes.Buffer
	var stderr bytes.Buffer
	code := runPlanUnityPackageReleaseWithDeps(context.Background(), &stdout, &stderr, unityPackageReleasePlanOptions{
		Repository: unityPackageReleaseTestRepository,
		RepoRoot:   repoRoot,
		DryRun:     dryRun,
	}, unityPackageReleasePlanDeps{runOutput: fake.runOutput, checkRelease: fake.checkRelease})
	return code, stdout.String(), stderr.String()
}

// Verifies the plan creates a release only when none is published and the readiness check names
// its release commit, never touches a published release except to dry-run sign it from its tag,
// runs the check only for a release that is not published yet, reports what a release that is
// not due waits for in its notice, and keeps what the check reports out of GITHUB_OUTPUT.
func TestPlanUnityPackageReleaseDecidesFromReleaseState(t *testing.T) {
	tests := []struct {
		name           string
		release        fakeUnityPackageReleaseResponse
		readiness      unityPackageReleaseReadiness
		dryRun         bool
		wantSign       bool
		wantPublish    bool
		wantSourceRef  string
		wantAnnotation string
		wantCheckCalls int
	}{
		{
			name:          "published release with the tarball is skipped",
			release:       fakeUnityPackageReleaseResponse{output: unityPackageReleaseTestPublished},
			wantSourceRef: unityPackageReleaseTestTagRef,
		},
		{
			name:          "dry run signs a published release from its tag",
			release:       fakeUnityPackageReleaseResponse{output: unityPackageReleaseTestPublished},
			dryRun:        true,
			wantSign:      true,
			wantSourceRef: unityPackageReleaseTestTagRef,
		},
		{
			name:           "published release without the tarball warns",
			release:        fakeUnityPackageReleaseResponse{output: unityPackageReleaseTestUnsigned},
			wantSourceRef:  unityPackageReleaseTestTagRef,
			wantAnnotation: "::warning::",
		},
		{
			name:           "published release with an empty tarball warns",
			release:        fakeUnityPackageReleaseResponse{output: unityPackageReleaseTestEmptyAsset},
			wantSourceRef:  unityPackageReleaseTestTagRef,
			wantAnnotation: "::warning::",
		},
		{
			name:          "dry run signs a published release without the tarball from its tag",
			release:       fakeUnityPackageReleaseResponse{output: unityPackageReleaseTestUnsigned},
			dryRun:        true,
			wantSign:      true,
			wantSourceRef: unityPackageReleaseTestTagRef,
		},
		{
			name:           "due release is signed and published from its release commit",
			release:        unityPackageReleaseTestNotFound,
			readiness:      unityPackageReleaseReadiness{ReleaseCommit: unityPackageReleaseTestCommit},
			wantSign:       true,
			wantPublish:    true,
			wantSourceRef:  unityPackageReleaseTestCommit,
			wantCheckCalls: 1,
		},
		{
			name:           "dry run signs a due release without publishing",
			release:        unityPackageReleaseTestNotFound,
			readiness:      unityPackageReleaseReadiness{ReleaseCommit: unityPackageReleaseTestCommit},
			dryRun:         true,
			wantSign:       true,
			wantSourceRef:  unityPackageReleaseTestCommit,
			wantCheckCalls: 1,
		},
		{
			name:           "release that is not due yet is skipped",
			release:        unityPackageReleaseTestNotFound,
			readiness:      unityPackageReleaseReadiness{WaitingFor: unityPackageReleaseTestWaitingFor},
			wantAnnotation: "::notice::",
			wantCheckCalls: 1,
		},
		{
			name:           "dry run skips a release that is not due yet",
			release:        unityPackageReleaseTestNotFound,
			readiness:      unityPackageReleaseReadiness{WaitingFor: unityPackageReleaseTestWaitingFor},
			dryRun:         true,
			wantAnnotation: "::notice::",
			wantCheckCalls: 1,
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			fake := newFakeUnityPackageReleaseCommands(t, map[string][]fakeUnityPackageReleaseResponse{"release": {test.release}})
			fake.readiness = test.readiness
			code, stdout, stderr := runPlanUnityPackageReleaseForTest(fake, writeUnityPackageReleaseManifest(t, unityPackageReleaseTestPlanVersion), test.dryRun)
			if code != 0 {
				t.Fatalf("exit code = %d, stderr = %s", code, stderr)
			}
			want := fmt.Sprintf("sign=%t\npublish=%t\n", test.wantSign, test.wantPublish) +
				"tag=v3.14.0\n" +
				"version=3.14.0\n" +
				"asset-name=io.github.hatayama.uloopmcp-3.14.0.tgz\n" +
				"source-ref=" + test.wantSourceRef + "\n"
			if stdout != want {
				t.Fatalf("stdout = %q, want %q", stdout, want)
			}
			if strings.TrimSpace(stderr) == "" {
				t.Fatal("stderr has no reason for the decision")
			}
			// The decision is the last line; what the readiness check reported comes before it.
			lines := strings.Split(strings.TrimSuffix(stderr, "\n"), "\n")
			decision := lines[len(lines)-1]
			annotation := ""
			for _, prefix := range []string{"::warning::", "::notice::"} {
				if strings.HasPrefix(decision, prefix) {
					annotation = prefix
				}
			}
			if annotation != test.wantAnnotation {
				t.Fatalf("stderr = %q, want annotation %q", stderr, test.wantAnnotation)
			}
			if test.readiness.WaitingFor != "" && !strings.HasSuffix(stderr, "\n::notice::"+test.readiness.WaitingFor+"\n") {
				t.Fatalf("stderr = %q, want a notice of what the release waits for", stderr)
			}
			if test.wantCheckCalls > 0 && !strings.HasPrefix(stderr, unityPackageReleaseTestCheckLog+"\n") {
				t.Fatalf("stderr = %q, want what the check reported in the job log", stderr)
			}
			if len(fake.checkVersions) != test.wantCheckCalls {
				t.Fatalf("check calls = %v, want %d", fake.checkVersions, test.wantCheckCalls)
			}
			if test.wantCheckCalls > 0 && fake.checkVersions[0] != "3.14.0" {
				t.Fatalf("checked version = %q, want the manifest version 3.14.0", fake.checkVersions[0])
			}
		})
	}
}

// Verifies every input the plan cannot act on fails the run without GITHUB_OUTPUT lines, so the
// workflow never signs a guessed release commit, and that a failed release lookup is never
// mistaken for a release that does not exist yet: the readiness check is not run after it.
func TestPlanUnityPackageReleaseFailsWithoutOutput(t *testing.T) {
	tests := []struct {
		name           string
		manifest       string
		release        []fakeUnityPackageReleaseResponse
		readiness      unityPackageReleaseReadiness
		checkErr       error
		wantCheckCalls int
	}{
		{
			name:           "check failure fails the plan",
			release:        []fakeUnityPackageReleaseResponse{unityPackageReleaseTestNotFound},
			checkErr:       errors.New("exit status 1"),
			wantCheckCalls: 1,
		},
		{
			name:           "release commit that is not a full SHA fails the plan",
			release:        []fakeUnityPackageReleaseResponse{unityPackageReleaseTestNotFound},
			readiness:      unityPackageReleaseReadiness{ReleaseCommit: "0123456"},
			wantCheckCalls: 1,
		},
		{
			name:           "check that answers neither a commit nor a wait fails the plan",
			release:        []fakeUnityPackageReleaseResponse{unityPackageReleaseTestNotFound},
			wantCheckCalls: 1,
		},
		{
			name:           "check that answers both a commit and a wait fails the plan",
			release:        []fakeUnityPackageReleaseResponse{unityPackageReleaseTestNotFound},
			readiness:      unityPackageReleaseReadiness{ReleaseCommit: unityPackageReleaseTestCommit, WaitingFor: unityPackageReleaseTestWaitingFor},
			wantCheckCalls: 1,
		},
		{
			name:    "lookup failure fails the plan",
			release: []fakeUnityPackageReleaseResponse{{err: errors.New("gh api failed: exit status 1\nHTTP 401: Bad credentials")}},
		},
		{
			name:    "release lookup that is not JSON",
			release: []fakeUnityPackageReleaseResponse{{output: "Not JSON"}},
		},
		{
			name:    "release lookup that answers with a draft",
			release: []fakeUnityPackageReleaseResponse{{output: `{"tag_name": "v3.14.0", "draft": true, "assets": []}`}},
		},
		{
			name:    "release lookup that answers with another tag",
			release: []fakeUnityPackageReleaseResponse{{output: `{"tag_name": "v3.13.0", "draft": false, "assets": []}`}},
		},
		{name: "manifest without the package entry", manifest: `{"cli/dispatcher": "3.8.1"}`},
		{name: "manifest that is not JSON", manifest: `Packages/src: 3.14.0`},
		{name: "manifest version that is not a release tag", manifest: `{"Packages/src": "3.14"}`},
		{name: "missing manifest"},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			repoRoot := t.TempDir()
			if test.release != nil {
				repoRoot = writeUnityPackageReleaseManifest(t, unityPackageReleaseTestPlanVersion)
			}
			if test.manifest != "" {
				repoRoot = writeUnityPackageReleaseManifest(t, test.manifest)
			}
			fake := newFakeUnityPackageReleaseCommands(t, map[string][]fakeUnityPackageReleaseResponse{"release": test.release})
			fake.readiness = test.readiness
			fake.checkErr = test.checkErr
			code, stdout, _ := runPlanUnityPackageReleaseForTest(fake, repoRoot, false)
			if code == 0 || stdout != "" {
				t.Fatalf("exit code = %d, stdout = %q; want a failure without GITHUB_OUTPUT lines", code, stdout)
			}
			if len(fake.checkVersions) != test.wantCheckCalls {
				t.Fatalf("check calls = %v, want %d", fake.checkVersions, test.wantCheckCalls)
			}
			if test.release == nil && len(fake.calls) != 0 {
				t.Fatalf("calls = %v, want none before the manifest names a release", fake.calls)
			}
		})
	}
}

// Verifies the repository comes from --repo or GITHUB_REPOSITORY, that the default repository
// root is the git root holding the release-please manifest, which the workflow relies on when it
// runs the command from cli/release-automation, and that the removed --tag flag is rejected.
func TestParseUnityPackageReleasePlanOptions(t *testing.T) {
	noEnv := func(string) (string, bool) { return "", false }
	repositoryEnv := func(name string) (string, bool) {
		if name == "GITHUB_REPOSITORY" {
			return unityPackageReleaseTestRepository, true
		}
		return "", false
	}

	options, err := parseUnityPackageReleasePlanOptions(context.Background(), []string{"--dry-run=true"}, repositoryEnv)
	if err != nil {
		t.Fatalf("parse with GITHUB_REPOSITORY: %v", err)
	}
	if options.Repository != unityPackageReleaseTestRepository || !options.DryRun {
		t.Fatalf("options = %+v, want repository from GITHUB_REPOSITORY and a dry run", options)
	}
	if _, err := os.Stat(filepath.Join(options.RepoRoot, ".release-please-manifest.json")); err != nil {
		t.Fatalf("default repository root %q has no release-please manifest: %v", options.RepoRoot, err)
	}

	options, err = parseUnityPackageReleasePlanOptions(context.Background(), []string{"--dry-run=false"}, repositoryEnv)
	if err != nil || options.DryRun {
		t.Fatalf("parse with --dry-run=false: options = %+v, err = %v; want a run that creates the release", options, err)
	}

	if _, err := parseUnityPackageReleasePlanOptions(context.Background(), []string{"--dry-run=false", "--repo-root", t.TempDir()}, noEnv); err == nil {
		t.Fatal("parse without --repo or GITHUB_REPOSITORY succeeded, want an error")
	}
	if _, err := parseUnityPackageReleasePlanOptions(context.Background(), []string{"--dry-run=false", "--tag", "v3.13.0"}, repositoryEnv); err == nil {
		t.Fatal("parse with --tag succeeded, want an error")
	}
}

// Verifies a plan without --dry-run is rejected rather than read as "not a dry run": a workflow
// that stopped passing the flag would otherwise create an immutable release from a manual dry run.
func TestParseUnityPackageReleasePlanOptionsRequiresDryRun(t *testing.T) {
	repositoryEnv := func(string) (string, bool) { return unityPackageReleaseTestRepository, true }
	_, err := parseUnityPackageReleasePlanOptions(context.Background(), []string{"--repo-root", t.TempDir()}, repositoryEnv)
	if err == nil || !strings.Contains(err.Error(), "--dry-run") {
		t.Fatalf("parse without --dry-run: err = %v, want an error naming --dry-run", err)
	}
}

// Verifies the command entry point fails without GITHUB_OUTPUT lines when the repository root has
// no release-please manifest to name the release.
func TestRunPlanUnityPackageReleaseFailsWithoutManifest(t *testing.T) {
	var stdout bytes.Buffer
	var stderr bytes.Buffer
	code := RunPlanUnityPackageRelease(context.Background(), &stdout, &stderr, []string{"--repo", unityPackageReleaseTestRepository, "--repo-root", t.TempDir(), "--dry-run=false"})
	if code != 1 || stdout.Len() != 0 {
		t.Fatalf("exit code = %d, stdout = %q; want 1 without output", code, stdout.String())
	}
}

// Verifies malformed arguments exit with the usage status before any release is read.
func TestRunPlanUnityPackageReleaseRejectsUnknownFlag(t *testing.T) {
	var stdout bytes.Buffer
	var stderr bytes.Buffer
	code := RunPlanUnityPackageRelease(context.Background(), &stdout, &stderr, []string{"--unknown"})
	if code != 2 || stdout.Len() != 0 {
		t.Fatalf("exit code = %d, stdout = %q; want 2 without output", code, stdout.String())
	}
}
