package automation

import (
	"context"
	"errors"
	"slices"
	"strings"
	"testing"
	"time"
)

// Verifies which titles count as release-please release titles: plain and scoped "release" titles match, while other prefixes and a scope that never closes do not.
func TestReleasePleaseReleaseTitleMatches(t *testing.T) {
	cases := map[string]bool{
		"chore: release":                  true,
		"chore: release 3.6.0":            true,
		"chore(main): release dispatcher": true,
		"chore(main): release":            true,
		"chore(main): releases":           false,
		"chore(main release 3.6.0":        false,
		"feat: release 3.6.0":             false,
		"feat(main): release 3.6.0":       false,
	}
	for title, want := range cases {
		if got := releasePleaseReleaseTitleMatches(title); got != want {
			t.Fatalf("releasePleaseReleaseTitleMatches(%q) = %t, want %t", title, got, want)
		}
	}
}

// Verifies a matching release pull request without a head SHA fails the lookup instead of being checked against an empty commit.
func TestFindReleasePRCheckPullRequestsRejectsMissingHeadSHA(t *testing.T) {
	deps := releasePRCheckDeps{runOutput: func(context.Context, string, ...string) (string, error) {
		return `[{"number":7,"headRefName":"release-please--branches--main","headRefOid":"","title":"chore(main): release 3.6.0"}]`, nil
	}}

	_, err := findReleasePRCheckPullRequests(context.Background(), releasePRCheckConfig{repository: "owner/repository", targetBranch: "main"}, deps)

	if err == nil || !strings.Contains(err.Error(), "release PR #7 has no head SHA") {
		t.Fatalf("expected a missing head SHA error, got %v", err)
	}
}

// Verifies an interrupted wait between lookups fails with the sleep error instead of reporting that no release pull request exists.
func TestFindReleasePRCheckPullRequestsWithRetryStopsOnInterruptedWait(t *testing.T) {
	calls := 0
	deps := releasePRCheckDeps{
		runOutput: func(context.Context, string, ...string) (string, error) {
			calls++
			return "[]", nil
		},
		sleep: func(context.Context, time.Duration) error { return errors.New("lookup wait interrupted") },
	}
	config := releasePRCheckConfig{repository: "owner/repository", targetBranch: "main", lookupAttempts: 3, lookupIntervalSeconds: 1}

	_, err := findReleasePRCheckPullRequestsWithRetry(context.Background(), config, deps)

	if err == nil || err.Error() != "lookup wait interrupted" || calls != 1 {
		t.Fatalf("expected the wait error after one lookup, got %v after %d lookups", err, calls)
	}
}

// Verifies the Unity package release pull request is moved to the front while every other pull request keeps its listed order, including the combined branch and a listing without a package pull request.
func TestOrderReleasePRsForChecks(t *testing.T) {
	const prefix = "release-please--branches--main--components--"
	runner := releasePullRequest{Number: 1, HeadRefName: prefix + "uloop-project-runner"}
	dispatcher := releasePullRequest{Number: 2, HeadRefName: prefix + "dispatcher"}
	unityPackage := releasePullRequest{Number: 3, HeadRefName: prefix + "unity-package"}
	combined := releasePullRequest{Number: 4, HeadRefName: "release-please--branches--main"}
	cases := []struct {
		name   string
		listed []releasePullRequest
		want   []int
	}{
		{name: "package listed last", listed: []releasePullRequest{runner, dispatcher, unityPackage}, want: []int{3, 1, 2}},
		{name: "package already first", listed: []releasePullRequest{unityPackage, dispatcher}, want: []int{3, 2}},
		{name: "package only", listed: []releasePullRequest{unityPackage}, want: []int{3}},
		{name: "no package", listed: []releasePullRequest{runner, dispatcher}, want: []int{1, 2}},
		{name: "combined branch is not the package", listed: []releasePullRequest{combined, unityPackage}, want: []int{3, 4}},
	}
	for _, testCase := range cases {
		ordered := orderReleasePRsForChecks(testCase.listed, "main")
		got := []int{}
		for _, releasePR := range ordered {
			got = append(got, releasePR.Number)
		}
		if !slices.Equal(got, testCase.want) {
			t.Fatalf("%s: order = %v, want %v", testCase.name, got, testCase.want)
		}
	}
}
