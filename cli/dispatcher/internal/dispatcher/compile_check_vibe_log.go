package dispatcher

import (
	"time"

	"github.com/hatayama/unity-cli-loop/common/clicore"
	clierrors "github.com/hatayama/unity-cli-loop/common/errors"
	"github.com/hatayama/unity-cli-loop/common/vibelog"
	"github.com/hatayama/unity-cli-loop/dispatcher/internal/compilecheck"
)

const (
	compileCheckVibeStartOperation    = "cli_compile_check_start"
	compileCheckVibeCompleteOperation = "cli_compile_check_complete"
)

// compileCheckVibeLog writes what one compile-check run did to the CLI VibeLog: a start entry, and a
// complete entry under the same correlation id on every way out of the run.
// Why compile-check needs its own entries: it never talks to the Editor, so nothing on the Editor
// side records which runs compiled, which were checked on their changed files, and why.
type compileCheckVibeLog struct {
	enabled       bool
	projectRoot   string
	correlationID string
	startedAt     time.Time
}

// startCompileCheckVibeLog writes the start entry of a run and returns what writes its complete
// entry. With ULOOP_DEBUG unset it writes nothing and builds no context.
func startCompileCheckVibeLog(projectRoot string, options compileCheckOptions) compileCheckVibeLog {
	if !vibelog.IsCLIVibeLogEnabled() {
		return compileCheckVibeLog{}
	}

	logRun := compileCheckVibeLog{
		enabled:       true,
		projectRoot:   projectRoot,
		correlationID: vibelog.NewCLIVibeCorrelationID(),
		startedAt:     time.Now(),
	}
	logRun.write("INFO", compileCheckVibeStartOperation, "Starting compile-check.", map[string]any{
		"command":          clicore.CompileCheckCommandName,
		"all":              options.all,
		"full":             options.full,
		"jobs":             options.jobs,
		"project_identity": vibelog.ProjectIdentity(projectRoot),
	})

	return logRun
}

// failed writes the complete entry of a run that stopped before it had a response to print.
func (logRun compileCheckVibeLog) failed(err error) {
	if !logRun.enabled {
		return
	}

	context := logRun.completeContext("failed")
	context["error"] = clicore.ErrorMessage(err)
	context["error_code"] = clierrors.ClassifyError(err, clierrors.ErrorContext{
		Command: clicore.CompileCheckCommandName, ProjectRoot: logRun.projectRoot,
	}).ErrorCode
	logRun.write("ERROR", compileCheckVibeCompleteOperation, "compile-check failed.", context)
}

// completed writes the complete entry of a run that produced a response, with the response's own
// lists so the log and the printed JSON can be read side by side.
func (logRun compileCheckVibeLog) completed(response compileCheckResponse, result compilecheck.Result) {
	if !logRun.enabled {
		return
	}

	level, outcome := "INFO", "succeeded"
	if response.ErrorCount > 0 {
		level, outcome = "WARNING", "errors"
	}
	context := logRun.completeContext(outcome)
	context["error_count"] = response.ErrorCount
	context["warning_count"] = response.WarningCount
	context["compiled"] = response.CompiledAssemblies
	context["reused"] = response.ReusedAssemblies
	context["fast_checked"] = response.FastCheckedAssemblies
	context["blocked"] = response.BlockedAssemblies
	context["skipped_count"] = response.SkippedAssemblies
	context["fast_checks"] = compileCheckVibeFastChecks(response.FastChecks)
	context["fallbacks"] = compileCheckVibeReasons(response.FastPathFallbacks)
	context["baseline_notes"] = compileCheckVibeReasons(response.FastPathBaselineNotes)
	context["units"] = compileCheckVibeUnits(result.Units)
	logRun.write(level, compileCheckVibeCompleteOperation, "compile-check finished.", context)
}

// completeContext holds the fields every complete entry carries.
func (logRun compileCheckVibeLog) completeContext(outcome string) map[string]any {
	return map[string]any{
		"command":     clicore.CompileCheckCommandName,
		"outcome":     outcome,
		"duration_ms": time.Since(logRun.startedAt).Milliseconds(),
	}
}

// write appends one entry of this run.
// Why a failed write is ignored: the log is a diagnostic aid, and a run must not fail for it.
func (logRun compileCheckVibeLog) write(level string, operation string, message string, context map[string]any) {
	_ = vibelog.WriteCLIVibeLog(logRun.projectRoot, vibelog.CLIVibeLogEntry{
		Level:         level,
		Operation:     operation,
		Message:       message,
		Context:       context,
		CorrelationID: logRun.correlationID,
	})
}

// compileCheckVibeFastChecks lists each fast check by assembly, whether it was replayed, and how
// many files it covered.
func compileCheckVibeFastChecks(checks []compileCheckFastCheck) []map[string]any {
	entries := make([]map[string]any, 0, len(checks))
	for _, check := range checks {
		entries = append(entries, map[string]any{
			"assembly":           check.Assembly,
			"reused":             check.Reused,
			"changed_file_count": len(check.ChangedFiles),
		})
	}

	return entries
}

// compileCheckVibeReasons lists one reason per assembly.
func compileCheckVibeReasons(reasons []compileCheckFastPathFallback) []map[string]any {
	entries := make([]map[string]any, 0, len(reasons))
	for _, reason := range reasons {
		entries = append(entries, map[string]any{"assembly": reason.Assembly, "reason": reason.Reason})
	}

	return entries
}

// compileCheckVibeUnits lists how long each unit this run compiled or checked took.
// Why replayed units are left out: they took no work, and their durations would only hide which
// units the run spent its time on.
func compileCheckVibeUnits(units []compilecheck.UnitResult) []map[string]any {
	entries := make([]map[string]any, 0, len(units))
	for _, unit := range units {
		if unit.Reused || unit.FastCheckReused {
			continue
		}
		entries = append(entries, map[string]any{
			"assembly":    unit.Assembly,
			"duration_ms": unit.Duration.Milliseconds(),
			"succeeded":   unit.Succeeded,
		})
	}

	return entries
}
