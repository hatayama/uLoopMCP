#if ULOOPMCP_HAS_ROSLYN
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
	/// <summary>
	/// Integration tests ensuring DynamicCodeExecutor executes snippets that include
	/// top-level statements and string literals containing C# keywords.
	/// </summary>
	public class ExecuteDynamicCodeTopLevelStatementsAstIntegrationTests
	{
		[Test]
		public async Task Executor_WrapsTopLevelStatements_AndExecutes_WithKeywordsInStrings()
		{
			IDynamicCodeExecutor executor = Factory.DynamicCodeExecutorFactory.Create(
				DynamicCodeSecurityLevel.Restricted
			);

			ExecutionResult result = await executor.ExecuteCodeAsync(
				"string s = \"this mentions class and namespace but is just text\"; return s;",
				"DynamicCommand",
				null,
				CancellationToken.None,
				false
			);

			Assert.IsTrue(result.Success, $"Execution failed: {result.ErrorMessage}");
			Assert.AreEqual("this mentions class and namespace but is just text", result.Result);
		}
	}
}
#endif
