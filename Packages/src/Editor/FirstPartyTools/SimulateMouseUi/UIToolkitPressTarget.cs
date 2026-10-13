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

        public void Click()
        {
            _target.PressDown(_gameViewTopLeft, _button);
            _target.Release(_gameViewTopLeft, _button);
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
