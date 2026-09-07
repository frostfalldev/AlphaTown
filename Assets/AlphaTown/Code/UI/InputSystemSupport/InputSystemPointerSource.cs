using AlphaTown.Core.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// Pointers read from the Input System package.
    ///
    /// This lives in its own assembly, constrained to <c>ENABLE_INPUT_SYSTEM</c> and
    /// <c>!ENABLE_LEGACY_INPUT_MANAGER</c>. When those do not hold, Unity skips the assembly
    /// entirely and never tries to resolve its reference to <c>Unity.InputSystem</c> — which is
    /// the point. A project without the package still compiles, and a project on "Both" keeps the
    /// legacy path rather than having two sources argue over which is in charge.
    ///
    /// It registers itself rather than being referenced, so <c>AlphaTown.UI</c> has no dependency
    /// on an assembly that may not exist.
    /// </summary>
    public sealed class InputSystemPointerSource : IPointerSource
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // Devices are read straight off Touchscreen.current rather than through the
            // EnhancedTouch API, so there is nothing to enable first — and one less thing that can
            // be forgotten in a build.
            PointerInput.SetSource(new InputSystemPointerSource());
            Log.Info("Input", "Legacy input is disabled; reading pointers from the Input System.");
        }

        public bool IsAvailable => Touchscreen.current != null || Mouse.current != null;

        public int Count
        {
            get
            {
                var touches = CountLiveTouches();
                if (touches > 0) return touches;

                return IsMouseHeld() ? 1 : 0;
            }
        }

        public float ScrollDelta
        {
            get
            {
                var mouse = Mouse.current;
                if (mouse == null) return 0f;

                // The Input System reports scroll in raw device units — 120 per notch on Windows —
                // where the legacy class reported notches. Divided so both backends feel the same.
                return mouse.scroll.ReadValue().y / 120f;
            }
        }

        public bool TryGet(int index, out int id, out Vector2 position)
        {
            id = 0;
            position = Vector2.zero;
            if (index < 0) return false;

            var screen = Touchscreen.current;
            if (screen != null)
            {
                var live = 0;
                var touches = screen.touches;

                for (var i = 0; i < touches.Count; i++)
                {
                    if (!IsPressed(touches[i])) continue;

                    if (live == index)
                    {
                        id = touches[i].touchId.ReadValue();
                        position = touches[i].position.ReadValue();
                        return true;
                    }

                    live++;
                }

                if (live > 0) return false;
            }

            if (index != 0 || !IsMouseHeld()) return false;

            id = MousePointerId;
            position = Mouse.current.position.ReadValue();
            return true;
        }

        /// <summary>Matches the legacy source's mouse id, so gestures behave identically.</summary>
        const int MousePointerId = -1;

        static bool IsPressed(TouchControl touch)
        {
            var phase = touch.phase.ReadValue();
            return phase == UnityEngine.InputSystem.TouchPhase.Began ||
                   phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                   phase == UnityEngine.InputSystem.TouchPhase.Stationary;
        }

        static int CountLiveTouches()
        {
            var screen = Touchscreen.current;
            if (screen == null) return 0;

            var live = 0;
            var touches = screen.touches;
            for (var i = 0; i < touches.Count; i++)
            {
                if (IsPressed(touches[i])) live++;
            }

            return live;
        }

        static bool IsMouseHeld()
        {
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }
    }
}
