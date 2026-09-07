using UnityEngine;

namespace AlphaTown.Data.Presentation
{
    /// <summary>
    /// The art for a building: its icon in menus, and the sprite drawn on the map.
    ///
    /// Separate from <see cref="Buildings.IBuildingDefinition"/> for the same reason as items — the
    /// simulation decides what a building does, and never what it looks like.
    /// </summary>
    public interface IBuildingVisuals
    {
        /// <summary>Shown in the build menu and the info panel.</summary>
        Sprite Icon { get; }

        /// <summary>Drawn in the town. Falls back to <see cref="Icon"/> when unset.</summary>
        Sprite MapSprite { get; }

        /// <summary>Tint used when there is no sprite at all, so a placeholder town is still readable.</summary>
        Color PlaceholderColour { get; }
    }
}
