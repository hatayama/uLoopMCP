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
        // cancellation has already fired when CancelRunningTool returns: continuations registered
        // synchronously on the token have run, and others are only scheduled, so the response is
        // built from its result rather than from whether the tool has stopped.
        internal static CancelRunningToolResponse Execute(UnityCliLoopToolRegistrarService toolRegistrarService)
        {
            if (toolRegistrarService == null)
            {
                throw new ArgumentNullException(nameof(toolRegistrarService));
            }

            UnityCliLoopCancelRequestResult result = toolRegistrarService.CancelRunningTool();
            if (result.FiredCancellation)
            {
                // Why: a tool waiting for frames while Play Mode is paused would never see its
                // cancellation through. Like a killed CLI, this releases any Editor pause, whether
                // a pause point or a manual pause holds it. This only sets a flag; the next
                // main-thread tick releases the pause.
                UloopPausePointRegistry.ResumeEditorPauseForClientDisconnect();
            }

            return new CancelRunningToolResponse(result);
        }
    }
}
