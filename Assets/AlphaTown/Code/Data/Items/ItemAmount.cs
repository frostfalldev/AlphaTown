using System;
using UnityEngine;

namespace AlphaTown.Data.Items
{
    /// <summary>
    /// Inspector-facing counterpart to <see cref="ItemStack"/>: designers pick an asset, the
    /// simulation gets an id. Converted once at load, never per-frame.
    /// </summary>
    [Serializable]
    public sealed class ItemAmount
    {
        [SerializeField] ItemDefinition _item;
        [SerializeField, Min(1)] int _count = 1;

        public ItemDefinition Item => _item;
        public int Count => _count;
        public bool IsValid => _item != null && _item.HasValidId && _count > 0;

        public ItemStack ToStack() => new ItemStack(_item != null ? _item.Id : null, _count);
    }
}
