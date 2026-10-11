package automation

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

const runnerReleaseRecoveryRunbook = "docs/release-recovery-runbook.md"

// runnerAttestationVerification is the part of one `gh attestation verify --format json` result
// the check reads: the commit the signing workflow ran at.
type runnerAttestationVerification struct {
	VerificationResult struct {
		Signature struct {
			Certificate struct {
				SourceRepositoryDigest string `json:"sourceRepositoryDigest"`
			} `json:"certificate"`
		} `json:"signature"`
	} `json:"verificationResult"`
}

// verifyRunnerReleaseAttestations requires every asset of the project runner release to carry an
// attestation of the native CLI publish workflow made at the commit of the release tag. The
// dispatcher refuses to download a release whose attestations name another commit, and a published
// release cannot be repaired, so a mismatch fails the check and points at the recovery runbook
// instead of being waited out.
func verifyRunnerReleaseAttestations(ctx context.Context, deps unityPackageReleaseReadinessDeps, input unityPackageReleaseReadinessInput, tag string) error {
	tagCommit, err := deps.runOutput(ctx, "gh", "api", "repos/"+input.Repository+"/commits/"+tag, "--jq", ".sha")
	if err != nil {
		return err
	}
	tagCommit = strings.TrimSpace(strings.ReplaceAll(tagCommit, "\r", ""))
	if tagCommit == "" {
		return invalidRunnerAttestation(tag)
	}
	assets, err := deps.requiredAssets(ctx, input.RepoRoot, projectRunnerReleaseAssetsScript, tag)
	if err != nil || len(assets) == 0 {
		return invalidRunnerAttestation(tag)
	}

	directory, err := os.MkdirTemp("", "runner-release-attestations-")
	if err != nil {
		return err
	}
	defer func() { _ = os.RemoveAll(directory) }()
	for _, asset := range assets {
		if err := verifyRunnerAssetAttestation(ctx, deps, input.Repository, tag, tagCommit, filepath.Join(directory, asset), asset); err != nil {
			return err
		}
	}
	return nil
}

func verifyRunnerAssetAttestation(ctx context.Context, deps unityPackageReleaseReadinessDeps, repository string, tag string, tagCommit string, directory string, asset string) error {
	if err := os.Mkdir(directory, 0o700); err != nil {
		return err
	}
	bundle := asset + ".sigstore.json"
	if _, err := deps.runOutput(ctx, "gh", "release", "download", tag, "--repo", repository, "--pattern", asset, "--pattern", bundle, "--dir", directory); err != nil {
		return err
	}
	assetPath := filepath.Join(directory, asset)
	bundlePath := filepath.Join(directory, bundle)
	if !runnerReleaseFileHasContent(assetPath) || !runnerReleaseFileHasContent(bundlePath) {
		return invalidRunnerAttestation(asset)
	}

	signerWorkflow := repository + "/.github/workflows/native-cli-publish.yml"
	output, err := deps.runOutput(ctx, "gh", "attestation", "verify", assetPath, "--repo", repository, "--bundle", bundlePath, "--signer-workflow", signerWorkflow, "--format", "json")
	if err != nil {
		return err
	}
	var verifications []runnerAttestationVerification
	if err := json.Unmarshal([]byte(output), &verifications); err != nil || len(verifications) == 0 {
		return invalidRunnerAttestation(asset)
	}
	for _, verification := range verifications {
		if verification.VerificationResult.Signature.Certificate.SourceRepositoryDigest != tagCommit {
			return invalidRunnerAttestation(asset)
		}
	}
	return nil
}

func runnerReleaseFileHasContent(path string) bool {
	info, err := os.Stat(path)
	return err == nil && info.Size() > 0
}

func invalidRunnerAttestation(subject string) error {
	return fmt.Errorf("project runner release attestation is invalid for %s; see %s for recovery steps", subject, runnerReleaseRecoveryRunbook)
}
