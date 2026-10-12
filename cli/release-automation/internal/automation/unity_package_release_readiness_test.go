package automation

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

const (
	readinessTestVersion       = "3.0.0-beta.6"
	readinessTestRunnerTag     = "uloop-project-runner-v3.0.0-beta.6"
	readinessTestDispatcherTag = "dispatcher-v3.0.0"
	readinessTestTagCommit     = "1111111111111111111111111111111111111111"
	readinessTestRunbook       = "docs/release-recovery-runbook.md"
)

var (
	readinessTestRunnerAssets     = []string{"uloop-project-runner-darwin-arm64.tar.gz", "uloop-project-runner-darwin-arm64.tar.gz.sha256"}
	readinessTestDispatcherAssets = []string{"install.sh", "uloop-dispatcher-darwin-arm64.tar.gz"}
)

// readinessTestRepo is a git repository whose HEAD is a follow-up commit after the release-please
// release commit of 3.0.0-beta.6, so a check that names the branch tip instead is caught.
type readinessTestRepo struct {
	root          string
	releaseCommit string
}

func newReadinessTestRepo(t *testing.T) readinessTestRepo {
	t.Helper()
	return newReadinessTestRepoWithRelease(t, func(*testing.T, string) {})
}

// newReadinessTestRepoWithRelease lets prepareRelease change the files of the release commit before
// it is committed.
func newReadinessTestRepoWithRelease(t *testing.T, prepareRelease func(t *testing.T, root string)) readinessTestRepo {
	t.Helper()
	root := t.TempDir()
	runGitInRepo(t, root, "init", "-q", "-b", "main")
	writeReadinessReleaseFiles(t, root, "3.0.0-beta.5", unityPackageManifestKey, "CHANGELOG.md")
	commitReadinessTestRepo(t, root, "chore(v3-beta): release 3.0.0-beta.5")
	writeReadinessReleaseFiles(t, root, readinessTestVersion, unityPackageManifestKey, "CHANGELOG.md")
	prepareRelease(t, root)
	commitReadinessTestRepo(t, root, "chore: release v3-beta")
	releaseCommit := strings.TrimSpace(runGitInRepoOutput(t, root, "rev-parse", "HEAD"))
	writeReadinessTestFile(t, root, "follow-up.txt", "follow-up\n")
	commitReadinessTestRepo(t, root, "fix: follow up after release")
	return readinessTestRepo{root: root, releaseCommit: releaseCommit}
}

func commitReadinessTestRepo(t *testing.T, root string, subject string) {
	t.Helper()
	runGitInRepo(t, root, "add", "-A")
	runGitInRepo(t, root, "commit", "-q", "-m", subject)
}

// writeReadinessReleaseFiles writes the release-please inputs of one release as release-please
// lays them out: the manifest one entry per line, and the changelog with the version heading.
func writeReadinessReleaseFiles(t *testing.T, root string, version string, packageKey string, changelogPath string) {
	t.Helper()
	writeReadinessTestFile(t, root, releasePleaseConfigRelativePath, fmt.Sprintf(`{
  "packages": {
    %q: {"component": "unity-package", "include-v-in-tag": true, "include-component-in-tag": false, "changelog-path": %q},
    "cli/dispatcher": {"component": "dispatcher", "include-v-in-tag": true, "include-component-in-tag": true, "changelog-path": "CHANGELOG.md"},
    "cli/project-runner": {"component": "uloop-project-runner", "include-v-in-tag": true, "include-component-in-tag": true, "changelog-path": "CHANGELOG.md"}
  }
}
`, packageKey, changelogPath))
	writeReadinessTestFile(t, root, releasePleaseManifestRelativePath, fmt.Sprintf(`{
  %q: %q,
  "cli/dispatcher": %q,
  "cli/project-runner": %q
}
`, packageKey, version, version, version))
	writeReadinessTestFile(t, root, unityPackageCliPinFile, fmt.Sprintf(`{
  "projectRunnerVersion": %q,
  "minimumDispatcherVersion": "3.0.0"
}
`, version))
	writeReadinessTestFile(t, root, "Packages/src/CHANGELOG.md", fmt.Sprintf("# Changelog\n\n## [%s](https://example.test/compare/old...new)\n\n### Bug Fixes\n\n* keep the package release baseline available\n", version))
}

func writeReadinessTestFile(t *testing.T, root string, relativePath string, content string) {
	t.Helper()
	path := filepath.Join(root, filepath.FromSlash(relativePath))
	if err := os.MkdirAll(filepath.Dir(path), 0o700); err != nil {
		t.Fatalf("create directory of %s: %v", relativePath, err)
	}
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatalf("write %s: %v", relativePath, err)
	}
}

func readinessTestReleaseJSON(tag string, assets []string, emptyAsset string) string {
	entries := make([]string, 0, len(assets))
	for _, asset := range assets {
		size := 1
		if asset == emptyAsset {
			size = 0
		}
		entries = append(entries, fmt.Sprintf(`{"name": %q, "size": %d}`, asset, size))
	}
	return fmt.Sprintf(`{"tag_name": %q, "draft": false, "assets": [%s]}`, tag, strings.Join(entries, ", "))
}

func readinessTestAttestations(digests ...string) string {
	entries := make([]string, 0, len(digests))
	for _, digest := range digests {
		entries = append(entries, fmt.Sprintf(`{"verificationResult": {"signature": {"certificate": {"sourceRepositoryDigest": %q}}}}`, digest))
	}
	return "[" + strings.Join(entries, ", ") + "]"
}

