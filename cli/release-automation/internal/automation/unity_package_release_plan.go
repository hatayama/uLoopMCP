package automation

import (
	"context"
	"errors"
	"flag"
	"fmt"
	"io"
	"os"
)

const planUnityPackageReleaseCommandName = "plan-unity-package-release"

// unityPackageReleasePlanOptions selects the repository and the root holding the release-please
// manifest, whose Unity package version is the release that is planned. DryRun signs without
// publishing, and also signs a release that is already published, because it is how a new UPM CLI
// pin is validated once every release is signed.
type unityPackageReleasePlanOptions struct {
	Repository string
	RepoRoot   string
	DryRun     bool
}

type unityPackageReleasePlanDeps struct {
	runOutput    func(context.Context, string, ...string) (string, error)
	checkRelease func(ctx context.Context, log io.Writer, version string) (unityPackageReleaseReadiness, error)
}

// unityPackageReleasePlan says whether the release is signed and whether it is then created.
// SourceRef is what the signing job checks out: the release commit of a release that is due, or
// the tag of a published release. Annotation is the workflow command level ("notice" or
// "warning") the reason is reported with, or empty for a plain log line.
type unityPackageReleasePlan struct {
	Sign       bool
	Publish    bool
	Tag        string
	Version    string
	AssetName  string
	SourceRef  string
	Reason     string
	Annotation string
}

// RunPlanUnityPackageRelease prints GITHUB_OUTPUT lines (sign, publish, tag, version, asset-name,
// source-ref) that tell the Unity package release workflow whether to sign the manifest version of
// the package and create its release. A published release is immutable, so the release is created
// only once the signed tarball exists, and only when the readiness check names its release commit.
func RunPlanUnityPackageRelease(ctx context.Context, stdout io.Writer, stderr io.Writer, args []string) int {
	options, err := parseUnityPackageReleasePlanOptions(ctx, args, os.LookupEnv)
	if err != nil {
		_, _ = fmt.Fprintln(stderr, planUnityPackageReleaseCommandName+":", err)
		return 2
	}
	deps := unityPackageReleasePlanDeps{
		runOutput: runCommandOutput,
		checkRelease: func(ctx context.Context, log io.Writer, version string) (unityPackageReleaseReadiness, error) {
			return runUnityPackageReleaseReadinessCheck(ctx, log, unityPackageReleaseReadinessInput{
				Repository: options.Repository,
				RepoRoot:   options.RepoRoot,
				Version:    version,
			})
		},
	}
	return runPlanUnityPackageReleaseWithDeps(ctx, stdout, stderr, options, deps)
}

