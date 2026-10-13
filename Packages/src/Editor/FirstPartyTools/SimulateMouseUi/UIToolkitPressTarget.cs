#nullable enable
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Delivers a click or long press to a UI Toolkit control by sending pointer events to its panel at the
    /// requested Game View point.
    /// </summary>
    internal sealed class UIToolkitPressTarget : IMouseUiPressTarget
    {
        private readonly UIToolkitPointerTarget _target;
        private readonly Vector2 _gameViewTopLeft;
        private readonly int _button;

        internal UIToolkitPressTarget(UIToolkitPointerTarget target, Vector2 gameViewTopLeft, int button)
        {
            _target = target;
            _gameViewTopLeft = gameViewTopLeft;
            _button = button;
            Report = new MouseUiHitReport(target.Name, target.Path, MouseUiHitSystem.UIToolkit);
        }

        public MouseUiHitReport Report { get; }

        // UI Toolkit lets handler exceptions reach the sender, unlike uGUI's ExecuteEvents, so the release is
        // sent even when the press throws, and the exception still reaches the caller.
        public void Click()
        {
            try
            {
                _target.PressDown(_gameViewTopLeft, _button);
            }
            finally
            {
                _target.Release(_gameViewTopLeft, _button);
            }
        }

        public void PressDown()
        {
            _target.PressDown(_gameViewTopLeft, _button);
        }

        public void Release()
        {
            _target.Release(_gameViewTopLeft, _button);
        }
    }
}
