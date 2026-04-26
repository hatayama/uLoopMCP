using System.Collections.Generic;
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

        [Test]
        public void GetAnnotationColorForElement_WhenFirstSixteenLabelsAreUsed_ShouldReturnUniqueColors()
        {
            HashSet<string> colorKeys = new HashSet<string>();
            for (int i = 0; i < 16; i++)
            {
                string label = ((char)('A' + i)).ToString();
                Color color = UIElementAnnotator.GetAnnotationColorForElement(new UIElementInfo { Label = label, Type = "Button" });

                colorKeys.Add(CreateColorKey(color));
            }

            Assert.That(colorKeys.Count, Is.EqualTo(16));
        }

        [Test]
        public void GetContrastingTextColor_WhenBackgroundIsBright_ShouldReturnDarkText()
        {
            Color textColor = UIElementAnnotator.GetContrastingTextColor(new Color(1f, 0.9f, 0f, 0.95f));

            Assert.That(textColor, Is.EqualTo(new Color(0f, 0f, 0f, 0.95f)));
        }

        [Test]
        public void GetContrastingTextColor_WhenBackgroundIsDark_ShouldReturnLightText()
        {
            Color textColor = UIElementAnnotator.GetContrastingTextColor(new Color(0.15f, 0.55f, 1f, 0.95f));

            Assert.That(textColor, Is.EqualTo(new Color(1f, 1f, 1f, 0.95f)));
        }

        [Test]
        public void GetContrastPartnerColor_WhenColorIsBright_ShouldReturnLightOutlineForDarkText()
        {
            Color outlineColor = UIElementAnnotator.GetContrastPartnerColor(new Color(1f, 0.9f, 0f, 0.95f));

            Assert.That(outlineColor, Is.EqualTo(new Color(1f, 1f, 1f, 0.95f)));
        }

        [Test]
        public void GetContrastPartnerColor_WhenColorIsDark_ShouldReturnDarkOutlineForLightText()
        {
            Color outlineColor = UIElementAnnotator.GetContrastPartnerColor(new Color(0.15f, 0.55f, 1f, 0.95f));

            Assert.That(outlineColor, Is.EqualTo(new Color(0f, 0f, 0f, 0.95f)));
        }

        private static string CreateColorKey(Color color)
        {
            return $"{color.r:F3}:{color.g:F3}:{color.b:F3}:{color.a:F3}";
        }
    }
}
