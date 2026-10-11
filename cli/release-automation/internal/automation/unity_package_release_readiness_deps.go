package automation

import (
	"context"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
)

// runUnityPackageReleaseReadinessCheck runs the readiness check against the checkout at repoRoot,
// GitHub, and the release scripts. Every release tag is fetched first, because the asset list and
// the protocol contract of a release are read from the release's own tag.
func runUnityPackageReleaseReadinessCheck(ctx context.Context, log io.Writer, input unityPackageReleaseReadinessInput) (unityPackageReleaseReadiness, error) {
	if err := fetchReleaseTags(ctx, input.RepoRoot, "+refs/tags/*:refs/tags/*"); err != nil {
		return unityPackageReleaseReadiness{}, err
	}
	deps := unityPackageReleaseReadinessDeps{
		runOutput:      runCommandOutput,
		requiredAssets: releaseRequiredAssets,
		// The runner release may have been published after the fetch above.
		fetchReleaseTag: func(ctx context.Context, tag string) error {
			return fetchReleaseTags(ctx, input.RepoRoot, "refs/tags/"+tag+":refs/tags/"+tag)
		},
		verifyProtocol: checkMinimumCliReleaseProtocol,
		verifyPin: func(ctx context.Context, repoRoot string, ref string) (string, error) {
			return checkPackagePinConsistency(ctx, packagePinConsistencyConfig{repoRoot: repoRoot, ref: ref})
		},
		log: log,
	}
	return checkUnityPackageReleaseReadiness(ctx, deps, input)
}

// fetchReleaseTags fetches the refspec from origin, overwriting local tags, and does nothing in a
// checkout without origin.
func fetchReleaseTags(ctx context.Context, repoRoot string, refspec string) error {
	if _, err := runCommandOutput(ctx, "git", "-C", repoRoot, "remote", "get-url", "origin"); err != nil {
		return nil
	}
	_, err := runCommandOutput(ctx, "git", "-C", repoRoot, "fetch", "--force", "origin", refspec)
	return err
}

// releaseRequiredAssets lists the assets a release of the tag must carry, by running the verify
// script with --list. Required asset lists grow over time, and a published release only ships the
// assets its own generation required, so the script is taken from the release's tag. A release
// without a local tag is being cut from the checkout right now, so the checkout's script is the
// right one for it.
func releaseRequiredAssets(ctx context.Context, repoRoot string, verifyScript string, tag string) ([]string, error) {
	content, found, err := gitFileAtRef(ctx, repoRoot, "refs/tags/"+tag, verifyScript)
	if err != nil {
		return nil, err
	}
	scriptPath := filepath.Join(repoRoot, filepath.FromSlash(verifyScript))
	if found {
		scriptPath, err = writeTaggedVerifyScript(content)
		if err != nil {
			return nil, err
		}
		defer func() { _ = os.Remove(scriptPath) }()
	}
	output, err := runCommandOutput(ctx, "sh", scriptPath, "--list")
	if err != nil {
		return nil, fmt.Errorf("list the assets of %s: %w", tag, err)
	}
	return strings.Fields(output), nil
}

func writeTaggedVerifyScript(content []byte) (string, error) {
	file, err := os.CreateTemp("", "release-assets-*.sh")
	if err != nil {
		return "", err
	}
	_, writeErr := file.Write(content)
	closeErr := file.Close()
	if writeErr != nil || closeErr != nil {
		_ = os.Remove(file.Name())
		return "", fmt.Errorf("write the tagged verify script: %w", errors.Join(writeErr, closeErr))
	}
	return file.Name(), nil
}
