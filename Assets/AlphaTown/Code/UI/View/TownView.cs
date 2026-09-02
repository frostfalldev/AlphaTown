using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Presentation;
using AlphaTown.Gameplay.Bootstrap;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.UI.Hud;
using AlphaTown.UI.Selection;
using UnityEngine;

namespace AlphaTown.UI.View
{
    /// <summary>
    /// Keeps the sprites on screen matching the buildings in the simulation.
    ///
    /// Reconciles rather than listens: each frame it walks the town's building list and adds,
    /// moves or removes views to match. That is a handful of microseconds for a town this size,
    /// and it cannot drift out of sync the way a chain of event subscriptions can — a view that is
    /// wrong is wrong for one frame, not until the next reload.
    ///
    /// TODO(scale): move to events plus a dirty flag when towns get large enough to notice.
    /// </summary>
    public sealed class TownView : MonoBehaviour
    {
        [SerializeField] GameRunner _runner;
        [SerializeField] TownSelection _selection;

        [SerializeField]
        [Tooltip("Optional. A one-unit white sprite is generated when this is empty.")]
        Sprite _placeholderSprite;

        [SerializeField] Color _groundColour = new Color(0.36f, 0.52f, 0.30f);
        [SerializeField] Color _lockedGroundColour = new Color(0.20f, 0.22f, 0.24f);
        [SerializeField] Color _selectionColour = new Color(1f, 0.93f, 0.45f, 0.55f);

        readonly Dictionary<string, BuildingView> _views = new Dictionary<string, BuildingView>(64);
        readonly List<string> _stale = new List<string>(8);

        Transform _buildingRoot;
        Transform _groundRoot;
        SpriteRenderer _selectionMarker;
        Sprite _placeholder;
        int _groundCellCount;

        void Awake()
        {
            if (_runner == null) _runner = FindAnyObjectByType<GameRunner>();
            if (_selection == null) _selection = FindAnyObjectByType<TownSelection>();

            _placeholder = _placeholderSprite != null ? _placeholderSprite : UiKit.CreateSolidSprite();

            _groundRoot = new GameObject("Ground").transform;
            _groundRoot.SetParent(transform, false);

            _buildingRoot = new GameObject("Buildings").transform;
            _buildingRoot.SetParent(transform, false);

            _selectionMarker = CreateMarker();
        }

        void Start()
        {
            if (_runner != null && _runner.World != null) BuildGround();
        }

        void LateUpdate()
        {
            if (_runner == null || _runner.World == null) return;

            // Land can be bought mid-session, so the ground is rebuilt when the owned cell count
            // changes. Cheap to check, and it saves a subscription for something that happens
            // a handful of times in a whole playthrough.
            if (_runner.World.Buildings.Grid.UnlockedCellCount != _groundCellCount) BuildGround();

            Reconcile();
            UpdateSelectionMarker();
        }

        void Reconcile()
        {
            var world = _runner.World;
            var buildings = world.Buildings.All;
            var gridHeight = world.Buildings.Grid.Size.Height;
            var now = _runner.Clock.UtcNowTicks;
            var database = _runner.Database;

            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];

                if (!_views.TryGetValue(building.InstanceId, out var view))
                {
                    view = BuildingView.Create(_buildingRoot, _placeholder);
                    view.Bind(building, gridHeight);
                    _views.Add(building.InstanceId, view);
                }

                view.Place(building, gridHeight);

                world.TryGetProducer(building.InstanceId, out var producer);
                view.Refresh(building, producer, now, CropVisualsFor(database, producer?.LastRecipeId));
            }

            if (_views.Count == buildings.Count) return;

            _stale.Clear();
            foreach (var pair in _views)
            {
                if (!world.Buildings.TryGetBuilding(pair.Key, out _)) _stale.Add(pair.Key);
            }

            for (var i = 0; i < _stale.Count; i++)
            {
                _views[_stale[i]].Despawn();
                _views.Remove(_stale[i]);
            }
        }

        static IRecipeVisuals CropVisualsFor(IGameDatabase database, string recipeId)
        {
            if (database == null || string.IsNullOrEmpty(recipeId)) return null;

            return database.TryGetRecipe(recipeId, out var recipe) ? recipe as IRecipeVisuals : null;
        }

        /// <summary>
        /// Lays one tile per owned cell. Rebuilt outright when land is bought: a town is a few
        /// hundred tiles, and correctness here is worth more than an incremental update.
        /// </summary>
        void BuildGround()
        {
            for (var i = _groundRoot.childCount - 1; i >= 0; i--) Destroy(_groundRoot.GetChild(i).gameObject);

            var grid = _runner.World.Buildings.Grid;
            var size = grid.Size;
            _groundCellCount = grid.UnlockedCellCount;

            for (var y = 0; y < size.Height; y++)
            {
                for (var x = 0; x < size.Width; x++)
                {
                    var cell = new GridPosition(x, y);
                    var unlocked = grid.IsUnlocked(cell);

                    var tile = new GameObject("Tile");
                    tile.transform.SetParent(_groundRoot, false);
                    tile.transform.position = IsoGridMath.GridToWorld(x, y);
                    tile.transform.localScale = new Vector3(
                        IsoGridMath.TileWidth * 0.96f, IsoGridMath.TileHeight * 0.96f, 1f);

                    var renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = _placeholder;
                    renderer.color = unlocked ? _groundColour : _lockedGroundColour;

                    // Below every building, and behind tiles further back.
                    renderer.sortingOrder = IsoGridMath.SortingOrder(cell, size.Height) - 1;
                }
            }
        }

        SpriteRenderer CreateMarker()
        {
            var marker = new GameObject("Selection");
            marker.transform.SetParent(transform, false);

            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = _placeholder;
            renderer.color = _selectionColour;
            renderer.enabled = false;
            return renderer;
        }

        void UpdateSelectionMarker()
        {
            if (_selection == null || !_selection.HasCell)
            {
                _selectionMarker.enabled = false;
                return;
            }

            var world = _runner.World;
            var rect = new GridRect(_selection.Cell, new GridSize(1, 1));

            // A selected building highlights its whole footprint, not just the tapped tile.
            if (world.Buildings.TryGetBuilding(_selection.BuildingInstanceId, out var building))
                rect = building.Footprint;

            _selectionMarker.enabled = true;
            _selectionMarker.transform.position = IsoGridMath.RectCentreToWorld(rect);
            _selectionMarker.transform.localScale = new Vector3(
                rect.Size.Width * IsoGridMath.TileWidth, rect.Size.Height * IsoGridMath.TileHeight, 1f);

            _selectionMarker.sortingOrder = IsoGridMath.SortingOrder(
                new GridPosition(rect.MaxX, rect.MaxY), world.Buildings.Grid.Size.Height) + 2;
        }
    }
}
