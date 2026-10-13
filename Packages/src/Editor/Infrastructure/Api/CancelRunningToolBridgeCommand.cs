using System;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.Runtime;

namespace io.github.hatayama.UnityCliLoop.Infrastructure
{
    /// <summary>
    /// Runs the cancel-running-tool command behind uloop cancel: fires the cancellation of the tool
    /// that holds the execution slot and reports what it found.
    /// </summary>
    internal static class CancelRunningToolBridgeCommand
    {
        // Why no main-thread switch: this must answer while a tool blocks the main thread. The
        // cancellation has already fired, and the cancelled requests' continuations already ran,
        // when CancelRunningTool returns, so the response is built from its result.
        internal static CancelRunningToolResponse Execute(UnityCliLoopToolRegistrarService toolRegistrarService)
        {
            if (toolRegistrarService == null)
            {
                throw new ArgumentNullException(nameof(toolRegistrarService));
            }

            UnityCliLoopCancelRequestResult result = toolRegistrarService.CancelRunningTool();
            if (result.FiredCancellation)
            {
                // Why: a tool waiting for frames while a pause point holds Play Mode paused would
                // never see its cancellation through. This only sets a flag; the next main-thread
                // tick releases the pause.
                UloopPausePointRegistry.ResumeEditorPauseForClientDisconnect();
            }

            return new CancelRunningToolResponse(result);
        }
    }
}
