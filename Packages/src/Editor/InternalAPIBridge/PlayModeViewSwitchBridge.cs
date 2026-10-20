using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop.InternalAPIBridge
{
    /// <summary>
    /// Which kind of Play Mode view a window is. Other covers third-party PlayModeView subclasses.
    /// </summary>
    public enum PlayModeViewKind
    {
        None,
        GameView,
        Simulator,
        Other,
    }

    /// <summary>
    /// Swaps the main Play Mode view tab between the Game view and the Device Simulator the same
    /// way the Game view's own dropdown does, and drives the simulated device rotation.
    /// PlayModeView is reached directly (friend assembly); the Device Simulator module keeps its
    /// internals hidden from this assembly, so those members are reached through reflection.
    /// </summary>
    public static class PlayModeViewSwitchBridge
    {
        private const string SimulatorWindowFullName = "UnityEditor.DeviceSimulation.SimulatorWindow";
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static PlayModeViewKind GetMainViewKind()
        {
            PlayModeView view = PlayModeView.GetMainPlayModeView();
            if (view == null)
            {
                return PlayModeViewKind.None;
            }

            if (view is GameView)
            {
                return PlayModeViewKind.GameView;
            }

            if (TryResolveSimulatorType(out Type simulatorType) && view.GetType() == simulatorType)
            {
                return PlayModeViewKind.Simulator;
            }

            return PlayModeViewKind.Other;
        }

        public static bool IsSimulatorAvailable()
        {
            return TryResolveSimulatorType(out _);
        }

        /// <summary>
        /// The window that currently serves as the main Play Mode view.
        /// Precondition: <see cref="GetMainViewKind"/> is not None.
        /// </summary>
        public static EditorWindow GetMainViewWindow()
        {
            PlayModeView view = PlayModeView.GetMainPlayModeView();
            if (view == null)
            {
                throw new InvalidOperationException("No Play Mode view is open.");
            }

            return view;
        }

        /// <summary>
        /// Replaces the main Play Mode view tab in place with a window of the target kind and
        /// returns that new window.
        /// Precondition: target is GameView or Simulator, a main view exists, and it is not already
        /// of the target kind.
        /// </summary>
        public static EditorWindow SwapMainViewTo(PlayModeViewKind target)
        {
            Type targetType = ResolveWindowType(target);
            PlayModeView main = (PlayModeView)GetMainViewWindow();
            if (main.GetType() == targetType)
            {
                throw new InvalidOperationException($"The main Play Mode view is already a {targetType.Name}.");
            }

            // SwapMainWindow returns nothing, and right after it GetMainPlayModeView can still name the
            // old window (it is destroyed later while playing), so the new window is found as the one
            // that appeared in the registry.
            HashSet<PlayModeView> before = new();
            foreach (PlayModeView window in PlayModeView.GetAllPlayModeViewWindows())
            {
                if (window != null)
                {
                    before.Add(window);
                }
            }

            main.SwapMainWindow(targetType);

            foreach (PlayModeView window in PlayModeView.GetAllPlayModeViewWindows())
            {
                if (window != null && window.GetType() == targetType && !before.Contains(window))
                {
                    return window;
                }
            }

            throw new InvalidOperationException($"The Play Mode view did not switch to {targetType.Name}.");
        }

        /// <summary>
        /// Opens a window of the target kind when no Play Mode view exists.
        /// Precondition: <see cref="GetMainViewKind"/> is None.
        /// </summary>
        public static EditorWindow OpenView(PlayModeViewKind target)
        {
            Type targetType = ResolveWindowType(target);
            // focus: false — the default overload focuses the window, which brings Unity to the front
            // and defeats the background Play focus suppression.
            return EditorWindow.GetWindow(targetType, false, null, false);
        }

        /// <summary>
        /// Rotates the simulated device and applies the change so that the rendered orientation is
        /// current when it is read next. Returns false when this Unity version exposes none of the
        /// members involved; nothing is invoked in that case.
        /// </summary>
        public static bool TrySetSimulatorRotation(EditorWindow simulatorWindow, int degrees)
        {
            Debug.Assert(simulatorWindow != null, "simulatorWindow must not be null");

            object main = GetPropertyValue(simulatorWindow, "main", PublicInstance);
            object userInterface = GetPropertyValue(main, "userInterface", PublicInstance);
            MethodInfo rotationSetter = userInterface?.GetType()
                .GetProperty("Rotation", AnyInstance)
                ?.GetSetMethod(true);
            object screenSimulation = GetPropertyValue(main, "ScreenSimulation", PublicInstance);
            MethodInfo applyChanges = screenSimulation?.GetType().GetMethod("ApplyChanges", PublicInstance);
            if (rotationSetter == null || applyChanges == null)
            {
                return false;
            }

            InvokeUnwrapped(() => rotationSetter.Invoke(userInterface, new object[] { degrees }));
            // The rotation only requests an orientation; the Simulator applies it on its next repaint,
            // which may not come while the Editor is unfocused. Applying it here makes the orientation
            // readable right away.
            InvokeUnwrapped(() => applyChanges.Invoke(screenSimulation, null));
            return true;
        }

        /// <summary>
        /// The orientation the simulated screen renders, as the ScreenOrientation member name, or null
        /// when it cannot be read.
        /// </summary>
        public static string GetSimulatorRenderedOrientation(EditorWindow simulatorWindow)
        {
            Debug.Assert(simulatorWindow != null, "simulatorWindow must not be null");

            object main = GetPropertyValue(simulatorWindow, "main", PublicInstance);
            object screenSimulation = GetPropertyValue(main, "ScreenSimulation", PublicInstance);
            object orientation = GetPropertyValue(screenSimulation, "orientation", PublicInstance);
            if (orientation is ScreenOrientation value)
            {
                return OrientationName(value);
            }

            return orientation?.ToString();
        }

        // ScreenOrientation still declares the obsolete alias Landscape with the same value as
        // LandscapeLeft, and Enum.ToString does not promise which of two equal names it picks.
        private static string OrientationName(ScreenOrientation value)
        {
            switch (value)
            {
                case ScreenOrientation.Portrait:
                    return "Portrait";
                case ScreenOrientation.PortraitUpsideDown:
                    return "PortraitUpsideDown";
                case ScreenOrientation.LandscapeLeft:
                    return "LandscapeLeft";
                case ScreenOrientation.LandscapeRight:
                    return "LandscapeRight";
                default:
                    return value.ToString();
            }
        }

        private static object GetPropertyValue(object target, string propertyName, BindingFlags flags)
        {
            if (target == null)
            {
                return null;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, flags);
            if (property == null)
            {
                return null;
            }

            return InvokeUnwrapped(() => property.GetValue(target));
        }

        private static Type ResolveWindowType(PlayModeViewKind kind)
        {
            if (kind == PlayModeViewKind.GameView)
            {
                return typeof(GameView);
            }

            if (kind == PlayModeViewKind.Simulator)
            {
                if (!TryResolveSimulatorType(out Type simulatorType))
                {
                    throw new InvalidOperationException("The Device Simulator is not available in this Editor.");
                }

                return simulatorType;
            }

            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only GameView and Simulator can be opened.");
        }

        private static bool TryResolveSimulatorType(out Type simulatorType)
        {
            foreach (Type windowType in PlayModeView.GetAvailableWindowTypes().Keys)
            {
                if (windowType.FullName == SimulatorWindowFullName)
                {
                    simulatorType = windowType;
                    return true;
                }
            }

            simulatorType = null;
            return false;
        }

        // Reflection wraps exceptions thrown by the target in TargetInvocationException, whose message
        // says nothing; rethrow Unity's own exception so the tool error carries its reason.
        private static object InvokeUnwrapped(Func<object> invocation)
        {
            try
            {
                return invocation();
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
