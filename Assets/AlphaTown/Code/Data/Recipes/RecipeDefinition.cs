using System;
using System.Collections.Generic;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Items;
using AlphaTown.Data.Presentation;
using UnityEngine;

namespace AlphaTown.Data.Recipes
{
    [CreateAssetMenu(menuName = "AlphaTown/Production/Recipe Definition", fileName = "Recipe_", order = 10)]
    public sealed class RecipeDefinition : GameDefinition, IRecipeDefinition, IRecipeVisuals
    {
        [SerializeField] ItemAmount[] _inputs = Array.Empty<ItemAmount>();
        [SerializeField] ItemAmount[] _outputs = Array.Empty<ItemAmount>();

        [SerializeField, Min(1)]
        [Tooltip("Seconds at producer level 1. The producer's speed multiplier is applied on top.")]
        int _durationSeconds = 60;

        [SerializeField, Min(1)] int _unlockLevel = 1;

        [Tooltip("Extra units of the first output a run may yield. 0 = always the authored count.")]
        [SerializeField, Min(0)] int _bonusOutputMax;

        [Header("Presentation")]
        [Tooltip("Presentation only. Nothing in the simulation reads these.")]
        [SerializeField] Sprite _icon;

        [Tooltip("Ordered seedling to ripe. Re-timed automatically when frames are added.")]
        [SerializeField] Sprite[] _growthStageSprites = Array.Empty<Sprite>();

        ItemStack[] _cachedInputs;
        ItemStack[] _cachedOutputs;

        public IReadOnlyList<ItemStack> Inputs => _cachedInputs ?? (_cachedInputs = BuildStacks(_inputs));
        public IReadOnlyList<ItemStack> Outputs => _cachedOutputs ?? (_cachedOutputs = BuildStacks(_outputs));
        public TimeSpan Duration => TimeSpan.FromSeconds(_durationSeconds);
        public int DurationSeconds => _durationSeconds;
        public int UnlockLevel => _unlockLevel;
        public int BonusOutputMax => _bonusOutputMax;

        public Sprite Icon => _icon;
        public Sprite[] GrowthStageSprites => _growthStageSprites;

        public Sprite StageFor(float progress)
        {
            var frames = _growthStageSprites;
            if (frames == null || frames.Length == 0) return _icon;

            var index = Mathf.FloorToInt(progress * frames.Length);
            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        void OnEnable() => InvalidateCache();

        static ItemStack[] BuildStacks(ItemAmount[] amounts)
        {
            if (amounts == null || amounts.Length == 0) return Array.Empty<ItemStack>();

            var stacks = new List<ItemStack>(amounts.Length);
            for (var i = 0; i < amounts.Length; i++)
            {
                if (amounts[i] == null || !amounts[i].IsValid) continue;
                stacks.Add(amounts[i].ToStack());
            }

            return stacks.ToArray();
        }

        void InvalidateCache()
        {
            _cachedInputs = null;
            _cachedOutputs = null;
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
