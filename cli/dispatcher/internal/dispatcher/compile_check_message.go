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
		count := len(response.BlockedAssemblies)
		message += fmt.Sprintf(compileCheckPlural(count,
			" %d assembly was not compiled because an assembly it references has errors.",
			" %d assemblies were not compiled because an assembly they reference has errors."), count)
	}
	if len(response.FastCheckedAssemblies) > 0 {
		count := len(response.FastCheckedAssemblies)
		message += fmt.Sprintf(compileCheckPlural(count,
			" %d assembly was checked on its changed files only;",
			" %d assemblies were checked on their changed files only;"), count) +
			" run with --full before finishing to cover assembly-wide rules."
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
			parts = append(parts, fmt.Sprintf("%s%s %d %s",
				strings.ToUpper(entry.verb[:1]), entry.verb[1:], entry.count,
				compileCheckPlural(entry.count, "assembly", "assemblies")))
			continue
		}
		parts = append(parts, fmt.Sprintf("%s %d", entry.verb, entry.count))
	}
	if len(parts) == 0 {
		return compileCheckNoChangeMessage
	}

	return fmt.Sprintf("%s with %d %s and %d %s.",
		joinCompileCheckSummaryParts(parts),
		response.ErrorCount, compileCheckPlural(response.ErrorCount, "error", "errors"),
		response.WarningCount, compileCheckPlural(response.WarningCount, "warning", "warnings"))
}

// compileCheckPlural picks the wording for count, which is singular only for exactly one; zero
// takes the plural ("0 errors").
func compileCheckPlural(count int, singular string, plural string) string {
	if count == 1 {
		return singular
	}

	return plural
}

// joinCompileCheckSummaryParts joins the summary parts as "A and b" or "A, b and c".
func joinCompileCheckSummaryParts(parts []string) string {
	if len(parts) == 1 {
		return parts[0]
	}

	return strings.Join(parts[:len(parts)-1], ", ") + " and " + parts[len(parts)-1]
}