// fakeReadinessGitHub answers the gh reads of the readiness check. Any other gh command, a write
// above all, fails the test.
type fakeReadinessGitHub struct {
	t             *testing.T
	releases      map[string]fakeUnityPackageReleaseResponse
	tagCommit     string
	attestations  string
	emptyDownload string
	// failures makes the tag commit lookup ("commit"), the asset download ("download"), or the
	// attestation verification ("verify") fail with the error.
	failures map[string]error
	commands []string
}

func newFakeReadinessGitHub(t *testing.T) *fakeReadinessGitHub {
	return &fakeReadinessGitHub{
		t: t,
		releases: map[string]fakeUnityPackageReleaseResponse{
			readinessTestRunnerTag:     {output: readinessTestReleaseJSON(readinessTestRunnerTag, readinessTestRunnerAssets, "")},
			readinessTestDispatcherTag: {output: readinessTestReleaseJSON(readinessTestDispatcherTag, readinessTestDispatcherAssets, "")},
		},
		tagCommit:    readinessTestTagCommit,
		attestations: readinessTestAttestations(readinessTestTagCommit),
	}
}

func (fake *fakeReadinessGitHub) runOutput(_ context.Context, name string, args ...string) (string, error) {
	fake.t.Helper()
	command := name + " " + strings.Join(args, " ")
	fake.commands = append(fake.commands, command)
	repository := "repos/" + unityPackageReleaseTestRepository
	switch {
	case strings.HasPrefix(command, "gh api "+repository+"/releases/tags/"):
		response, found := fake.releases[strings.TrimPrefix(command, "gh api "+repository+"/releases/tags/")]
		if !found {
			return unityPackageReleaseTestNotFound.output, unityPackageReleaseTestNotFound.err
		}
		return response.output, response.err
	case command == "gh api "+repository+"/commits/"+readinessTestRunnerTag+" --jq .sha":
		return fake.tagCommit + "\n", fake.failures["commit"]
	case strings.HasPrefix(command, "gh release download "+readinessTestRunnerTag+" --repo "+unityPackageReleaseTestRepository+" "):
		if err := fake.failures["download"]; err != nil {
			return "", err
		}
		fake.download(args)
		return "", nil
	case strings.HasPrefix(command, "gh attestation verify ") &&
		strings.Contains(command, " --repo "+unityPackageReleaseTestRepository+" ") &&
		strings.HasSuffix(command, " --signer-workflow "+unityPackageReleaseTestRepository+"/.github/workflows/native-cli-publish.yml --format json"):
		return fake.attestations, fake.failures["verify"]
	}
	fake.t.Fatalf("unexpected command %s", command)
	return "", nil
}

// download writes every --pattern file into --dir, leaving the one named by emptyDownload empty.
func (fake *fakeReadinessGitHub) download(args []string) {
	fake.t.Helper()
	directory := ""
	var names []string
	for index := 0; index+1 < len(args); index++ {
		switch args[index] {
		case "--dir":
			directory = args[index+1]
		case "--pattern":
			names = append(names, args[index+1])
		}
	}
	for _, name := range names {
		content := "release asset\n"
		if name == fake.emptyDownload {
			content = ""
		}
		if err := os.WriteFile(filepath.Join(directory, name), []byte(content), 0o600); err != nil {
			fake.t.Fatalf("write downloaded %s: %v", name, err)
		}
	}
}

func (fake *fakeReadinessGitHub) ran(prefix string) bool {
	for _, command := range fake.commands {
		if strings.HasPrefix(command, prefix) {
			return true
		}
	}
	return false
}

// readinessTestRun holds the stand-ins for one readiness check: gh, the asset lists of each
// release generation, and the protocol and pin checks of the release commit.
type readinessTestRun struct {
	github       *fakeReadinessGitHub
	assetLists   map[string][]string
	protocolErr  error
	pinErr       error
	fetchedTags  []string
	protocolRefs []string
	pinRefs      []string
	log          bytes.Buffer
}

func newReadinessTestRun(t *testing.T) *readinessTestRun {
	return &readinessTestRun{
		github: newFakeReadinessGitHub(t),
		assetLists: map[string][]string{
			"scripts/verify-native-cli-release-assets.sh": readinessTestRunnerAssets,
			"scripts/verify-dispatcher-release-assets.sh": readinessTestDispatcherAssets,
		},
	}
}

func (run *readinessTestRun) check(repoRoot string, version string) (unityPackageReleaseReadiness, error) {
	deps := unityPackageReleaseReadinessDeps{
		runOutput: run.github.runOutput,
		requiredAssets: func(_ context.Context, _ string, verifyScript string, _ string) ([]string, error) {
			assets, found := run.assetLists[verifyScript]
			if !found {
				return nil, errors.New(verifyScript + " --list failed")
			}
			return assets, nil
		},
		fetchReleaseTag: func(_ context.Context, tag string) error {
			run.fetchedTags = append(run.fetchedTags, tag)
			return nil
		},
		verifyProtocol: func(_ context.Context, _ string, ref string) (string, error) {
			run.protocolRefs = append(run.protocolRefs, ref)
			return "protocol verified at " + ref, run.protocolErr
		},
		verifyPin: func(_ context.Context, _ string, ref string) (string, error) {
			run.pinRefs = append(run.pinRefs, ref)
			return "pin verified at " + ref, run.pinErr
		},
		log: &run.log,
	}
	return checkUnityPackageReleaseReadiness(context.Background(), deps, unityPackageReleaseReadinessInput{
		Repository: unityPackageReleaseTestRepository,
		RepoRoot:   repoRoot,
		Version:    version,
	})
}

