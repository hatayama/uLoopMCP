package automation

import (
	"context"
	"encoding/json"
	"fmt"
	"strings"
)

type publishedRelease struct {
	TagName string                  `json:"tag_name"`
	Draft   bool                    `json:"draft"`
	Assets  []publishedReleaseAsset `json:"assets"`
}

type publishedReleaseAsset struct {
	Name string `json:"name"`
	Size int64  `json:"size"`
}

// lookupPublishedRelease returns the published release of the tag, which is the one installers and
// OpenUPM read. GitHub answers this lookup with 404 for a missing tag and for a draft alike, and
// both mean the release is not out yet. Any other failure is returned, because treating an outage
// as "no release yet" would create a second release, leave the first unsigned, or wait on a release
// that is already out.
func lookupPublishedRelease(ctx context.Context, runOutput func(context.Context, string, ...string) (string, error), repository string, tag string) (publishedRelease, bool, error) {
	output, err := runOutput(ctx, "gh", "api", "repos/"+repository+"/releases/tags/"+tag)
	if err != nil {
		if strings.Contains(err.Error(), "HTTP 404") {
			return publishedRelease{}, false, nil
		}
		return publishedRelease{}, false, err
	}
	var release publishedRelease
	if err := json.Unmarshal([]byte(output), &release); err != nil {
		return publishedRelease{}, false, fmt.Errorf("parse release %s: %w", tag, err)
	}
	if release.TagName != tag || release.Draft {
		return publishedRelease{}, false, fmt.Errorf("release lookup for %s answered with tag %q (draft=%t), not the published release", tag, release.TagName, release.Draft)
	}
	return release, true, nil
}

// hasNonEmptyAsset ignores a zero-byte asset, which an interrupted upload can leave behind and
// which nothing could install.
func (release publishedRelease) hasNonEmptyAsset(name string) bool {
	for _, asset := range release.Assets {
		if asset.Name == name && asset.Size > 0 {
			return true
		}
	}
	return false
}
