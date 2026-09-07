using AlphaTown.Core.Spatial;
using AlphaTown.Data.Definitions;
using UnityEngine;

namespace AlphaTown.Data.Town
{
    [CreateAssetMenu(menuName = "AlphaTown/Town Definition", fileName = "TownDefinition", order = 60)]
    public sealed class TownDefinition : GameDefinition, ITownDefinition
    {
        [SerializeField, Min(1)]
        [Tooltip("Maximum town width in cells. Expansion unlocks regions inside this.")]
        int _width = 32;

        [SerializeField, Min(1)] int _height = 32;

        [Header("Starting area")]
        [SerializeField, Min(0)] int _startX;
        [SerializeField, Min(0)] int _startY;

        [SerializeField, Min(0)]
        [Tooltip("Leave width or height at zero to start with the whole grid unlocked.")]
        int _startWidth;

        [SerializeField, Min(0)] int _startHeight;

        public GridSize Size => new GridSize(_width, _height);

        public GridRect StartingArea =>
            new GridRect(new GridPosition(_startX, _startY), new GridSize(_startWidth, _startHeight));
    }
}
