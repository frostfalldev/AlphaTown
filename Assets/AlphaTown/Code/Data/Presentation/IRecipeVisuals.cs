using UnityEngine;

namespace AlphaTown.Data.Presentation
{
    /// <summary>
    /// The art for one production step, and — for crops — the frames it grows through.
    ///
    /// Growth stages are presentation only. The simulation knows a start timestamp and a duration;
    /// which picture that maps to is a question the view asks, per frame, of
    /// <see cref="StageFor"/>. Adding or removing frames re-times the animation and changes
    /// nothing about when the crop is ready.
    /// </summary>
    public interface IRecipeVisuals
    {
        Sprite Icon { get; }

        /// <summary>Ordered seedling to ripe. Empty is legal: not every recipe grows in a field.</summary>
        Sprite[] GrowthStageSprites { get; }

        /// <summary>
        /// Frame for a 0..1 progress fraction. The last frame is the finished look, so a ready
        /// crop and a crop one tick from ready are drawn the same — which is why the view also
        /// checks the producer's ready tray before drawing the harvest prompt.
        /// </summary>
        Sprite StageFor(float progress);
    }
}
