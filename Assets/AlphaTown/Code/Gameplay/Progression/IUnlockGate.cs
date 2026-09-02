using AlphaTown.Data.Recipes;

namespace AlphaTown.Gameplay.Progression
{
    /// <summary>
    /// Answers "can the player do this yet?".
    ///
    /// A narrow interface on purpose: production takes this rather than the whole progression
    /// system, so a producer can be tested against a fixed level without a curve, a wallet or an
    /// event bus in sight.
    /// </summary>
    public interface IUnlockGate
    {
        int TownLevel { get; }

        bool IsUnlocked(int requiredLevel);

        bool IsRecipeUnlocked(IRecipeDefinition recipe);
    }
}
