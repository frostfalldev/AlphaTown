using System;
using AlphaTown.Core.Spatial;
using UnityEngine;

namespace AlphaTown.UI.Selection
{
    /// <summary>
    /// What the player last tapped: a cell, and the building standing on it if there is one.
    ///
    /// Shared state deliberately kept tiny and in one place. Input writes it, the HUD and the town
    /// view read it. It is a scene component rather than a static so a test scene can hold two
    /// towns and neither of them fights over a singleton.
    /// </summary>
    public sealed class TownSelection : MonoBehaviour
    {
        /// <summary>Fires after every change, including a clear.</summary>
        public event Action Changed;

        public GridPosition Cell { get; private set; }

        /// <summary>Empty when the tap landed on bare ground.</summary>
        public string BuildingInstanceId { get; private set; } = string.Empty;

        public bool HasCell { get; private set; }

        public bool HasBuilding => !string.IsNullOrEmpty(BuildingInstanceId);

        public void Select(GridPosition cell, string buildingInstanceId)
        {
            var id = buildingInstanceId ?? string.Empty;
            if (HasCell && Cell.Equals(cell) && BuildingInstanceId == id) return;

            Cell = cell;
            BuildingInstanceId = id;
            HasCell = true;
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (!HasCell && !HasBuilding) return;

            HasCell = false;
            BuildingInstanceId = string.Empty;
            Changed?.Invoke();
        }

        /// <summary>
        /// Called when the selected building is demolished or replaced. Keeps the cell, so the
        /// panel can offer to build something there rather than snapping shut under the player.
        /// </summary>
        public void ForgetBuilding()
        {
            if (!HasBuilding) return;

            BuildingInstanceId = string.Empty;
            Changed?.Invoke();
        }
    }
}