// Verifies a due release names its release-please release commit, not the later branch tip, after
// the runner attestations and the protocol and pin checks of that commit pass, and that what those
// checks report reaches the log.
func TestCheckUnityPackageReleaseReadinessNamesTheReleaseCommit(t *testing.T) {
	repo := newReadinessTestRepo(t)
	run := newReadinessTestRun(t)

	readiness, err := run.check(repo.root, readinessTestVersion)
	if err != nil {
		t.Fatalf("check failed: %v\n%s", err, run.log.String())
	}
	if readiness != (unityPackageReleaseReadiness{ReleaseCommit: repo.releaseCommit}) {
		t.Fatalf("readiness = %+v, want only the release commit %s", readiness, repo.releaseCommit)
	}
	for _, asset := range readinessTestRunnerAssets {
		if !run.github.ran("gh attestation verify ") || !run.github.ran("gh release download "+readinessTestRunnerTag+" --repo "+unityPackageReleaseTestRepository+" --pattern "+asset+" --pattern "+asset+".sigstore.json ") {
			t.Fatalf("commands = %v, want %s and its bundle downloaded and verified", run.github.commands, asset)
		}
	}
	if !run.github.ran("gh api repos/" + unityPackageReleaseTestRepository + "/releases/tags/" + readinessTestDispatcherTag) {
		t.Fatalf("commands = %v, want the dispatcher floor release looked up", run.github.commands)
	}
	if len(run.fetchedTags) != 1 || run.fetchedTags[0] != readinessTestRunnerTag {
		t.Fatalf("fetched tags = %v, want the runner tag fetched before its attestations are verified", run.fetchedTags)
	}
	if len(run.protocolRefs) != 1 || run.protocolRefs[0] != repo.releaseCommit || len(run.pinRefs) != 1 || run.pinRefs[0] != repo.releaseCommit {
		t.Fatalf("protocol refs = %v, pin refs = %v, want each checked once at %s", run.protocolRefs, run.pinRefs, repo.releaseCommit)
	}
	for _, want := range []string{"protocol verified at " + repo.releaseCommit, "pin verified at " + repo.releaseCommit, repo.releaseCommit} {
		if !strings.Contains(run.log.String(), want) {
			t.Fatalf("log = %q, want it to contain %q", run.log.String(), want)
		}
	}
}

// Verifies the runner and dispatcher releases the package waits for are the ones its release commit
// records, not the ones a later commit on the branch records: the release is tagged at that commit,
// so its manifest and pin are what the release ships with.
func TestCheckUnityPackageReleaseReadinessReadsDependenciesAtTheReleaseCommit(t *testing.T) {
	repo := newReadinessTestRepo(t)
	writeReadinessTestFile(t, repo.root, releasePleaseManifestRelativePath, `{
  "Packages/src": "3.0.0-beta.6",
  "cli/dispatcher": "3.0.0-beta.7",
  "cli/project-runner": "3.0.0-beta.7"
}
`)
	writeReadinessTestFile(t, repo.root, unityPackageCliPinFile, `{"projectRunnerVersion": "3.0.0-beta.7", "minimumDispatcherVersion": "3.1.0"}`)
	commitReadinessTestRepo(t, repo.root, "chore: release v3-beta runner")
	run := newReadinessTestRun(t)

	readiness, err := run.check(repo.root, readinessTestVersion)
	if err != nil || readiness.ReleaseCommit != repo.releaseCommit {
		t.Fatalf("readiness = %+v, err = %v; want the release commit %s", readiness, err, repo.releaseCommit)
	}
	lookup := "gh api repos/" + unityPackageReleaseTestRepository + "/releases/tags/"
	if !run.github.ran(lookup+readinessTestRunnerTag) || !run.github.ran(lookup+readinessTestDispatcherTag) {
		t.Fatalf("commands = %v, want the releases the release commit records looked up", run.github.commands)
	}
	if run.github.ran(lookup+"uloop-project-runner-v3.0.0-beta.7") || run.github.ran(lookup+"dispatcher-v3.1.0") {
		t.Fatalf("commands = %v, want no release that only the later commit records", run.github.commands)
	}
}

// Verifies every state of a runner or dispatcher release that is not "published with every asset
// it requires" makes the release wait, naming that release, without verifying attestations or
// checking the release commit.
func TestCheckUnityPackageReleaseReadinessWaitsForDependencyReleases(t *testing.T) {
	tests := []struct {
		name    string
		prepare func(run *readinessTestRun)
		waitTag string
	}{
		{
			name:    "runner release not published",
			prepare: func(run *readinessTestRun) { delete(run.github.releases, readinessTestRunnerTag) },
			waitTag: readinessTestRunnerTag,
		},
		{
			name: "runner release without an asset",
			prepare: func(run *readinessTestRun) {
				run.github.releases[readinessTestRunnerTag] = fakeUnityPackageReleaseResponse{output: readinessTestReleaseJSON(readinessTestRunnerTag, readinessTestRunnerAssets[:1], "")}
			},
			waitTag: readinessTestRunnerTag,
		},
		{
			name: "runner release with an empty asset",
			prepare: func(run *readinessTestRun) {
				run.github.releases[readinessTestRunnerTag] = fakeUnityPackageReleaseResponse{output: readinessTestReleaseJSON(readinessTestRunnerTag, readinessTestRunnerAssets, readinessTestRunnerAssets[0])}
			},
			waitTag: readinessTestRunnerTag,
		},
		{
			name:    "dispatcher release not published",
			prepare: func(run *readinessTestRun) { delete(run.github.releases, readinessTestDispatcherTag) },
			waitTag: readinessTestDispatcherTag,
		},
		{
			name: "dispatcher release without an asset",
			prepare: func(run *readinessTestRun) {
				run.github.releases[readinessTestDispatcherTag] = fakeUnityPackageReleaseResponse{output: readinessTestReleaseJSON(readinessTestDispatcherTag, nil, "")}
			},
			waitTag: readinessTestDispatcherTag,
		},
	}

	repo := newReadinessTestRepo(t)
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			run := newReadinessTestRun(t)
			test.prepare(run)

			readiness, err := run.check(repo.root, readinessTestVersion)
			if err != nil {
				t.Fatalf("check failed: %v", err)
			}
			if readiness.ReleaseCommit != "" || !strings.Contains(readiness.WaitingFor, test.waitTag) {
				t.Fatalf("readiness = %+v, want it to wait for %s", readiness, test.waitTag)
			}
			if test.waitTag == readinessTestRunnerTag && run.github.ran("gh attestation verify ") {
				t.Fatalf("commands = %v, want no attestation verified for a runner release that is not ready", run.github.commands)
			}
			if len(run.protocolRefs) != 0 || len(run.pinRefs) != 0 {
				t.Fatalf("protocol refs = %v, pin refs = %v, want no release commit check", run.protocolRefs, run.pinRefs)
			}
		})
	}
}

