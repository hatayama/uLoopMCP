using System.Threading.Tasks;
using System.Threading;
using UnityEditor.TestTools.TestRunner.Api;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Handles temporal cohesion for test execution processing
    /// Processing sequence: 1. Test filter creation, 2. Test execution, 3. Result processing
    /// Related classes: RunTestsTool, TestFilterCreationService, TestExecutionService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class RunTestsUseCase : AbstractUseCase<RunTestsSchema, RunTestsResponse>
    {
        /// <summary>
        /// Executes test execution processing
        /// </summary>
        /// <param name="parameters">Test execution parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>Test execution result</returns>
        public override async Task<RunTestsResponse> ExecuteAsync(RunTestsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. Test filter creation
            TestExecutionFilter filter = null;
            if (parameters.FilterType != TestFilterType.all)
            {
                TestFilterCreationService filterService = new();
                filter = filterService.CreateFilter(parameters.FilterType, parameters.FilterValue);
            }
            
            // 2. Test execution
            cancellationToken.ThrowIfCancellationRequested();
            TestExecutionService executionService = new();
            SerializableTestResult result;
            
            if (parameters.TestMode == TestMode.PlayMode)
            {
                result = await executionService.ExecutePlayModeTestAsync(filter, parameters.SaveXml);
            }
            else
            {
                result = await executionService.ExecuteEditModeTestAsync(filter, parameters.SaveXml);
            }
            
            // 3. Response creation
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
    }
}