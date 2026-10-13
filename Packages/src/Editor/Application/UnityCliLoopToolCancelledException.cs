using System;

namespace io.github.hatayama.UnityCliLoop.Application
{
    /// <summary>
    /// Reports that uloop cancel stopped the tool a request was running, so the protocol layer can
    /// answer the waiting CLI with a tool_cancelled error instead of dropping the connection.
    /// Why not an OperationCanceledException: the request pipeline treats that as a disconnect and
    /// writes no response.
    /// </summary>
    public sealed class UnityCliLoopToolCancelledException : Exception
    {
        public UnityCliLoopToolCancelledException(string toolName)
            : base(CreateMessage(toolName))
        {
            ToolName = toolName;
        }

        public string ToolName { get; }

        private static string CreateMessage(string toolName)
        {
            return $"'{toolName}' was cancelled by uloop cancel.";
        }
    }
}