// Verifies a release lookup that fails for a reason other than "not found" fails the check instead
// of reading as a release that is not published yet, and so does a lookup that answers with a
// draft, which the published-release lookup never returns.
func TestCheckUnityPackageReleaseReadinessFailsWhenALookupFails(t *testing.T) {
	tests := []struct {
		name     string
		response fakeUnityPackageReleaseResponse
		wantErr  string
	}{
		{name: "outage", response: fakeUnityPackageReleaseResponse{err: errors.New("gh api failed: exit status 1\nHTTP 502: Bad Gateway")}, wantErr: "HTTP 502"},
		{name: "draft", response: fakeUnityPackageReleaseResponse{output: `{"tag_name": "%s", "draft": true, "assets": []}`}, wantErr: "draft=true"},
	}

	repo := newReadinessTestRepo(t)
	for _, test := range tests {
		for _, tag := range []string{readinessTestRunnerTag, readinessTestDispatcherTag} {
			t.Run(test.name+" of "+tag, func(t *testing.T) {
				run := newReadinessTestRun(t)
				response := test.response
				if strings.Contains(response.output, "%s") {
					response.output = fmt.Sprintf(response.output, tag)
				}
				run.github.releases[tag] = response

				_, err := run.check(repo.root, readinessTestVersion)
				if err == nil || !strings.Contains(err.Error(), test.wantErr) {
					t.Fatalf("err = %v, want a failure that names %q", err, test.wantErr)
				}
			})
		}
	}
}

// Verifies an asset list of a published runner or dispatcher release that cannot be read, or that
// names no asset, fails the check instead of making the release wait: the list comes from a script
// that does not change by waiting, and an empty list would let a release without its assets through.
func TestCheckUnityPackageReleaseReadinessFailsOnAnUnusableAssetList(t *testing.T) {
	tests := []struct {
		name         string
		verifyScript string
		assets       []string
		listFails    bool
	}{
		{name: "runner list that cannot be read", verifyScript: projectRunnerReleaseAssetsScript, listFails: true},
		{name: "runner list that names no asset", verifyScript: projectRunnerReleaseAssetsScript},
		{name: "dispatcher list that cannot be read", verifyScript: dispatcherReleaseAssetsScript, listFails: true},
		{name: "dispatcher list that names no asset", verifyScript: dispatcherReleaseAssetsScript},
	}

	repo := newReadinessTestRepo(t)
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			run := newReadinessTestRun(t)
			if test.listFails {
				delete(run.assetLists, test.verifyScript)
			} else {
				run.assetLists[test.verifyScript] = test.assets
			}

			readiness, err := run.check(repo.root, readinessTestVersion)
			if err == nil || !strings.Contains(err.Error(), test.verifyScript) {
				t.Fatalf("readiness = %+v, err = %v; want a failure that names %s", readiness, err, test.verifyScript)
			}
			if len(run.protocolRefs) != 0 {
				t.Fatalf("protocol refs = %v, want no release commit check", run.protocolRefs)
			}
		})
	}
}

// Verifies a runner release whose attestations do not bind every asset to the commit of its tag
// fails the check and points at the recovery runbook, because no later run can repair a published
// release, and that the release commit is not checked after it.
func TestCheckUnityPackageReleaseReadinessFailsOnAnInvalidRunnerAttestation(t *testing.T) {
	tests := []struct {
		name    string
		prepare func(github *fakeReadinessGitHub)
	}{
		{name: "digest of another commit", prepare: func(github *fakeReadinessGitHub) {
			github.attestations = readinessTestAttestations(readinessTestTagCommit, unityPackageReleaseTestOtherCommit)
		}},
		{name: "no verification", prepare: func(github *fakeReadinessGitHub) { github.attestations = "[]" }},
		{name: "verification without a digest", prepare: func(github *fakeReadinessGitHub) {
			github.attestations = readinessTestAttestations(readinessTestTagCommit, "")
		}},
		{name: "empty downloaded asset", prepare: func(github *fakeReadinessGitHub) { github.emptyDownload = readinessTestRunnerAssets[1] }},
		{name: "empty downloaded bundle", prepare: func(github *fakeReadinessGitHub) {
			github.emptyDownload = readinessTestRunnerAssets[0] + ".sigstore.json"
		}},
		{name: "tag without a commit", prepare: func(github *fakeReadinessGitHub) {
			github.tagCommit = ""
			github.attestations = readinessTestAttestations("")
		}},
	}

	repo := newReadinessTestRepo(t)
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			run := newReadinessTestRun(t)
			test.prepare(run.github)

			_, err := run.check(repo.root, readinessTestVersion)
			if err == nil || !strings.Contains(err.Error(), readinessTestRunbook) {
				t.Fatalf("err = %v, want an attestation failure that points at %s", err, readinessTestRunbook)
			}
			if len(run.protocolRefs) != 0 {
				t.Fatalf("protocol refs = %v, want no release commit check", run.protocolRefs)
			}
		})
	}
}

