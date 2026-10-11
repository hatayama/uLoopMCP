package automation

import (
	"path"
	"strings"
)

const releasePleaseConfigRelativePath = "release-please-config.json"

// releasePleaseConfig is the part of release-please-config.json the release automation reads:
// how each package names its release tag and where its changelog lives.
type releasePleaseConfig struct {
	Packages map[string]releasePleasePackageConfig `json:"packages"`
}

type releasePleasePackageConfig struct {
	Component             string `json:"component"`
	IncludeComponentInTag bool   `json:"include-component-in-tag"`
	IncludeVInTag         bool   `json:"include-v-in-tag"`
	ChangelogPath         string `json:"changelog-path"`
}

// releasePleaseChangelogPath returns the repository-relative path of a package changelog.
// release-please resolves a changelog path that starts with "/" from the repository root and any
// other from the package directory.
func releasePleaseChangelogPath(packagePath string, changelogPath string) string {
	if strings.HasPrefix(changelogPath, "/") {
		return strings.TrimPrefix(changelogPath, "/")
	}
	return path.Join(packagePath, changelogPath)
}
