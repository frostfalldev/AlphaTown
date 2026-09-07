using System;
using UnityEngine;

namespace AlphaTown.UI.Selection
{
    /// <summary>
    /// The tool the player is holding, if any.
    ///
    /// Separate from <see cref="TownSelection"/> because they answer different questions — what is
    /// selected, versus what happens when you drag — and because arming a tool must not clear the
    /// selection that armed it.
    ///
    /// A tool is a mode, and modes are only worth their cost when they are impossible to be in by
    /// accident and obvious once you are. Arming the sickle takes two deliberate taps and puts a
    /// banner on screen; it disarms itself when there is nothing left to cut.
    /// </summary>
    public sealed class TownTool : MonoBehaviour
    {
        public event Action Changed;

        public TownToolKind Active { get; private set; }

        public bool IsSickleArmed => Active == TownToolKind.Sickle;

        public void Select(TownToolKind kind)
        {
            if (Active == kind) return;

            Active = kind;
            Changed?.Invoke();
        }

        public void Toggle(TownToolKind kind) => Select(Active == kind ? TownToolKind.None : kind);

        public void Clear() => Select(TownToolKind.None);
    }
}
