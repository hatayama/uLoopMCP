package automation

import (
	"context"
	"fmt"
	"strings"
)

// findUnityPackageReleaseCommit returns the newest release-please release commit on HEAD that
// records the version of the Unity package. Only a commit with a release-please release title
// qualifies, and it must add both the package's manifest entry and the version's changelog heading:
// a commit that moves the changelog re-adds every changelog line in a diff limited to that path,
// and one that renames the package key re-adds its manifest line at an unchanged version, so
// either alone could impersonate the release commit.
func findUnityPackageReleaseCommit(ctx context.Context, repoRoot string, version string, changelogPath string) (string, error) {
	history, err := runCommandOutput(ctx, "git", "-C", repoRoot, "log", "--format=%H%x09%s", "HEAD")
	if err != nil {
		return "", err
	}
	for _, line := range strings.Split(strings.ReplaceAll(history, "\r", ""), "\n") {
		commit, subject, found := strings.Cut(line, "\t")
		if !found || !releasePleaseReleaseTitleMatches(subject) {
			continue
		}
		records, err := commitRecordsUnityPackageVersion(ctx, repoRoot, commit, version, changelogPath)
		if err != nil {
			return "", err
		}
		if records {
			return commit, nil
		}
	}
	return "", fmt.Errorf("no release-please commit for %s version %s was found", unityPackageManifestKey, version)
}

// commitRecordsUnityPackageVersion reads the commit's own changes to the manifest and the
// changelog. A commit that cannot be read fails the search rather than being skipped, because the
// history it came from was readable a moment ago.
func commitRecordsUnityPackageVersion(ctx context.Context, repoRoot string, commit string, version string, changelogPath string) (bool, error) {
	diff, err := runCommandOutput(ctx, "git", "-C", repoRoot, "show", "--format=", commit, "--", releasePleaseManifestRelativePath, changelogPath)
	if err != nil {
		return false, err
	}
	manifestEntry := fmt.Sprintf("%q: %q", unityPackageManifestKey, version)
	changelogHeading := unityPackageChangelogHeadingStart + version + "]"
	manifestFound := false
	changelogFound := false
	for _, line := range strings.Split(diff, "\n") {
		if !strings.HasPrefix(line, "+") {
			continue
		}
		manifestFound = manifestFound || strings.Contains(line, manifestEntry)
		changelogFound = changelogFound || strings.Contains(line, changelogHeading)
	}
	return manifestFound && changelogFound, nil
}