func parseUnityPackageReleasePlanOptions(ctx context.Context, args []string, lookupEnv func(string) (string, bool)) (unityPackageReleasePlanOptions, error) {
	flags := flag.NewFlagSet(planUnityPackageReleaseCommandName, flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	repository := flags.String("repo", "", "owner/name of the repository; defaults to GITHUB_REPOSITORY")
	repoRoot := flags.String("repo-root", "", "repository root holding the release-please manifest (default: the current git repository root)")
	dryRun := flags.Bool("dry-run", false, "sign without creating the release, and sign a published release too")
	if err := flags.Parse(args); err != nil {
		return unityPackageReleasePlanOptions{}, err
	}
	// Omitting the flag would mean "not a dry run", which creates an immutable release, so the
	// caller has to say which it wants rather than inherit that from a default.
	if !unityPackageReleasePlanFlagSet(flags, "dry-run") {
		return unityPackageReleasePlanOptions{}, errors.New("--dry-run=true or --dry-run=false is required")
	}

	if *repository == "" {
		*repository, _ = lookupEnv("GITHUB_REPOSITORY")
	}
	if *repository == "" {
		return unityPackageReleasePlanOptions{}, errors.New("--repo or GITHUB_REPOSITORY is required")
	}
	if *repoRoot == "" {
		resolvedRoot, err := gitRepoRoot(ctx)
		if err != nil {
			return unityPackageReleasePlanOptions{}, fmt.Errorf("failed to resolve the repository root: %w", err)
		}
		*repoRoot = resolvedRoot
	}
	return unityPackageReleasePlanOptions{Repository: *repository, RepoRoot: *repoRoot, DryRun: *dryRun}, nil
}

func unityPackageReleasePlanFlagSet(flags *flag.FlagSet, name string) bool {
	set := false
	flags.Visit(func(visited *flag.Flag) {
		if visited.Name == name {
			set = true
		}
	})
	return set
}

func runPlanUnityPackageReleaseWithDeps(ctx context.Context, stdout io.Writer, stderr io.Writer, options unityPackageReleasePlanOptions, deps unityPackageReleasePlanDeps) int {
	// What the readiness check reports goes to stderr, the job log, because stdout is GITHUB_OUTPUT.
	plan, err := planUnityPackageRelease(ctx, stderr, options, deps)
	if err != nil {
		_, _ = fmt.Fprintln(stderr, planUnityPackageReleaseCommandName+":", err)
		return 1
	}
	// stdout is redirected into GITHUB_OUTPUT, so the annotation goes to stderr, which the runner
	// scans for workflow commands as well.
	if plan.Annotation != "" {
		_, _ = fmt.Fprintln(stderr, "::"+plan.Annotation+"::"+plan.Reason)
	} else {
		_, _ = fmt.Fprintln(stderr, plan.Reason)
	}
	_, _ = fmt.Fprintf(stdout, "sign=%t\npublish=%t\ntag=%s\nversion=%s\nasset-name=%s\nsource-ref=%s\n",
		plan.Sign, plan.Publish, plan.Tag, plan.Version, plan.AssetName, plan.SourceRef)
	return 0
}

func planUnityPackageRelease(ctx context.Context, log io.Writer, options unityPackageReleasePlanOptions, deps unityPackageReleasePlanDeps) (unityPackageReleasePlan, error) {
	version, err := readUnityPackageManifestVersion(options.RepoRoot)
	if err != nil {
		return unityPackageReleasePlan{}, err
	}
	tag := "v" + version
	if !unityPackageReleaseTagPattern.MatchString(tag) {
		return unityPackageReleasePlan{}, fmt.Errorf("manifest version %q does not make a Unity package release tag (expected <major>.<minor>.<patch>)", version)
	}
	plan := unityPackageReleasePlan{Tag: tag, Version: version, AssetName: signedUnityPackageAssetName(version)}

	release, published, err := lookupPublishedRelease(ctx, deps.runOutput, options.Repository, tag)
	if err != nil {
		return unityPackageReleasePlan{}, err
	}
	if published {
		return planPublishedUnityPackageRelease(plan, release, options.DryRun), nil
	}
	return planDueUnityPackageRelease(ctx, log, plan, options.DryRun, deps)
}

// planPublishedUnityPackageRelease never creates anything: a published release is immutable. A
// missing tarball is reported as a warning, not a failure, because no run can attach it any more,
// and failing would repeat on every release workflow run until the next version is released.
func planPublishedUnityPackageRelease(plan unityPackageReleasePlan, release publishedRelease, dryRun bool) unityPackageReleasePlan {
	plan.SourceRef = "refs/tags/" + plan.Tag
	switch {
	case dryRun:
		plan.Sign = true
		plan.Reason = fmt.Sprintf("Dry run: signing published release %s from its tag without changing it.", plan.Tag)
	case release.hasNonEmptyAsset(plan.AssetName):
		plan.Reason = fmt.Sprintf("Release %s is already published with %s.", plan.Tag, plan.AssetName)
	default:
		plan.Annotation = "warning"
		plan.Reason = fmt.Sprintf("Release %s was published without %s; a published release is immutable, so it stays unsigned.", plan.Tag, plan.AssetName)
	}
	return plan
}

// planDueUnityPackageRelease asks the readiness check whether the release can be created now. A
// release that waits for another release is skipped with a notice that names it; the publish
// workflow of that release starts this workflow again when it completes.
func planDueUnityPackageRelease(ctx context.Context, log io.Writer, plan unityPackageReleasePlan, dryRun bool, deps unityPackageReleasePlanDeps) (unityPackageReleasePlan, error) {
	readiness, err := deps.checkRelease(ctx, log, plan.Version)
	if err != nil {
		return unityPackageReleasePlan{}, fmt.Errorf("readiness check for release %s failed: %w", plan.Tag, err)
	}
	if (readiness.ReleaseCommit == "") == (readiness.WaitingFor == "") {
		return unityPackageReleasePlan{}, fmt.Errorf("readiness check for release %s answered %+v, not either a release commit or what it waits for", plan.Tag, readiness)
	}
	if readiness.WaitingFor != "" {
		plan.Annotation = "notice"
		plan.Reason = readiness.WaitingFor
		return plan, nil
	}
	commit := readiness.ReleaseCommit
	if !releaseCommitPattern.MatchString(commit) {
		return unityPackageReleasePlan{}, fmt.Errorf("readiness check for release %s named %q, not a release commit SHA", plan.Tag, commit)
	}
	plan.Sign = true
	plan.Publish = !dryRun
	plan.SourceRef = commit
	if dryRun {
		plan.Reason = fmt.Sprintf("Dry run: signing release %s from release commit %s without creating it.", plan.Tag, commit)
		return plan, nil
	}
	plan.Reason = fmt.Sprintf("Release %s is due; signing release commit %s, then creating the release with %s.", plan.Tag, commit, plan.AssetName)
	return plan, nil
}
