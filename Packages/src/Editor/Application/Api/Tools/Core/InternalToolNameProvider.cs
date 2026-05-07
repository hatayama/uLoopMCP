using System;
using System.Collections.Generic;

using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    public interface IInternalToolNameProvider
    {
        HashSet<string> GetInternalToolNames(string projectRoot);
    }

    public sealed class EmptyInternalToolNameProvider : IInternalToolNameProvider
    {
        public HashSet<string> GetInternalToolNames(string projectRoot)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
