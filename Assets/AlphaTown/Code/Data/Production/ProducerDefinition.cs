using System;
using System.Collections.Generic;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Recipes;
using UnityEngine;

namespace AlphaTown.Data.Production
{
    [CreateAssetMenu(menuName = "AlphaTown/Production/Producer Definition", fileName = "Producer_", order = 11)]
    public sealed class ProducerDefinition : GameDefinition, IProducerDefinition
    {
        [SerializeField] string _displayNameKey;
        [SerializeField] RecipeDefinition[] _recipes = Array.Empty<RecipeDefinition>();

        [SerializeField]
        [Tooltip("Element 0 is level 1. Every producer needs at least one level.")]
        ProducerLevel[] _levels = new ProducerLevel[1];

        IRecipeDefinition[] _cachedRecipes;

        public string DisplayNameKey => _displayNameKey;
        public int MaxLevel => _levels != null && _levels.Length > 0 ? _levels.Length : 1;

        public IReadOnlyList<IRecipeDefinition> Recipes =>
            _cachedRecipes ?? (_cachedRecipes = BuildRecipes());

        public IProducerLevel GetLevel(int level)
        {
            if (_levels == null || _levels.Length == 0) return FallbackLevel;

            var index = Mathf.Clamp(level, 1, _levels.Length) - 1;
            return _levels[index] ?? FallbackLevel;
        }

        static readonly ProducerLevel FallbackLevel = new ProducerLevel();

        void OnEnable() => _cachedRecipes = null;

        IRecipeDefinition[] BuildRecipes()
        {
            if (_recipes == null || _recipes.Length == 0) return Array.Empty<IRecipeDefinition>();

            var recipes = new List<IRecipeDefinition>(_recipes.Length);
            for (var i = 0; i < _recipes.Length; i++)
            {
                if (_recipes[i] == null) continue;
                recipes.Add(_recipes[i]);
            }

            return recipes.ToArray();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (string.IsNullOrWhiteSpace(_displayNameKey)) _displayNameKey = "producer." + Id;
            if (_levels == null || _levels.Length == 0) _levels = new ProducerLevel[1];
            _cachedRecipes = null;
        }
#endif
    }
}
