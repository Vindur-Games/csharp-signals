using System;

namespace Vindur.Signals
{
    /// <summary>
    /// Represents a handle to a registered reactive effect.
    /// </summary>
    public interface IEffectRef : IDisposable
    {
        /// <summary>
        /// Indicates whether this effect has been destroyed.
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// Manually destroys the effect, unregistering its reactive dependencies.
        /// </summary>
        void Destroy();
    }
}
