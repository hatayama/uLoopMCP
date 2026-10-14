using System;
using NUnit.Framework;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which UI Toolkit controls are annotated and which uGUI-style type each one is reported as.
    /// </summary>
    public sealed class UIToolkitElementClassifierTests
    {
        /// <summary>
        /// Verifies each supported control maps to its type, and plain elements map to null.
        /// </summary>
        [TestCase(typeof(Button), "Button")]
        [TestCase(typeof(RepeatButton), "Button")]
        [TestCase(typeof(Toggle), "Toggle")]
        [TestCase(typeof(RadioButton), "Toggle")]
        [TestCase(typeof(Scroller), "Scrollbar")]
        [TestCase(typeof(Slider), "Slider")]
        [TestCase(typeof(SliderInt), "Slider")]
        [TestCase(typeof(MinMaxSlider), "Slider")]
        [TestCase(typeof(TextField), "InputField")]
        [TestCase(typeof(DropdownField), "Dropdown")]
        [TestCase(typeof(EnumField), "Dropdown")]
        [TestCase(typeof(Label), null)]
        [TestCase(typeof(VisualElement), null)]
        public void Classify_ShouldMapControlToType(Type elementType, string expectedType)
        {
            VisualElement element = (VisualElement)Activator.CreateInstance(elementType);

            Assert.That(UIToolkitElementClassifier.Classify(element), Is.EqualTo(expectedType));
        }
    }
}