// Verifies a gh call of the attestation verification that fails fails the check with gh's error,
// rather than letting the release through or reading as a release that is not published yet.
func TestCheckUnityPackageReleaseReadinessFailsWhenAnAttestationCallFails(t *testing.T) {
	repo := newReadinessTestRepo(t)
	for _, call := range []string{"commit", "download", "verify"} {
		t.Run(call, func(t *testing.T) {
			run := newReadinessTestRun(t)
			run.github.failures = map[string]error{call: errors.New("gh " + call + " failed: HTTP 503")}

			readiness, err := run.check(repo.root, readinessTestVersion)
			if err == nil || !strings.Contains(err.Error(), "gh "+call+" failed") {
				t.Fatalf("readiness = %+v, err = %v; want the failed gh %s call", readiness, err, call)
			}
			if len(run.protocolRefs) != 0 {
				t.Fatalf("protocol refs = %v, want no release commit check", run.protocolRefs)
			}
		})
	}
}

// Verifies a release commit whose protocol or pin check fails fails the readiness check, and that
// the pin is not checked once the protocol check has failed.
func TestCheckUnityPackageReleaseReadinessFailsWhenTheReleaseCommitFailsItsChecks(t *testing.T) {
	repo := newReadinessTestRepo(t)

	run := newReadinessTestRun(t)
	run.protocolErr = errors.New("minimum project runner release does not advertise protocol 9")
	if _, err := run.check(repo.root, readinessTestVersion); err == nil || !strings.Contains(err.Error(), "protocol 9") {
		t.Fatalf("err = %v, want the protocol check failure", err)
	}
	if len(run.pinRefs) != 0 {
		t.Fatalf("pin refs = %v, want no pin check after the protocol check failed", run.pinRefs)
	}

	run = newReadinessTestRun(t)
	run.pinErr = errors.New("pin records dispatcher-v2.9.0")
	if _, err := run.check(repo.root, readinessTestVersion); err == nil || !strings.Contains(err.Error(), "dispatcher-v2.9.0") {
		t.Fatalf("err = %v, want the pin check failure", err)
	}
}

// Verifies a manifest version that no release-please release commit records fails instead of
// naming another commit.
func TestCheckUnityPackageReleaseReadinessFailsWithoutAReleaseCommit(t *testing.T) {
	repo := newReadinessTestRepo(t)
	run := newReadinessTestRun(t)

	_, err := run.check(repo.root, "3.0.0-beta.7")
	if err == nil || !strings.Contains(err.Error(), "no release-please commit for Packages/src version 3.0.0-beta.7") {
		t.Fatalf("err = %v, want the missing release commit reported", err)
	}
	if len(run.protocolRefs) != 0 {
		t.Fatalf("protocol refs = %v, want no release commit check", run.protocolRefs)
	}
}

// Verifies a commit that only looks like the release commit is not taken for it: one with a
// release title whose manifest entry is under another package key, a later one with a release
// title that re-adds the manifest entry without the changelog heading, and a package-root move
// whose diff re-adds the manifest entry and the whole changelog under a title that is not a
// release title. Rename detection is off so a moved changelog shows as added lines on every git
// version.
func TestCheckUnityPackageReleaseReadinessSkipsLookalikeCommits(t *testing.T) {
	tests := []struct {
		name  string
		build func(t *testing.T, root string)
	}{
		{
			name: "release title with the manifest entry under another key",
			build: func(t *testing.T, root string) {
				writeReadinessReleaseFiles(t, root, readinessTestVersion, ".", "Packages/src/CHANGELOG.md")
				commitReadinessTestRepo(t, root, "chore: release v3-beta")
				writeReadinessReleaseFiles(t, root, readinessTestVersion, unityPackageManifestKey, "CHANGELOG.md")
				commitReadinessTestRepo(t, root, "chore: move unity-package release root")
			},
		},
		{
			name: "release title that re-adds only the manifest entry",
			build: func(t *testing.T, root string) {
				writeReadinessReleaseFiles(t, root, readinessTestVersion, ".", "Packages/src/CHANGELOG.md")
				commitReadinessTestRepo(t, root, "chore: release v3-beta")
				writeReadinessReleaseFiles(t, root, readinessTestVersion, unityPackageManifestKey, "CHANGELOG.md")
				commitReadinessTestRepo(t, root, "chore(main): release dispatcher 3.0.1")
			},
		},
		{
			name: "package-root move that re-adds the changelog",
			build: func(t *testing.T, root string) {
				runGitInRepo(t, root, "config", "diff.renames", "false")
				writeReadinessReleaseFiles(t, root, readinessTestVersion, ".", "CHANGELOG.md")
				if err := os.Rename(filepath.Join(root, "Packages", "src", "CHANGELOG.md"), filepath.Join(root, "CHANGELOG.md")); err != nil {
					t.Fatalf("move the changelog: %v", err)
				}
				commitReadinessTestRepo(t, root, "chore: release v3-beta")
				writeReadinessReleaseFiles(t, root, readinessTestVersion, unityPackageManifestKey, "CHANGELOG.md")
				if err := os.Remove(filepath.Join(root, "CHANGELOG.md")); err != nil {
					t.Fatalf("remove the moved changelog: %v", err)
				}
				commitReadinessTestRepo(t, root, "chore: move unity-package release root")
			},
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			root := t.TempDir()
			runGitInRepo(t, root, "init", "-q", "-b", "main")
			test.build(t, root)
			run := newReadinessTestRun(t)

			_, err := run.check(root, readinessTestVersion)
			if err == nil || !strings.Contains(err.Error(), "no release-please commit for Packages/src version "+readinessTestVersion) {
				t.Fatalf("err = %v, want no release commit found", err)
			}
		})
	}
}

