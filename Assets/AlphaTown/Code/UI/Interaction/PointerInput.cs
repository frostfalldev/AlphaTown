using AlphaTown.Core.Diagnostics;
using UnityEngine;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// Where touches come from, whichever input backend the project is set to.
    ///
    /// This exists because of a bug that shipped: everything read <c>UnityEngine.Input</c>
    /// directly, and a project set to "Input System Package (New)" — the Unity 6 default — throws
    /// from every one of those calls at runtime. Pan, zoom and swipe were all dead on device while
    /// the UI Toolkit HUD kept working, because UI Toolkit speaks both backends and this code did
    /// not. Nothing failed to compile, so nothing caught it until an APK was in someone's hand.
    ///
    /// The fix is that no gameplay code names a backend. The legacy source compiles only when
    /// <c>ENABLE_LEGACY_INPUT_MANAGER</c> is defined; a companion assembly, constrained to
    /// <c>ENABLE_INPUT_SYSTEM</c> without it, registers an Input System source at startup. One of
    /// the two is always present, so all three settings work.
    /// </summary>
    public static class PointerInput
    {
        static IPointerSource _source = CreateDefault();
        static bool _hasWarned;

        /// <summary>
        /// Installs a source. Called by the Input System companion assembly before the first
        /// scene loads; there is no reason for gameplay code to call it.
        /// </summary>
        public static void SetSource(IPointerSource source)
        {
            if (source == null || !source.IsAvailable) return;

            _source = source;
            Log.Info("Input", "Pointer source is now " + source.GetType().Name + ".");
        }

        public static int Count => Available ? _source.Count : 0;

        public static float ScrollDelta => Available ? _source.ScrollDelta : 0f;

        public static bool TryGet(int index, out int id, out Vector2 position)
        {
            if (Available) return _source.TryGet(index, out id, out position);

            id = 0;
            position = Vector2.zero;
            return false;
        }

        /// <summary>Convenience for the common single-finger case.</summary>
        public static bool TryGetPrimary(out int id, out Vector2 position) => TryGet(0, out id, out position);

        /// <summary>
        /// Complains exactly once rather than every frame. A build with no usable input backend is
        /// a build nobody can play, and the log should say so in one readable line instead of
        /// sixty a second.
        /// </summary>
        static bool Available
        {
            get
            {
                if (_source != null && _source.IsAvailable) return true;
                if (_hasWarned) return false;

                _hasWarned = true;
                Log.Error("Input",
                    "No pointer source is available, so nothing in the town will respond to touch. " +
                    "Set Project Settings ▸ Player ▸ Active Input Handling to 'Both', or install " +
                    "the Input System package so the companion assembly can register itself.");

                return false;
            }
        }

        static IPointerSource CreateDefault()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new LegacyPointerSource();
#else
            // Left null on purpose. The Input System assembly registers itself before the first
            // scene loads; if it is missing too, the warning above fires once.
            return null;
#endif
        }
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    /// <summary>
    /// The old <c>UnityEngine.Input</c> class. Compiled only when the project actually has it
    /// enabled, so this file cannot be the thing that throws.
    /// </summary>
    sealed class LegacyPointerSource : IPointerSource
    {
        public bool IsAvailable => true;

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
#if UNITY_EDITOR || UNITY_STANDALONE
                return Input.mouseScrollDelta.y;
#else
                return 0f;
#endif
            }
        }

        public bool TryGet(int index, out int id, out Vector2 position)
        {
            id = 0;
            position = Vector2.zero;
            if (index < 0) return false;

            // Touches that ended this frame are skipped so Count falls to zero on release, which
            // is what tells a gesture the finger has lifted.
            var live = 0;
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;

                if (live == index)
                {
                    id = touch.fingerId;
                    position = touch.position;
                    return true;
                }

                live++;
            }

            if (live > 0 || index != 0 || !IsMouseHeld()) return false;

            // The mouse stands in for a single finger, which is what makes the whole scene
            // testable in the Editor without a device.
            id = MousePointerId;
            position = Input.mousePosition;
            return true;
        }

        /// <summary>Outside the range Android assigns to fingers, so it can never collide.</summary>
        const int MousePointerId = -1;

        static int CountLiveTouches()
        {
            var live = 0;
            for (var i = 0; i < Input.touchCount; i++)
            {
                var phase = Input.GetTouch(i).phase;
                if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled) live++;
            }

            return live;
        }

        static bool IsMouseHeld()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // Android synthesises mouse events from touches, which would double every finger.
            // Guarding by platform keeps the device path purely touch-driven.
            return Input.mousePresent && Input.GetMouseButton(0);
#else
            return false;
#endif
        }
    }
#endif
}
