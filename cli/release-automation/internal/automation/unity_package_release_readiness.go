package automation

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"path/filepath"
)

const (
	projectRunnerManifestKey         = "cli/project-runner"
	projectRunnerReleaseAssetsScript = "scripts/verify-native-cli-release-assets.sh"
	dispatcherReleaseAssetsScript    = "scripts/verify-dispatcher-release-assets.sh"
)

// unityPackageReleaseReadiness is the answer of the readiness check: the release commit of a
// release that can be created now, or what a release that cannot be created yet waits for.
// Exactly one of the two is set.
type unityPackageReleaseReadiness struct {
	ReleaseCommit string
	WaitingFor    string
}

type unityPackageReleaseReadinessInput struct {
	Repository string
	RepoRoot   string
	Version    string
}

// unityPackageReleaseReadinessDeps are the calls the readiness check makes outside the repository
// checkout. requiredAssets lists the assets a release of the tag must carry; verifyProtocol and
// verifyPin check a release commit and return what they confirmed. Everything the check reports
// along the way goes to log.
type unityPackageReleaseReadinessDeps struct {
	runOutput       func(context.Context, string, ...string) (string, error)
	requiredAssets  func(ctx context.Context, repoRoot string, verifyScript string, tag string) ([]string, error)
	fetchReleaseTag func(ctx context.Context, tag string) error
	verifyProtocol  func(ctx context.Context, repoRoot string, ref string) (string, error)
	verifyPin       func(ctx context.Context, repoRoot string, ref string) (string, error)
	log             io.Writer
}

// unityPackageReleaseInputs is what the checkout says the release depends on. An empty tag means
// the release does not depend on that component.
type unityPackageReleaseInputs struct {
	changelogPath string
	runnerTag     string
	dispatcherTag string
}

// checkUnityPackageReleaseReadiness says whether the Unity package release of the version can be
// created now. It can once the project runner release of the manifest and the dispatcher release
// of the pin's minimumDispatcherVersion are published with every asset, the runner's attestations
// bind each asset to the commit of its tag, and the release-please release commit of the version
// passes the protocol and pin checks. A release it depends on that is not published yet is waited
// for, because the publish workflow of that release starts the release workflow again. Anything
// else that is wrong fails the check, because none of it resolves by waiting.
func checkUnityPackageReleaseReadiness(ctx context.Context, deps unityPackageReleaseReadinessDeps, input unityPackageReleaseReadinessInput) (unityPackageReleaseReadiness, error) {
	inputs, err := readUnityPackageReleaseInputs(input.RepoRoot)
	if err != nil {
		return unityPackageReleaseReadiness{}, err
	}

	if inputs.runnerTag != "" {
		ready, err := dependencyReleaseIsReady(ctx, deps, input, inputs.runnerTag, projectRunnerReleaseAssetsScript)
		if err != nil {
			return unityPackageReleaseReadiness{}, err
		}
		if !ready {
			return waitForDependencyRelease("Project runner", inputs.runnerTag, input.Version), nil
		}
		if err := deps.fetchReleaseTag(ctx, inputs.runnerTag); err != nil {
			return unityPackageReleaseReadiness{}, err
		}
		if err := verifyRunnerReleaseAttestations(ctx, deps, input, inputs.runnerTag); err != nil {
			return unityPackageReleaseReadiness{}, err
		}
	}

	if inputs.dispatcherTag != "" {
		ready, err := dependencyReleaseIsReady(ctx, deps, input, inputs.dispatcherTag, dispatcherReleaseAssetsScript)
		if err != nil {
			return unityPackageReleaseReadiness{}, err
		}
		if !ready {
			return waitForDependencyRelease("Dispatcher", inputs.dispatcherTag, input.Version), nil
		}
	}

	releaseCommit, err := findUnityPackageReleaseCommit(ctx, input.RepoRoot, input.Version, inputs.changelogPath)
	if err != nil {
		return unityPackageReleaseReadiness{}, err
	}
	if err := checkUnityPackageReleaseCommit(ctx, deps, input.RepoRoot, releaseCommit); err != nil {
		return unityPackageReleaseReadiness{}, err
	}
	writeReadinessLine(deps.log, fmt.Sprintf("Unity package release v%s can be created from release commit %s.", input.Version, releaseCommit))
	return unityPackageReleaseReadiness{ReleaseCommit: releaseCommit}, nil
}

func waitForDependencyRelease(component string, tag string, version string) unityPackageReleaseReadiness {
	return unityPackageReleaseReadiness{WaitingFor: fmt.Sprintf(
		"%s release %s is not published with complete assets yet; the Unity package release v%s waits for it.",
		component, tag, version)}
}

