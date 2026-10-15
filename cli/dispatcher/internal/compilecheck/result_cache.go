package compilecheck

import (
	"context"
	"crypto/sha256"
	"encoding/hex"
	"fmt"
	"io"
	"os"
	"path/filepath"

	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck/resultstore"
)

// reuseOrCompile reports what csc would say about one unit, replaying the previous run's answer when
// every input that decides it is still exactly as that run read it, and compiling otherwise.
func (c Compiler) reuseOrCompile(
	ctx context.Context, plan BuildPlan, unit CompileUnit,
) (UnitResult, error) {
	records := c.records(plan)
	// Why an unreadable input is not an error here: the compile that follows reads the same files and
	// reports what is wrong with them in its own way. All this loses is the chance to reuse.
	keyResult, keyErr := computeInputKey(c.ProjectRoot, c.Paths, plan, unit)
	key := keyResult.Key
	if keyErr != nil {
		key = ""
	}
	// Why --all only skips the lookup and still records: it is the way out of a reuse that got
	// something wrong, and the ordinary run after it should still be able to reuse what it compiled.
	if key != "" && !plan.All {
		if recorded, found := records.Reusable(unit.Assembly.AssemblyName, key); found {
			return UnitResult{
				Assembly:    unit.Assembly.AssemblyName,
				Diagnostics: recorded.Diagnostics,
				Succeeded:   recorded.Succeeded,
				Reused:      true,
			}, nil
		}
	}

	// Why a fast check that was used touches nothing on disk: the outputs, the manifest and the
	// baseline still describe the last full compile, which is what the next fast check compares against.
	nonSourceKey := c.nonSourceKeyBeforeCompile(plan, unit, key)
	fallbackReason := ""
	if c.FastPath.shouldTry(plan, unit, key) {
		attempt := c.FastPath.Try(ctx, c, plan, unit, keyResult, nonSourceKey)
		if attempt.Err != nil || attempt.Used {
			return attempt.Result, attempt.Err
		}
		fallbackReason = attempt.Reason
	}

	// Why nothing is recorded unless csc ran to completion: a compile that was cancelled or never
	// started says nothing about the inputs, and CompileUnit has already removed the manifest of the
	// compile this one replaces, so an interrupted run leaves no record at all rather than a stale one.
	result, err := c.CompileUnit(ctx, plan, unit)
	result.FastPathFallbackReason = fallbackReason
	if err != nil || key == "" || result.RawOutput != "" {
		return result, err
	}
	// Why the key is read again before recording: csc reads the sources while it compiles, so an edit
	// made during the compile would otherwise be recorded under the key of the bytes before it, and
	// putting the file back would replay what the edited file reported. CompileUnit has already
	// removed the previous record, so leaving this one out makes the next run compile again.
	if c.keyStillHolds(plan, unit, key) {
		if writeErr := records.RecordCompile(unit.Assembly.AssemblyName, keyResult.identity(),
			resultstore.CompileResult[Diagnostic]{Succeeded: result.Succeeded, Diagnostics: result.Diagnostics}, unitOutputNames(unit)); writeErr != nil {
			return UnitResult{}, writeErr
		}
	}

	// Why --full records too: running --full before finishing would otherwise drop the baseline, and
	// the first check after the next edit would always compile in full.
	// Why a failure to record does not fail the run: the compile itself succeeded, and all it costs
	// is a full compile next time.
	if c.FastPath.shouldRecord(unit, result) {
		if recordErr := c.FastPath.RecordBaseline(ctx, c, plan, unit, keyResult, nonSourceKey); recordErr != nil {
			result.FastPathNote = "baseline not recorded: " + recordErr.Error()
		}
	}

	return result, nil
}

// recordMayStillApply reports whether the unit's recorded compile may be replayed once the
// references named in unknown finish compiling. unit is shaped the way it would start now, reading
// Unity's reference assembly for each of them; what it reads for them is not compared.
// Why the project references are compared before the shape: the shape reads every source of the
// unit, and in the common case where a reference that already finished changed again, the
// references alone answer no.
// Why --all and an unreadable input answer no: --all never replays a record, and a unit whose key
// cannot be computed cannot replay one either, so waiting for either would gain nothing.
func (c Compiler) recordMayStillApply(plan BuildPlan, unit CompileUnit, unknown map[string]bool) bool {
	if plan.All {
		return false
	}
	projectReferences, err := computeProjectReferences(c.ProjectRoot, plan, unit)
	if err != nil {
		return false
	}

	// Why the sources are hashed inside the function only: the store asks for the shape last, and
	// most answers of no come from the references before it.
	return c.records(plan).MayStillApply(unit.Assembly.AssemblyName, projectReferences, unknown,
		func() (string, error) {
			keyResult, keyErr := computeInputKey(c.ProjectRoot, c.Paths, plan, unit)

			return keyResult.Shape, keyErr
		})
}

// records opens the store of what earlier runs of the check recorded in the plan's output directory.
func (c Compiler) records(plan BuildPlan) resultstore.Store[Diagnostic] {
	return resultstore.Open[Diagnostic](c.ProjectRoot, plan.OutputDir)
}

// unitOutputNames lists the files a compile of the unit writes in the output directory, by name,
// leaving out an output the response file names no path for.
func unitOutputNames(unit CompileUnit) []string {
	names := []string{}
	for _, path := range []string{unit.Assembly.OutputPath, unit.Assembly.RefOutputPath} {
		if path == "" {
			continue
		}
		names = append(names, filepath.Base(path))
	}

	return names
}

// nonSourceKeyBeforeCompile reads a unit's non-source key for the changed-files check, or an empty
// one when there is no check or no key.
// Why it is read before anything runs: a baseline must describe the inputs the compile saw, and
// reading it after the compile would let a change made during it in unchecked.
func (c Compiler) nonSourceKeyBeforeCompile(plan BuildPlan, unit CompileUnit, key string) nonSourceIdentity {
	if c.FastPath == nil || key == "" {
		return nonSourceIdentity{}
	}
	nonSourceKey, err := computeNonSourceKey(c.ProjectRoot, c.Paths, plan, unit)
	if err != nil {
		return nonSourceIdentity{}
	}

	return nonSourceKey
}

// hashFile reports what a file holds, as a hash.
func hashFile(path string) (string, error) {
	file, err := os.Open(path)
	if err != nil {
		return "", err
	}
	defer func() { _ = file.Close() }()

	digest := sha256.New()
	if _, err := io.Copy(digest, file); err != nil {
		return "", fmt.Errorf("failed to read %s: %w", path, err)
	}

	return hex.EncodeToString(digest.Sum(nil)), nil
}