// Verifies a commit history that cannot be read fails with git's own error instead of reading as a
// history without the release commit.
func TestCheckUnityPackageReleaseReadinessFailsOnAnUnreadableHistory(t *testing.T) {
	repo := newReadinessTestRepo(t)
	runGitInRepo(t, repo.root, "symbolic-ref", "HEAD", "refs/heads/missing")
	run := newReadinessTestRun(t)

	_, err := run.check(repo.root, readinessTestVersion)
	if err == nil || !strings.Contains(err.Error(), " log ") || strings.Contains(err.Error(), "no release-please commit") {
		t.Fatalf("err = %v, want the failed git log", err)
	}
	if len(run.protocolRefs) != 0 {
		t.Fatalf("protocol refs = %v, want no release commit check", run.protocolRefs)
	}
}

// Verifies a release-titled commit whose changes cannot be read fails the search with git's error,
// instead of being skipped for an older commit.
func TestCheckUnityPackageReleaseReadinessFailsOnAnUnreadableCandidate(t *testing.T) {
	repo := newReadinessTestRepo(t)
	tree := strings.TrimSpace(runGitInRepoOutput(t, repo.root, "rev-parse", repo.releaseCommit+"^{tree}"))
	objectPath := filepath.Join(repo.root, ".git", "objects", tree[:2], tree[2:])
	if err := os.Chmod(objectPath, 0o600); err != nil {
		t.Fatalf("make the tree object writable: %v", err)
	}
	if err := os.Remove(objectPath); err != nil {
		t.Fatalf("remove the tree object: %v", err)
	}
	run := newReadinessTestRun(t)

	_, err := run.check(repo.root, readinessTestVersion)
	if err == nil || !strings.Contains(err.Error(), " show ") || strings.Contains(err.Error(), "no release-please commit") {
		t.Fatalf("err = %v, want the failed git show", err)
	}
}

// readinessTestRepoWithFile builds the test repository with the file written, or removed when
// content is empty, either in the release commit or in the checkout after it.
func readinessTestRepoWithFile(t *testing.T, path string, content string, atReleaseCommit bool) readinessTestRepo {
	t.Helper()
	change := func(t *testing.T, root string) {
		if content != "" {
			writeReadinessTestFile(t, root, path, content)
			return
		}
		if err := os.Remove(filepath.Join(root, filepath.FromSlash(path))); err != nil {
			t.Fatalf("remove %s: %v", path, err)
		}
	}
	if atReleaseCommit {
		return newReadinessTestRepoWithRelease(t, change)
	}
	repo := newReadinessTestRepo(t)
	change(t, repo.root)
	return repo
}

// Verifies an unreadable or incomplete release input fails the check instead of reading as a
// release that is not due, or as a dependency that does not exist. The manifest and pin are read
// at the release commit and the config and changelog in the checkout. An unreadable pin above all
// must not read as "no dispatcher floor", which would skip the dispatcher release.
func TestCheckUnityPackageReleaseReadinessFailsOnUnreadableInputs(t *testing.T) {
	tests := []struct {
		name            string
		path            string
		content         string
		atReleaseCommit bool
		wantErr         string
	}{
		{name: "manifest that is not JSON", path: releasePleaseManifestRelativePath, content: "{\n  \"Packages/src\": \"3.0.0-beta.6\",\n}\n", atReleaseCommit: true, wantErr: releasePleaseManifestRelativePath},
		{name: "config that is not JSON", path: releasePleaseConfigRelativePath, content: "packages:\n", wantErr: releasePleaseConfigRelativePath},
		{name: "config without the package changelog path", path: releasePleaseConfigRelativePath, content: `{"packages": {"Packages/src": {}}}`, wantErr: "changelog-path"},
		{name: "runner component missing from its tag", path: releasePleaseConfigRelativePath, content: `{"packages": {"Packages/src": {"changelog-path": "CHANGELOG.md"}, "cli/project-runner": {"include-component-in-tag": true}}}`, wantErr: "without a component"},
		{name: "changelog that does not exist", path: "Packages/src/CHANGELOG.md", wantErr: "Packages/src/CHANGELOG.md"},
		{name: "pin that is not JSON", path: unityPackageCliPinFile, content: "minimumDispatcherVersion: 3.0.0\n", atReleaseCommit: true, wantErr: unityPackageCliPinFile},
		{name: "pin missing at the release commit", path: unityPackageCliPinFile, atReleaseCommit: true, wantErr: unityPackageCliPinFile},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			repo := readinessTestRepoWithFile(t, test.path, test.content, test.atReleaseCommit)
			run := newReadinessTestRun(t)

			_, err := run.check(repo.root, readinessTestVersion)
			if err == nil || !strings.Contains(err.Error(), test.wantErr) {
				t.Fatalf("err = %v, want a failure that names %q", err, test.wantErr)
			}
			if len(run.protocolRefs) != 0 {
				t.Fatalf("protocol refs = %v, want no release commit check", run.protocolRefs)
			}
		})
	}
}

