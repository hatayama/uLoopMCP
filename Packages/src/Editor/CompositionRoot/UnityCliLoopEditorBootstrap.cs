using UnityEditor;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.CompositionRoot
{
    // Centralizes production Editor startup so Unity's unordered hooks do not decide boot order.
    internal static class UnityCliLoopEditorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            UnityCliLoopEditorBootstrapper bootstrapper = new();
            bootstrapper.Initialize();
        }
    }
}
