using AlphaTown.Core.Spatial;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Presentation;
using AlphaTown.Data.Production;
using UnityEngine;

namespace AlphaTown.Data.Buildings
{
    [CreateAssetMenu(menuName = "AlphaTown/Buildings/Building Definition", fileName = "Building_", order = 50)]
    public sealed class BuildingDefinition : GameDefinition, IBuildingDefinition, IBuildingVisuals
    {
        static readonly BuildingLevel FallbackLevel = new BuildingLevel();

        [SerializeField] string _displayNameKey;
        [SerializeField] BuildingCategory _category = BuildingCategory.Production;
        [SerializeField, Min(1)] int _unlockLevel = 1;

        [Header("Footprint")]
        [SerializeField, Min(1)] int _footprintWidth = 1;
        [SerializeField, Min(1)] int _footprintHeight = 1;

        [Header("Levels")]
        [SerializeField]
        [Tooltip("Element 0 is level 1, the initial build. Later entries are upgrades.")]
        BuildingLevel[] _levels = { new BuildingLevel() };

        [Header("Presentation")]
        [Tooltip("Presentation only. Nothing in the simulation reads these.")]
        [SerializeField] Sprite _icon;

        [Tooltip("Drawn on the map. Falls back to the icon when empty.")]
        [SerializeField] Sprite _mapSprite;

        [Tooltip("Used when there is no sprite yet, so a placeholder town still reads.")]
        [SerializeField] Color _placeholderColour = new Color(0.72f, 0.62f, 0.44f, 1f);

        [Header("Behaviour")]
        [SerializeField]
        [Tooltip("Optional. The producer this building runs once construction finishes.")]
        ProducerDefinition _producer;

        [SerializeField]
        [Tooltip("Optional. Upgrading past the last level replaces this building with that one.")]
        BuildingDefinition _upgradesInto;

        public string DisplayNameKey => _displayNameKey;
        public BuildingCategory Category => _category;
        public GridSize Footprint => new GridSize(_footprintWidth, _footprintHeight);
        public int UnlockLevel => _unlockLevel;
        public int MaxLevel => _levels != null && _levels.Length > 0 ? _levels.Length : 1;

        public string ProducerDefinitionId => _producer != null ? _producer.Id : string.Empty;

        public string UpgradesIntoId => _upgradesInto != null ? _upgradesInto.Id : string.Empty;

        public Sprite Icon => _icon;
        public Sprite MapSprite => _mapSprite != null ? _mapSprite : _icon;
        public Color PlaceholderColour => _placeholderColour;

        public IBuildingLevel GetLevel(int level)
        {
            if (_levels == null || _levels.Length == 0) return FallbackLevel;

            var index = Mathf.Clamp(level, 1, _levels.Length) - 1;
            return _levels[index] ?? FallbackLevel;
        }

        void OnEnable() => InvalidateLevelCaches();

        void InvalidateLevelCaches()
        {
            if (_levels == null) return;

            for (var i = 0; i < _levels.Length; i++) _levels[i]?.InvalidateCache();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (string.IsNullOrWhiteSpace(_displayNameKey)) _displayNameKey = "building." + Id;
            if (_levels == null || _levels.Length == 0) _levels = new[] { new BuildingLevel() };

            if (_upgradesInto == this)
            {
                Core.Diagnostics.Log.Error("BuildingDefinition",
                    "'" + name + "' upgrades into itself. Clearing to avoid an endless upgrade.");
                _upgradesInto = null;
            }

            InvalidateLevelCaches();
        }
#endif
    }
}
