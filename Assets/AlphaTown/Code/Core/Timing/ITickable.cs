namespace AlphaTown.Core.Timing
{
    /// <summary>
    /// A system that wants a periodic pump. Implementations must stay cheap: production and
    /// other time-gated state is derived from timestamps, so ticking is for polling completion
    /// and driving presentation, never for accumulating progress.
    /// </summary>
    public interface ITickable
    {
        void Tick(float deltaSeconds);
    }
}
