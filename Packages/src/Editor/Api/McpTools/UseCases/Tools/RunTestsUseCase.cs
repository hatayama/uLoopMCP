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
            
            try
            {
                if (parameters.TestMode == TestMode.PlayMode)
                {
                    // TODO: Add cancellationToken parameter when TestExecutionService supports it
                    // result = await executionService.ExecutePlayModeTestAsync(filter, parameters.SaveXml, cancellationToken);
                    result = await executionService.ExecutePlayModeTestAsync(filter, parameters.SaveXml);
                }
                else
                {
                    // TODO: Add cancellationToken parameter when TestExecutionService supports it
                    // result = await executionService.ExecuteEditModeTestAsync(filter, parameters.SaveXml, cancellationToken);
                    result = await executionService.ExecuteEditModeTestAsync(filter, parameters.SaveXml);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Propagate cancellation exceptions
                throw;
            }
            catch (System.Exception ex)
            {
                // Log full exception details for debugging
                UnityEngine.Debug.LogError($"Test execution failed: {ex}");
                VibeLogger.LogError(
                    "test_execution_failed", 
                    "Test execution encountered an error", 
                    new { testMode = parameters.TestMode, filterType = parameters.FilterType, filterValue = parameters.FilterValue, error = ex.Message }
                );
                
                // Create a minimal error result
                throw new System.InvalidOperationException("Test execution failed. Please check the logs for details.", ex);
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