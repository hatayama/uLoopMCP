using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Test execution service
    /// Single function: Execute tests using Unity Test Runner
    /// Related classes: PlayModeTestExecuter, RunTestsUseCase, RunTestsTool
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - Application Service Layer (Single Function Implementation)
    /// </summary>
    public class TestExecutionService
    {
        /// <summary>
        /// Execute tests in PlayMode
        /// </summary>
        /// <param name="filter">Test execution filter</param>
        /// <param name="saveXml">XML save flag</param>
        /// <returns>Test execution result</returns>
        public async Task<SerializableTestResult> ExecutePlayModeTestAsync(TestExecutionFilter filter, bool saveXml)
        {
            return await PlayModeTestExecuter.ExecutePlayModeTest(filter, saveXml);
        }

        /// <summary>
        /// Execute tests in EditMode
        /// </summary>
        /// <param name="filter">Test execution filter</param>
        /// <param name="saveXml">XML save flag</param>
        /// <returns>Test execution result</returns>
        public async Task<SerializableTestResult> ExecuteEditModeTestAsync(TestExecutionFilter filter, bool saveXml)
        {
            return await PlayModeTestExecuter.ExecuteEditModeTest(filter, saveXml);
        }
    }
}