// Verifies a release commit whose manifest has no runner version, or a config without the runner
// package, skips the runner release, and a release commit whose pin has no dispatcher floor skips
// the dispatcher release.
func TestCheckUnityPackageReleaseReadinessSkipsAbsentDependencies(t *testing.T) {
	tests := []struct {
		name            string
		path            string
		content         string
		atReleaseCommit bool
		skipped         string
		required        string
	}{
		{
			name:            "manifest without a runner version",
			path:            releasePleaseManifestRelativePath,
			content:         "{\n  \"Packages/src\": \"3.0.0-beta.6\",\n  \"cli/dispatcher\": \"3.0.0-beta.6\"\n}\n",
			atReleaseCommit: true,
			skipped:         readinessTestRunnerTag,
			required:        readinessTestDispatcherTag,
		},
		{
			name:     "config without the runner package",
			path:     releasePleaseConfigRelativePath,
			content:  `{"packages": {"Packages/src": {"changelog-path": "CHANGELOG.md"}}}`,
			skipped:  readinessTestRunnerTag,
			required: readinessTestDispatcherTag,
		},
		{
			name:            "pin without a dispatcher floor",
			path:            unityPackageCliPinFile,
			content:         `{"projectRunnerVersion": "3.0.0-beta.6"}`,
			atReleaseCommit: true,
			skipped:         readinessTestDispatcherTag,
			required:        readinessTestRunnerTag,
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			repo := readinessTestRepoWithFile(t, test.path, test.content, test.atReleaseCommit)
			run := newReadinessTestRun(t)

			readiness, err := run.check(repo.root, readinessTestVersion)
			if err != nil {
				t.Fatalf("check failed: %v", err)
			}
			if readiness.ReleaseCommit != repo.releaseCommit {
				t.Fatalf("readiness = %+v, want the release commit %s", readiness, repo.releaseCommit)
			}
			lookup := "gh api repos/" + unityPackageReleaseTestRepository + "/releases/tags/"
			if run.github.ran(lookup+test.skipped) || !run.github.ran(lookup+test.required) {
				t.Fatalf("commands = %v, want %s skipped and %s looked up", run.github.commands, test.skipped, test.required)
			}
		})
	}
}

func readinessTestListScript(assets ...string) string {
	return "#!/bin/sh\nset -eu\nif [ \"${1:-}\" = \"--list\" ]; then\n  printf '%s\\n' " + strings.Join(assets, " ") + "\n  exit 0\nfi\nexit 1\n"
}

// Verifies the asset list of a release comes from the verify script at that release's own tag, so
// a list that grew later is not demanded of an older release, that a release without a local tag
// gets the checkout's list, and that a list that fails is an error rather than an empty list.
func TestReleaseRequiredAssetsFollowTheReleaseGeneration(t *testing.T) {
	if _, err := exec.LookPath("sh"); err != nil {
		t.Skip("sh is not available")
	}
	root := t.TempDir()
	runGitInRepo(t, root, "init", "-q", "-b", "main")
	writeReadinessTestFile(t, root, dispatcherReleaseAssetsScript, readinessTestListScript("install.sh"))
	commitReadinessTestRepo(t, root, "fix: require the installer")
	runGitInRepo(t, root, "tag", readinessTestDispatcherTag)
	writeReadinessTestFile(t, root, dispatcherReleaseAssetsScript, readinessTestListScript("install.sh", "install.sh.sha256"))
	commitReadinessTestRepo(t, root, "fix: require installer checksums")

	tests := []struct {
		tag  string
		want []string
	}{
		{tag: readinessTestDispatcherTag, want: []string{"install.sh"}},
		{tag: "dispatcher-v3.1.0", want: []string{"install.sh", "install.sh.sha256"}},
	}
	for _, test := range tests {
		assets, err := releaseRequiredAssets(context.Background(), root, dispatcherReleaseAssetsScript, test.tag)
		if err != nil {
			t.Fatalf("assets of %s: %v", test.tag, err)
		}
		if strings.Join(assets, ",") != strings.Join(test.want, ",") {
			t.Fatalf("assets of %s = %v, want %v", test.tag, assets, test.want)
		}
	}

	writeReadinessTestFile(t, root, dispatcherReleaseAssetsScript, "#!/bin/sh\nexit 1\n")
	if assets, err := releaseRequiredAssets(context.Background(), root, dispatcherReleaseAssetsScript, "dispatcher-v3.1.0"); err == nil {
		t.Fatalf("assets = %v, want the failed --list reported", assets)
	}
}

// Verifies release tags are fetched from origin over local tags of the same name, so a tag that
// moved on origin is read at its new commit, and that a checkout without origin is left as it is.
func TestFetchReleaseTagsOverwritesLocalTagsFromOrigin(t *testing.T) {
	origin := t.TempDir()
	runGitInRepo(t, origin, "init", "-q", "-b", "main")
	writeReadinessTestFile(t, origin, "release.txt", "origin\n")
	commitReadinessTestRepo(t, origin, "chore: release on origin")
	runGitInRepo(t, origin, "tag", readinessTestDispatcherTag)
	originCommit := strings.TrimSpace(runGitInRepoOutput(t, origin, "rev-parse", "HEAD"))

	local := t.TempDir()
	runGitInRepo(t, local, "init", "-q", "-b", "main")
	writeReadinessTestFile(t, local, "release.txt", "local\n")
	commitReadinessTestRepo(t, local, "chore: release locally")
	runGitInRepo(t, local, "tag", readinessTestDispatcherTag)
	localCommit := strings.TrimSpace(runGitInRepoOutput(t, local, "rev-parse", "HEAD"))

	if err := fetchReleaseTags(context.Background(), local, "+refs/tags/*:refs/tags/*"); err != nil {
		t.Fatalf("fetch without origin: %v", err)
	}
	if tagged := strings.TrimSpace(runGitInRepoOutput(t, local, "rev-parse", readinessTestDispatcherTag+"^{commit}")); tagged != localCommit {
		t.Fatalf("tag after a fetch without origin = %s, want the local commit %s", tagged, localCommit)
	}

	runGitInRepo(t, local, "remote", "add", "origin", origin)
	if err := fetchReleaseTags(context.Background(), local, "+refs/tags/*:refs/tags/*"); err != nil {
		t.Fatalf("fetch from origin: %v", err)
	}
	if tagged := strings.TrimSpace(runGitInRepoOutput(t, local, "rev-parse", readinessTestDispatcherTag+"^{commit}")); tagged != originCommit {
		t.Fatalf("tag after the fetch = %s, want origin's commit %s", tagged, originCommit)
	}
}

