using System;
using UnityEngine;

namespace AlphaTown.Data.Economy
{
    /// <summary>
    /// Inspector-facing counterpart to <see cref="CurrencyAmount"/>: designers pick an asset, the
    /// simulation gets an id. Mirrors how <see cref="Items.ItemAmount"/> pairs with ItemStack.
    ///
    /// Neutrally named because it serves both directions — a level-up reward and a build cost are
    /// the same shape.
    /// </summary>
    [Serializable]
    public sealed class CurrencyEntry
    {
        [SerializeField] CurrencyDefinition _currency;
        [SerializeField, Min(1)] int _amount = 1;

        public CurrencyDefinition Currency => _currency;
        public int Amount => _amount;
        public bool IsValid => _currency != null && _currency.HasValidId && _amount > 0;

        public CurrencyAmount ToAmount() => new CurrencyAmount(_currency != null ? _currency.Id : null, _amount);
    }
}
