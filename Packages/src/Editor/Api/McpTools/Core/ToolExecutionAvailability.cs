using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    internal static class ToolExecutionAvailability
    {
        internal static bool ShouldReportDependencyUnavailableBeforeDisabled(string toolName)
        {
            return ShouldReportDependencyUnavailableBeforeDisabled(
                toolName,
                isTestFrameworkAvailable: IsTestFrameworkAvailable);
        }

        internal static bool ShouldReportDependencyUnavailableBeforeDisabled(
            string toolName,
            bool isTestFrameworkAvailable)
        {
            Debug.Assert(!string.IsNullOrEmpty(toolName), "toolName must not be null or empty");

            return toolName == McpConstants.TOOL_NAME_RUN_TESTS && !isTestFrameworkAvailable;
        }

        private static bool IsTestFrameworkAvailable
        {
            get
            {
#if ULOOPMCP_HAS_TEST_FRAMEWORK
                return true;
#else
                return false;
#endif
            }
        }
    }
}
