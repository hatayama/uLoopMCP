#nullable enable
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Names UI Toolkit elements the way annotations report them: the owning document's GameObject path,
    /// a '#', then the element chain below the document root.
    /// </summary>
    internal static class UIToolkitElementPath
    {
        private const char PATH_SEPARATOR = '#';
        private const string CHAIN_SEPARATOR = "/";

        internal static string Build(UIDocument document, VisualElement element)
        {
            return GameObjectPathUtility.GetFullPath(document.gameObject) + PATH_SEPARATOR
                + BuildElementChain(document.rootVisualElement, element);
        }

        internal static string GetDisplayName(VisualElement element)
        {
            return string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name;
        }

        // Lists the elements from just below the document root down to the element, so the root itself, which
        // has no name of its own, is left out.
        private static string BuildElementChain(VisualElement root, VisualElement element)
        {
            List<string> segments = new List<string>();
            VisualElement? current = element;
            while (current != null && current != root)
            {
                segments.Add(GetDisplayName(current));
                current = current.hierarchy.parent;
            }

            segments.Reverse();
            return string.Join(CHAIN_SEPARATOR, segments);
        }
    }
}
