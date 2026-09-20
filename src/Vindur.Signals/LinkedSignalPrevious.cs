namespace Vindur.Signals
{
    /// <summary>
    /// Represents the previous source and target state of a linked signal when computation is triggered by source changes.
    /// </summary>
    /// <typeparam name="TSource">Source signal value type.</typeparam>
    /// <typeparam name="TValue">Linked signal value type.</typeparam>
    public readonly struct LinkedSignalPrevious<TSource, TValue>
    {
        /// <summary>
        /// Gets the previous source signal value.
        /// </summary>
        public TSource Source { get; }

        /// <summary>
        /// Gets the previous linked signal value.
        /// </summary>
        public TValue Value { get; }

        public LinkedSignalPrevious(TSource source, TValue value)
        {
            Source = source;
            Value = value;
        }
    }
}
