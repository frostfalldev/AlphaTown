using UnityEngine;

namespace AlphaTown.Data.Presentation
{
    /// <summary>
    /// The art for an item, kept off <see cref="Items.IItemDefinition"/> on purpose.
    ///
    /// The simulation contract stays free of <c>UnityEngine</c> types so it can be faked with a
    /// plain object in a test, and so nothing in Gameplay can accidentally start branching on a
    /// sprite. A view asks for this separately:
    /// <code>if (definition is IItemVisuals visuals) icon.sprite = visuals.Icon;</code>
    /// </summary>
    public interface IItemVisuals
    {
        Sprite Icon { get; }
    }
}
