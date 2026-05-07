using System;
using System.Collections.Generic;

namespace io.github.hatayama.UnityCliLoop.Application
{
    public interface IInternalToolNameProvider
    {
        HashSet<string> GetInternalToolNames(string projectRoot);
    }

    /// <summary>
    /// Provides Empty Internal Tool Name dependencies to callers without exposing construction details.
    /// </summary>
    public sealed class EmptyInternalToolNameProvider : IInternalToolNameProvider
    {
        public HashSet<string> GetInternalToolNames(string projectRoot)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
