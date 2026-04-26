using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public class UIElementAnnotatorTests
    {
        [Test]
        public void GetAnnotationColorForElement_WhenLabelsAreDifferent_ShouldReturnDifferentColors()
        {
            Color firstColor = UIElementAnnotator.GetAnnotationColorForElement(new UIElementInfo { Label = "A", Type = "Button" });
            Color secondColor = UIElementAnnotator.GetAnnotationColorForElement(new UIElementInfo { Label = "B", Type = "Button" });

            Assert.That(firstColor, Is.Not.EqualTo(secondColor));
        }

        [Test]
        public void GetAnnotationColorForElement_WhenTypeIsDifferentButLabelMatches_ShouldReturnSameColor()
        {
            Color buttonColor = UIElementAnnotator.GetAnnotationColorForElement(new UIElementInfo { Label = "C", Type = "Button" });
            Color draggableColor = UIElementAnnotator.GetAnnotationColorForElement(new UIElementInfo { Label = "C", Type = "Draggable" });

            Assert.That(buttonColor, Is.EqualTo(draggableColor));
        }
    }
}
