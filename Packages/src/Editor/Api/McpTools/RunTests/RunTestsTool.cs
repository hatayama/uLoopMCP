using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.TestTools.TestRunner.Api;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Test execution tool handler - Type-safe implementation using Schema and Response
    /// Executes tests using Unity Test Runner and returns the results
    /// </summary>
    [McpTool(
        RequiredSecuritySetting = SecuritySettings.EnableTestsExecution,
        Description = "Execute Unity Test Runner with advanced filtering options - exact test methods, regex patterns for classes/namespaces, assembly filtering"
    )]
    public class RunTestsTool : AbstractUnityTool<RunTestsSchema, RunTestsResponse>
    {
        public override string ToolName => "run-tests";

        protected override async Task<RunTestsResponse> ExecuteAsync(RunTestsSchema parameters, CancellationToken cancellationToken)
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();
            
            // Create filter if specified
            TestExecutionFilter filter = null;
            if (parameters.FilterType != TestFilterType.all)
            {
                filter = CreateFilter(parameters.FilterType.ToString(), parameters.FilterValue);
            }

            // Check for cancellation before test execution
            cancellationToken.ThrowIfCancellationRequested();
            
            // Execute tests using appropriate method
            SerializableTestResult result;
            if (parameters.TestMode == TestMode.PlayMode)
            {
                result = await PlayModeTestExecuter.ExecutePlayModeTest(
                    filter, 
                    parameters.SaveXml);
            }
            else
            {
                result = await PlayModeTestExecuter.ExecuteEditModeTest(
                    filter, 
                    parameters.SaveXml);
            }

            return new RunTestsResponse(
                success: result.success,
                message: result.message,
                completedAt: result.completedAt,
                testCount: result.testCount,
                passedCount: result.passedCount,
                failedCount: result.failedCount,
                skippedCount: result.skippedCount,
                xmlPath: result.xmlPath
            );
        }

        /// <summary>
        /// Create test execution filter
        /// </summary>
        private TestExecutionFilter CreateFilter(string filterType, string filterValue)
        {
            return filterType.ToLower() switch
            {
                "all" => TestExecutionFilter.All(), // Run all tests
                "exact" => TestExecutionFilter.ByTestName(filterValue), // Individual test method (exact match)
                "regex" => TestExecutionFilter.ByClassName(filterValue), // Class name or namespace (regex pattern)
                "assembly" => TestExecutionFilter.ByAssemblyName(filterValue), // Assembly name
                _ => throw new ArgumentException($"Unsupported filter type: {filterType}")
            };
        }
    }
}