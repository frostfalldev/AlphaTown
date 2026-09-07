using AlphaTown.Core.Spatial;
using AlphaTown.Data.Presentation;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Production;
using UnityEngine;

namespace AlphaTown.UI.View
{
    /// <summary>
    /// One building on the map: its sprite, a growth frame if it is a field, and a badge when
    /// there is something to collect.
    ///
    /// Reads the simulation, never writes to it. Everything shown here is derived on the spot from
    /// timestamps the simulation already holds, so the view has no state to keep in sync and
    /// nothing to restore after a load — it just draws whatever the world currently says.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        SpriteRenderer _body;
        SpriteRenderer _badge;
        Sprite _placeholder;
        Sprite _baseSprite;
        Color _baseColour = Color.white;

        public string InstanceId { get; private set; }

        public static BuildingView Create(Transform parent, Sprite placeholder)
        {
            var root = new GameObject("Building");
            root.transform.SetParent(parent, false);

            var view = root.AddComponent<BuildingView>();
            view._placeholder = placeholder;
            view._body = root.AddComponent<SpriteRenderer>();

            var badge = new GameObject("Ready");
            badge.transform.SetParent(root.transform, false);
            badge.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            badge.transform.localScale = new Vector3(0.28f, 0.28f, 1f);

            view._badge = badge.AddComponent<SpriteRenderer>();
            view._badge.sprite = placeholder;
            view._badge.color = new Color(1f, 0.86f, 0.25f);
            view._badge.enabled = false;

            return view;
        }

        /// <summary>Binds to a building. Called once on spawn and again if the definition changes.</summary>
        public void Bind(BuildingInstance building, int gridHeight)
        {
            InstanceId = building.InstanceId;
            name = "Building_" + building.DefinitionId + "_" + building.InstanceId;

            var visuals = building.Definition as IBuildingVisuals;
            _baseSprite = visuals != null && visuals.MapSprite != null ? visuals.MapSprite : _placeholder;
            _baseColour = visuals != null && visuals.MapSprite == null ? visuals.PlaceholderColour : Color.white;

            _body.sprite = _baseSprite;
            _body.color = _baseColour;

            Place(building, gridHeight);
        }

        /// <summary>Positions the sprite over its footprint and sets its draw order.</summary>
        public void Place(BuildingInstance building, int gridHeight)
        {
            var footprint = building.Footprint;
            transform.position = IsoGridMath.RectCentreToWorld(footprint);

            // A placeholder sprite is one world unit square, so it is stretched to the footprint.
            // Authored art already has its own size and is left alone.
            if (_body.sprite == _placeholder)
            {
                var size = building.Definition.Footprint;
                transform.localScale = new Vector3(size.Width * 0.9f, size.Height * 0.45f, 1f);
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            // Sorted by the far corner so a building never draws in front of one behind it.
            _body.sortingOrder = IsoGridMath.SortingOrder(
                new GridPosition(footprint.MaxX, footprint.MaxY), gridHeight);

            _badge.sortingOrder = _body.sortingOrder + 1;
        }

        /// <summary>
        /// Per-frame refresh: growth frame, construction tint, ready badge.
        ///
        /// <paramref name="producer"/> is null for a building that produces nothing, which is most
        /// of them — a decoration has none of this to show.
        /// </summary>
        public void Refresh(BuildingInstance building, Producer producer, long nowTicks, IRecipeVisuals crop)
        {
            if (building.IsBusy)
            {
                // Under construction: faded, and no crop or badge on top of scaffolding.
                _body.sprite = _baseSprite;
                _body.color = new Color(_baseColour.r, _baseColour.g, _baseColour.b, 0.45f);
                _badge.enabled = false;
                return;
            }

            _body.color = _baseColour;

            if (producer == null)
            {
                _body.sprite = _baseSprite;
                _badge.enabled = false;
                return;
            }

            // A field draws the crop growing on it; the frame is chosen from progress, so adding
            // art frames re-times the animation without touching the simulation.
            if (crop != null && producer.TryGetActiveOrder(out var order))
            {
                var frame = crop.StageFor(order.Progress01(nowTicks));
                _body.sprite = frame != null ? frame : _baseSprite;
            }
            else
            {
                _body.sprite = _baseSprite;
            }

            _badge.enabled = producer.HasReadyGoods;
        }

        public void Despawn() => Destroy(gameObject);
    }
}
