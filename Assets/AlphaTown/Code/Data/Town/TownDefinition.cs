using AlphaTown.Core.Spatial;
using AlphaTown.Data.Definitions;
using UnityEngine;

namespace AlphaTown.Data.Town
{
    [CreateAssetMenu(menuName = "AlphaTown/Town Definition", fileName = "TownDefinition", order = 60)]
    public sealed class TownDefinition : GameDefinition, ITownDefinition
    {
        [SerializeField, Min(1)]
        [Tooltip("Buildable width in cells. TODO(expansion): this becomes the maximum extent, " +
                 "with unlocked regions gating what is actually buildable.")]
        int _width = 32;

        [SerializeField, Min(1)] int _height = 32;

        public GridSize Size => new GridSize(_width, _height);
    }
}
