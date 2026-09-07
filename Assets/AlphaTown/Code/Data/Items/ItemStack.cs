using System;

namespace AlphaTown.Data.Items
{
    /// <summary>
    /// A quantity of one item, identified by id rather than by asset reference so the same type
    /// serves runtime, save data and network payloads without conversion.
    /// </summary>
    [Serializable]
    public readonly struct ItemStack : IEquatable<ItemStack>
    {
        public readonly string ItemId;
        public readonly int Count;

        public ItemStack(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;

        public ItemStack WithCount(int count) => new ItemStack(ItemId, count);

        public ItemStack Scaled(int multiplier) => new ItemStack(ItemId, Count * multiplier);

        public bool Equals(ItemStack other) =>
            Count == other.Count && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ItemId != null ? ItemId.GetHashCode() : 0) * 397) ^ Count;
            }
        }

        public override string ToString() => Count + "x " + (ItemId ?? "<none>");
    }
}
