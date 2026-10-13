#nullable enable
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// A drag on a UI Toolkit control: the left button is pressed on the control, moved, and released through
    /// its panel, so controls that capture the pointer (Slider, Scroller) follow it as with real input.
    /// </summary>
    internal sealed class UIToolkitDragSession : IMouseUiDragSession
    {
        private const int LeftButton = 0;

        private readonly UIToolkitPointerTarget _target;
        private Vector2 _currentInputPosition;

        private UIToolkitDragSession(UIToolkitPointerTarget target, Vector2 inputStart)
        {
            _target = target;
            _currentInputPosition = inputStart;
            Report = new MouseUiHitReport(target.Name, target.Path, MouseUiHitSystem.UIToolkit);
        }

        /// <summary>
        /// Presses the left button on the control at the start point.
        /// </summary>
        // UI Toolkit passes handler exceptions on to the sender; a press that throws is released before the
        // exception leaves, since no session exists yet to release it later.
        internal static UIToolkitDragSession Begin(UIToolkitPointerTarget target, Vector2 inputStart)
        {
            bool pressed = false;
            try
            {
                target.PressDown(inputStart, LeftButton);
                pressed = true;
            }
            finally
            {
                if (!pressed)
                {
                    target.Release(inputStart, LeftButton);
                }
            }

            return new UIToolkitDragSession(target, inputStart);
        }

        public MouseUiHitReport Report { get; }

        // Stored as an input position: UI Toolkit maps Game View points itself and never goes through Screen.
        public Vector2 CurrentInputPosition => _currentInputPosition;

        public bool IsTargetDestroyed => _target.IsDocumentDestroyed;

        public bool UsesEventSystem => false;

        // No "interrupted" check as uGUI has: some controls never capture the pointer, so the lack of a capture
        // cannot tell an interrupted drag from a normal one.
        public string? CheckStillActive()
        {
            return _target.IsAvailable
                ? null
                : "Drag target UI Toolkit element is no longer available (its document was disabled or destroyed).";
        }

        public void MoveTo(Vector2 inputPosition)
        {
            _target.MoveTo(inputPosition);
            _currentInputPosition = inputPosition;
        }

        // Callers refuse a drop target before a UI Toolkit drag starts or ends, so none reaches here.
        public void Finish(GameObject? explicitDropTarget)
        {
            _target.Release(_currentInputPosition, LeftButton);
        }
    }
}