// Verifies the readiness check the plan runs checks the release commit with the real protocol
// check: a release commit without the protocol constants fails it, naming the file at that commit. The release depends on
// no runner or dispatcher release here, so nothing is looked up on GitHub.
func TestRunUnityPackageReleaseReadinessCheckChecksTheReleaseCommit(t *testing.T) {
	repo := newReadinessTestRepoWithRelease(t, func(t *testing.T, root string) {
		writeReadinessTestFile(t, root, unityPackageCliPinFile, `{"projectRunnerVersion": "3.0.0-beta.6"}`)
	})
	writeReadinessTestFile(t, repo.root, releasePleaseConfigRelativePath, `{"packages": {"Packages/src": {"changelog-path": "CHANGELOG.md"}}}`)
	var log bytes.Buffer

	readiness, err := runUnityPackageReleaseReadinessCheck(context.Background(), &log, unityPackageReleaseReadinessInput{
		Repository: unityPackageReleaseTestRepository,
		RepoRoot:   repo.root,
		Version:    readinessTestVersion,
	})
	if err == nil || !strings.Contains(err.Error(), repo.releaseCommit+":"+protocolMinimumVersionFile) {
		t.Fatalf("readiness = %+v, err = %v; want the protocol check to fail reading %s at the release commit %s", readiness, err, protocolMinimumVersionFile, repo.releaseCommit)
	}
}

// Verifies the calls the plan's readiness check makes are the real ones: the pin check refuses a
// release commit whose pin names another dispatcher than its manifest and accepts a matching one,
// the runner tag is fetched from origin over the local tag, the asset list comes from the verify
// script, commands run for real, and the check reports into the log it was given.
func TestNewUnityPackageReleaseReadinessDepsUseTheRealChecks(t *testing.T) {
	if _, err := exec.LookPath("sh"); err != nil {
		t.Skip("sh is not available")
	}
	ctx := context.Background()
	origin := t.TempDir()
	runGitInRepo(t, origin, "init", "-q", "-b", "main")
	writeReadinessTestFile(t, origin, "release.txt", "origin\n")
	commitReadinessTestRepo(t, origin, "chore: release on origin")
	runGitInRepo(t, origin, "tag", readinessTestRunnerTag)
	originCommit := strings.TrimSpace(runGitInRepoOutput(t, origin, "rev-parse", "HEAD"))

	root := t.TempDir()
	runGitInRepo(t, root, "init", "-q", "-b", "main")
	writeReadinessTestFile(t, root, releasePleaseManifestRelativePath, `{"cli/dispatcher": "3.0.1"}`)
	writeReadinessTestFile(t, root, unityPackageCliPinFile, `{"dispatcherReleaseTag": "dispatcher-v3.0.0"}`)
	writeReadinessTestFile(t, root, dispatcherReleaseAssetsScript, readinessTestListScript("install.sh", "install.sh.sha256"))
	commitReadinessTestRepo(t, root, "chore: release with a stale pin")
	runGitInRepo(t, root, "tag", readinessTestRunnerTag)
	staleCommit := strings.TrimSpace(runGitInRepoOutput(t, root, "rev-parse", "HEAD"))
	writeReadinessTestFile(t, root, unityPackageCliPinFile, `{"dispatcherReleaseTag": "dispatcher-v3.0.1"}`)
	commitReadinessTestRepo(t, root, "chore: stamp the pin")
	stampedCommit := strings.TrimSpace(runGitInRepoOutput(t, root, "rev-parse", "HEAD"))
	runGitInRepo(t, root, "remote", "add", "origin", origin)
	var log bytes.Buffer

	deps := newUnityPackageReleaseReadinessDeps(root, &log)

	if _, err := deps.verifyPin(ctx, root, staleCommit); err == nil || !strings.Contains(err.Error(), "pin dispatcher-v3.0.0") {
		t.Fatalf("pin check of the stale commit: err = %v, want the mismatch", err)
	}
	if message, err := deps.verifyPin(ctx, root, stampedCommit); err != nil || !strings.Contains(message, "dispatcher-v3.0.1") {
		t.Fatalf("pin check of the stamped commit = %q, %v; want it to pass", message, err)
	}
	if err := deps.fetchReleaseTag(ctx, readinessTestRunnerTag); err != nil {
		t.Fatalf("fetch the runner tag: %v", err)
	}
	if tagged := strings.TrimSpace(runGitInRepoOutput(t, root, "rev-parse", readinessTestRunnerTag+"^{commit}")); tagged != originCommit {
		t.Fatalf("runner tag after the fetch = %s, want origin's commit %s", tagged, originCommit)
	}
	assets, err := deps.requiredAssets(ctx, root, dispatcherReleaseAssetsScript, "dispatcher-v3.0.1")
	if err != nil || strings.Join(assets, ",") != "install.sh,install.sh.sha256" {
		t.Fatalf("assets = %v, %v; want the verify script's list", assets, err)
	}
	if head, err := deps.runOutput(ctx, "git", "-C", root, "rev-parse", "HEAD"); err != nil || strings.TrimSpace(head) != stampedCommit {
		t.Fatalf("run git rev-parse = %q, %v; want the commit %s", head, err, stampedCommit)
	}
	writeReadinessLine(deps.log, "reported")
	if log.String() != "reported\n" {
		t.Fatalf("log = %q, want what the check reported", log.String())
	}
}
