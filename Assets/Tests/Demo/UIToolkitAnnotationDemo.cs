#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.UnityCliLoop.Tests.Demo
{
    /// <summary>
    /// Builds the UI Toolkit controls the screenshot annotation E2E expects on two screen-space panels, and
    /// counts button clicks so the E2E can check that a click reached a control.
    /// </summary>
    public sealed class UIToolkitAnnotationDemo : MonoBehaviour
    {
        [SerializeField] private UIDocument backDocument = null!;
        [SerializeField] private UIDocument frontDocument = null!;

        private readonly Dictionary<string, int> _clickCounts = new Dictionary<string, int>();

        public int GetClickCount(string elementName)
        {
            return _clickCounts.TryGetValue(elementName, out int count) ? count : 0;
        }

        // Start runs after every Awake and OnEnable, so both documents have created their roots by now
        // regardless of component order.
        private void Start()
        {
            BuildBackPanel(backDocument.rootVisualElement);
            BuildFrontPanel(frontDocument.rootVisualElement);
        }

        private void BuildBackPanel(VisualElement root)
        {
            AddPlaced(root, CreateCountingButton("play"), 100f, 100f, 200f, 50f);
            AddPlaced(root, new Toggle("Sound") { name = "sound" }, 100f, 180f, 200f, 30f);
            AddPlaced(root, new Slider("Volume", 0f, 1f) { name = "volume" }, 100f, 240f, 300f, 30f);
            AddPlaced(root, new TextField("Player") { name = "player-name" }, 100f, 300f, 300f, 40f);
            DropdownField quality = new DropdownField(
                "Quality", new List<string> { "Low", "Medium", "High" }, 1) { name = "quality" };
            AddPlaced(root, quality, 100f, 370f, 300f, 40f);

            Button locked = CreateCountingButton("locked");
            locked.SetEnabled(false);
            AddPlaced(root, locked, 100f, 440f, 200f, 50f);

            Button ghost = CreateCountingButton("ghost");
            ghost.style.visibility = Visibility.Hidden;
            AddPlaced(root, ghost, 100f, 510f, 200f, 50f);

            ScrollView list = new ScrollView { name = "list" };
            list.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            VisualElement content = new VisualElement { name = "list-content" };
            content.style.height = 1000f;
            content.style.flexShrink = 0f;
            list.Add(content);
            AddPlaced(root, list, 500f, 100f, 200f, 200f);

            AddPlaced(root, CreateCountingButton("partly-covered"), 800f, 100f, 300f, 60f);
        }

        // Covers the left 60% of partly-covered from a panel with a higher sorting order.
        private static void BuildFrontPanel(VisualElement root)
        {
            VisualElement cover = new VisualElement { name = "cover" };
            cover.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f, 1f);
            AddPlaced(root, cover, 800f, 100f, 180f, 60f);
        }

        private Button CreateCountingButton(string elementName)
        {
            Button button = new Button { name = elementName, text = elementName };
            button.clicked += () => _clickCounts[elementName] = GetClickCount(elementName) + 1;
            return button;
        }

        // Margins are zeroed because the theme gives controls margins that would shift their bounds away from
        // the coordinates the E2E checks.
        private static void AddPlaced(
            VisualElement root, VisualElement element, float left, float top, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
            element.style.marginLeft = 0f;
            element.style.marginTop = 0f;
            element.style.marginRight = 0f;
            element.style.marginBottom = 0f;
            root.Add(element);
        }
    }
}
