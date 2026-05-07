using System.Collections.Generic;
using System.Diagnostics;

using io.github.hatayama.UnityCliLoop.Application;

namespace io.github.hatayama.UnityCliLoop.Infrastructure
{
    // Infrastructure adapter that resolves hidden tool names from installed skill metadata.
    public sealed class SkillInstallLayoutInternalToolNameProvider : IInternalToolNameProvider
    {
        public HashSet<string> GetInternalToolNames(string projectRoot)
        {
            Debug.Assert(!string.IsNullOrEmpty(projectRoot), "projectRoot must not be null or empty");

            return SkillInstallLayout.GetInternalSkillToolNames(projectRoot);
        }
    }
}
