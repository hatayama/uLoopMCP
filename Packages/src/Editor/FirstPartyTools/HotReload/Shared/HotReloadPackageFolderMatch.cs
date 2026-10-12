using System;

using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Tells whether a slash-separated path lies in a folder, respecting folder boundaries. Does no
    /// IO, so it can run on any thread.
    /// </summary>
    internal static class HotReloadPackageFolderMatch
    {
        /// <summary>
        /// Returns the length of <paramref name="folder"/> when <paramref name="path"/> is inside it,
        /// so that the character after it is "/"; -1 otherwise, and -1 for a null folder. Why the
        /// "/": "/a/LocalPkg2/x.cs" must not count as inside "/a/LocalPkg".
        /// </summary>
        internal static int PrefixLength(string path, string folder, StringComparison comparison)
        {
            Debug.Assert(path != null, "path must not be null.");

            if (folder == null || path.Length <= folder.Length + 1)
            {
                return -1;
            }

            if (path[folder.Length] != '/' || !path.StartsWith(folder, comparison))
            {
                return -1;
            }

            return folder.Length;
        }

        /// <summary>
        /// Returns the folder with forward slashes and without a trailing slash, the form
        /// <see cref="PrefixLength"/> expects.
        /// </summary>
        internal static string NormalizeFolder(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
