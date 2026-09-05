using UnityEngine;

namespace AlphaTown.Data.Definitions
{
    /// <summary>
    /// Base for every designer-authored definition asset.
    ///
    /// <see cref="Id"/> is written into save files and live-ops payloads, so it is a serialized
    /// field rather than the asset name: renaming an asset must never invalidate a player's save.
    /// </summary>
    public abstract class GameDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Stable identifier. Written into save data — never change it after release.")]
        string _id;

        public string Id => _id;

        public bool HasValidId => !string.IsNullOrWhiteSpace(_id);

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Seed from the asset name on first authoring, then leave it alone forever.
            if (string.IsNullOrWhiteSpace(_id)) _id = name;
        }
#endif
    }
}