// checkUnityPackageReleaseCommit runs the protocol check before the pin check. The release commit
// is what the release is tagged at, so its pin is the dispatcher every fresh install of the release
// fetches, and a commit that fails either check is refused rather than waited out: its files never
// change.
func checkUnityPackageReleaseCommit(ctx context.Context, deps unityPackageReleaseReadinessDeps, repoRoot string, releaseCommit string) error {
	message, err := deps.verifyProtocol(ctx, repoRoot, releaseCommit)
	if err != nil {
		return err
	}
	writeReadinessLine(deps.log, message)
	message, err = deps.verifyPin(ctx, repoRoot, releaseCommit)
	if err != nil {
		return err
	}
	writeReadinessLine(deps.log, message)
	return nil
}

// dependencyReleaseIsReady says whether the release of the tag is published with every asset its
// own generation requires. A release lookup that fails for another reason than "not published" is
// returned as an error, because an outage must not read as "not published yet". So is an asset
// list that cannot be read or names nothing: it comes from a script that waiting does not change,
// and an empty list would pass a release without its assets.
func dependencyReleaseIsReady(ctx context.Context, deps unityPackageReleaseReadinessDeps, input unityPackageReleaseReadinessInput, tag string, verifyScript string) (bool, error) {
	release, published, err := lookupPublishedRelease(ctx, deps.runOutput, input.Repository, tag)
	if err != nil || !published {
		return false, err
	}
	assets, err := deps.requiredAssets(ctx, input.RepoRoot, verifyScript, tag)
	if err != nil {
		return false, fmt.Errorf("%s --list for release %s failed: %w", verifyScript, tag, err)
	}
	if len(assets) == 0 {
		return false, fmt.Errorf("%s --list named no asset for release %s", verifyScript, tag)
	}
	for _, asset := range assets {
		if !release.hasNonEmptyAsset(asset) {
			return false, nil
		}
	}
	return true, nil
}

// readUnityPackageReleaseInputs reads the release-please config, manifest, and package pin of the
// checkout. An unreadable file fails the check: read as "no value", it would skip a release the
// package depends on.
func readUnityPackageReleaseInputs(repoRoot string) (unityPackageReleaseInputs, error) {
	var config releasePleaseConfig
	if err := readReadinessJSON(repoRoot, releasePleaseConfigRelativePath, &config); err != nil {
		return unityPackageReleaseInputs{}, err
	}
	var manifest map[string]string
	if err := readReadinessJSON(repoRoot, releasePleaseManifestRelativePath, &manifest); err != nil {
		return unityPackageReleaseInputs{}, err
	}
	var pin dispatcherMinimumVersionCliPin
	if err := readReadinessJSON(repoRoot, unityPackageCliPinFile, &pin); err != nil {
		return unityPackageReleaseInputs{}, err
	}

	changelogPath, err := unityPackageChangelogPath(repoRoot, config)
	if err != nil {
		return unityPackageReleaseInputs{}, err
	}
	inputs := unityPackageReleaseInputs{changelogPath: changelogPath}
	runnerConfig, runnerConfigured := config.Packages[projectRunnerManifestKey]
	if runnerVersion := manifest[projectRunnerManifestKey]; runnerVersion != "" && runnerConfigured {
		inputs.runnerTag, err = releasePleaseReleaseTag(projectRunnerManifestKey, runnerConfig, runnerVersion)
		if err != nil {
			return unityPackageReleaseInputs{}, err
		}
	}
	if pin.MinimumDispatcherVersion != "" {
		inputs.dispatcherTag = dispatcherReleaseTagPrefix + pin.MinimumDispatcherVersion
	}
	return inputs, nil
}

func unityPackageChangelogPath(repoRoot string, config releasePleaseConfig) (string, error) {
	configuredPath := config.Packages[unityPackageManifestKey].ChangelogPath
	if configuredPath == "" {
		return "", fmt.Errorf("release-please package %s has no changelog-path", unityPackageManifestKey)
	}
	changelogPath := releasePleaseChangelogPath(unityPackageManifestKey, configuredPath)
	if _, err := os.Stat(filepath.Join(repoRoot, filepath.FromSlash(changelogPath))); err != nil {
		return "", fmt.Errorf("missing changelog for release-please package %s: %s: %w", unityPackageManifestKey, changelogPath, err)
	}
	return changelogPath, nil
}

func readReadinessJSON(repoRoot string, relativePath string, value any) error {
	content, err := os.ReadFile(filepath.Join(repoRoot, filepath.FromSlash(relativePath)))
	if err != nil {
		return fmt.Errorf("read %s: %w", relativePath, err)
	}
	if err := json.Unmarshal(content, value); err != nil {
		return fmt.Errorf("parse %s: %w", relativePath, err)
	}
	return nil
}

func writeReadinessLine(writer io.Writer, line string) {
	// The job log is the only reader, and nothing can be done once writing to it fails.
	_, _ = fmt.Fprintln(writer, line)
}
