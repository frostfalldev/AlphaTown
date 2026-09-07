using UnityEngine;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// One frame's worth of pointers — fingers, or the mouse standing in for one.
    ///
    /// Deliberately reports only "which pointers are down and where", not began/moved/ended.
    /// Phase enums differ between Unity's two input backends in ways that are easy to mirror
    /// slightly wrong, and every consumer here needs to track the previous frame anyway. Deriving
    /// the phases in one place (<see cref="TownGestures"/>) means there is one implementation of
    /// that logic to get right rather than one per backend.
    /// </summary>
    public interface IPointerSource
    {
        /// <summary>False when the backend this source wraps is not the active one.</summary>
        bool IsAvailable { get; }

        /// <summary>Pointers currently pressed. Zero on the frame the last finger lifts.</summary>
        int Count { get; }

        /// <summary>
        /// Position in screen pixels, bottom-left origin — the convention the whole project uses,
        /// including <c>Camera.ScreenToWorldPoint</c>.
        ///
        /// <paramref name="id"/> is stable for the life of a press, so a gesture can follow the
        /// same finger even as others come and go.
        /// </summary>
        bool TryGet(int index, out int id, out Vector2 position);

        /// <summary>Mouse wheel, in notches. Always zero on a device.</summary>
        float ScrollDelta { get; }
    }
}
