package dispatcher

import (
	"fmt"
	"strings"
)

// compileCheckMessage states what the run did in one line.
// Why the blocked assemblies are added rather than folded in: without them a run that reports one
// error and hundreds of assemblies it never got to reads as a run that lost them.
// Why the fast-checked ones get a reminder: the check skipped rules only a full compile runs, and
// the caller has to know to run --full before finishing.
func compileCheckMessage(response compileCheckResponse) string {
	message := compileCheckRunSummary(response)
	if len(response.BlockedAssemblies) > 0 {
		message += fmt.Sprintf(
			" %d assemblies were not compiled because an assembly they reference has errors.",
			len(response.BlockedAssemblies))
	}
	if len(response.FastCheckedAssemblies) > 0 {
		message += fmt.Sprintf(
			" %d assemblies were checked on their changed files only; run with --full before finishing to cover assembly-wide rules.",
			len(response.FastCheckedAssemblies))
	}

	return message
}

// compileCheckRunSummary states what the run compiled, reused, and fast-checked.
// Why only the non-zero counts are named: a run that reused nothing reads exactly as it did before
// reuse existed, and a run that compiled nothing should not open by saying so.
func compileCheckRunSummary(response compileCheckResponse) string {
	counts := []struct {
		verb  string
		count int
	}{
		{"compiled", len(response.CompiledAssemblies)},
		{"reused", len(response.ReusedAssemblies)},
		{"fast-checked", len(response.FastCheckedAssemblies)},
	}
	parts := []string{}
	for _, entry := range counts {
		if entry.count == 0 {
			continue
		}
		if len(parts) == 0 {
			parts = append(parts, fmt.Sprintf("%s%s %d assemblies",
				strings.ToUpper(entry.verb[:1]), entry.verb[1:], entry.count))
			continue
		}
		parts = append(parts, fmt.Sprintf("%s %d", entry.verb, entry.count))
	}
	if len(parts) == 0 {
		return compileCheckNoChangeMessage
	}

	return fmt.Sprintf("%s with %d errors and %d warnings.",
		joinCompileCheckSummaryParts(parts), response.ErrorCount, response.WarningCount)
}

// joinCompileCheckSummaryParts joins the summary parts as "A and b" or "A, b and c".
func joinCompileCheckSummaryParts(parts []string) string {
	if len(parts) == 1 {
		return parts[0]
	}

	return strings.Join(parts[:len(parts)-1], ", ") + " and " + parts[len(parts)-1]
}
