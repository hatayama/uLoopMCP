using System.ComponentModel;
using UnityEditor.TestTools.TestRunner.Api;

namespace io.github.hatayama.uMCP
{
    /// <summary>
    /// Supported test filter types
    /// </summary>
    public enum TestFilterType
    {
        all = 0,
        exact = 1,
        regex = 2,
        assembly = 3
    }

    /// <summary>
    /// Schema for RunTests command parameters
    /// Provides type-safe parameter access with default values
    /// </summary>
    public class RunTestsSchema : BaseToolSchema
    {
        /// <summary>
        /// Test mode - EditMode(0), PlayMode(1)
        /// </summary>
        [Description("Test mode - EditMode(0), PlayMode(1)")]
        public TestMode TestMode { get; set; } = TestMode.EditMode;

        /// <summary>
        /// Type of test filter - all(0), exact(1), regex(2), assembly(3)
        /// </summary>
        [Description("Type of test filter - all(0), exact(1), regex(2), assembly(3)")]
        public TestFilterType FilterType { get; set; } = TestFilterType.all;

        /// <summary>
        /// Filter value (specify when filterType is not all)
        /// • exact: Individual test method name (e.g.: io.github.hatayama.uMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs)
        /// • regex: Class name or namespace (e.g.: io.github.hatayama.uMCP.ConsoleLogRetrieverTests, io.github.hatayama.uMCP)
        /// • assembly: Assembly name (e.g.: uMCP.Tests.Editor)
        /// </summary>
        [Description("Filter value (specify when filterType is not all)\n• exact: Individual test method name (e.g.: io.github.hatayama.uMCP.ConsoleLogRetrieverTests.GetAllLogs_WithMaskAllOff_StillReturnsAllLogs)\n• regex: Class name or namespace (e.g.: io.github.hatayama.uMCP.ConsoleLogRetrieverTests, io.github.hatayama.uMCP)\n• assembly: Assembly name (e.g.: uMCP.Tests.Editor)")]
        public string FilterValue { get; set; } = "";

        /// <summary>
        /// Whether to save test results as XML file
        /// </summary>
        [Description("Whether to save test results as XML file. Test results are saved to external files to avoid massive token consumption when returning results directly. Please read the file if you need detailed test results.")]
        public bool SaveXml { get; set; } = false;

        /// <summary>
        /// Timeout for test execution in seconds (default: 60 seconds for EditMode, 120 seconds for PlayMode)
        /// </summary>
        [Description("Timeout for test execution in seconds (default: 30 seconds)")]
        public override int TimeoutSeconds { get; set; } = 30;
    }
} 