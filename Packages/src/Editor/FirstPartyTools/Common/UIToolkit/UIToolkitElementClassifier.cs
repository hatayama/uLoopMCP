#nullable enable
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Decides which UI Toolkit controls are annotated and names each with the uGUI type word, so the same
    /// interaction mapping applies to both UI systems.
    /// </summary>
    internal static class UIToolkitElementClassifier
    {
        // Returns null for elements that are not annotated, including plain elements with a Clickable attached.
        internal static string? Classify(VisualElement element)
        {
            if (element is Button || element is RepeatButton)
            {
                return "Button";
            }

            if (element is BaseBoolField)
            {
                return "Toggle";
            }

            if (element is Scroller)
            {
                return "Scrollbar";
            }

            if (element is Slider || element is SliderInt || element is MinMaxSlider)
            {
                return "Slider";
            }

            if (element is TextField)
            {
                return "InputField";
            }

            if (element is DropdownField || element is EnumField)
            {
                return "Dropdown";
            }

            return null;
        }
    }
}
