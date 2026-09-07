using UnityEngine;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Interaction
{
    /// <summary>
    /// Whether a screen position lands on a HUD widget.
    ///
    /// Without this, a tap on the Deliver button also taps the tile behind it — the world reads
    /// raw pointers and knows nothing about the panel drawn over it.
    ///
    /// It only works because the HUD marks its layout containers <see cref="PickingMode.Ignore"/>.
    /// A UI Toolkit root fills the screen and is pickable by default, so testing against it
    /// unchanged would report every touch as "on the UI" and nothing in the town would ever
    /// respond again.
    /// </summary>
    public static class UiHitTest
    {
        public static bool IsOverUi(UIDocument document, Vector2 screenPosition)
        {
            var root = document != null ? document.rootVisualElement : null;
            if (root?.panel == null) return false;

            // Screen space is bottom-left origin; UI Toolkit panels are top-left. Getting this
            // flip wrong inverts the whole HUD vertically and is invisible until a button at the
            // top stops responding.
            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, flipped);

            return root.panel.Pick(panelPosition) != null;
        }
    }
}
