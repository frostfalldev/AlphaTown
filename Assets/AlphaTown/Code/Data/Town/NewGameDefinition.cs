using System;
using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Items;
using UnityEngine;

namespace AlphaTown.Data.Town
{
    [Serializable]
    public sealed class StartingBuildingEntry
    {
        [SerializeField] BuildingDefinition _building;
        [SerializeField, Min(0)] int _x;
        [SerializeField, Min(0)] int _y;

        public BuildingDefinition Building => _building;
        public bool IsValid => _building != null && _building.HasValidId;

        public StartingBuilding ToStartingBuilding() =>
            new StartingBuilding(_building != null ? _building.Id : null, new GridPosition(_x, _y));
    }

    [CreateAssetMenu(menuName = "AlphaTown/Town/New Game", fileName = "NewGame", order = 62)]
    public sealed class NewGameDefinition : GameDefinition, INewGameDefinition
    {
        [SerializeField, Min(1)] int _startingBarnLevel = 1;
        [SerializeField] ItemAmount[] _startingItems = Array.Empty<ItemAmount>();
        [SerializeField] StartingBuildingEntry[] _startingBuildings = Array.Empty<StartingBuildingEntry>();

        ItemStack[] _cachedItems;
        StartingBuilding[] _cachedBuildings;

        public int StartingBarnLevel => _startingBarnLevel;

        public IReadOnlyList<ItemStack> StartingItems =>
            _cachedItems ?? (_cachedItems = BuildItems());

        public IReadOnlyList<StartingBuilding> StartingBuildings =>
            _cachedBuildings ?? (_cachedBuildings = BuildBuildings());

        void OnEnable() => InvalidateCache();

        void InvalidateCache()
        {
            _cachedItems = null;
            _cachedBuildings = null;
        }

        ItemStack[] BuildItems()
        {
            if (_startingItems == null || _startingItems.Length == 0) return Array.Empty<ItemStack>();

            var stacks = new List<ItemStack>(_startingItems.Length);
            for (var i = 0; i < _startingItems.Length; i++)
            {
                if (_startingItems[i] == null || !_startingItems[i].IsValid) continue;
                stacks.Add(_startingItems[i].ToStack());
            }

            return stacks.ToArray();
        }

        StartingBuilding[] BuildBuildings()
        {
            if (_startingBuildings == null || _startingBuildings.Length == 0)
                return Array.Empty<StartingBuilding>();

            var placements = new List<StartingBuilding>(_startingBuildings.Length);
            for (var i = 0; i < _startingBuildings.Length; i++)
            {
                if (_startingBuildings[i] == null || !_startingBuildings[i].IsValid) continue;
                placements.Add(_startingBuildings[i].ToStartingBuilding());
            }

            return placements.ToArray();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            InvalidateCache();
        }
#endif
    }
}
