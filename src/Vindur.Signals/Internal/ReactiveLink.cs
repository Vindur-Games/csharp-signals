namespace Vindur.Signals.Internal
{
    /// <summary>
    /// Represents a directed dependency link between a producer and a consumer node in the reactive graph.
    /// </summary>
    internal class ReactiveLink
    {
        public ReactiveNode Producer { get; init; } = null!;
        public ReactiveNode Consumer { get; init; } = null!;

        /// <summary>
        /// Global epoch at which this link was last verified or observed.
        /// Used to deduplicate multiple reads of the same signal within a single evaluation epoch.
        /// </summary>
        public long KnownValidAtEpoch { get; set; }

        /// <summary>
        /// Version of the producer's value when the consumer last read it.
        /// </summary>
        public long LastReadVersion { get; set; }
        
        /// <summary>
        /// The <see cref="ReactiveNode.RecomputeId"/> of the consumer when this link was last verified.
        /// Used for O(1) deduplication of multiple reads of the same signal.
        /// </summary>
        public int LastRecomputeId { get; set; }

        /// <summary>
        /// Next link in the consumer's list of producer dependencies.
        /// </summary>
        public ReactiveLink? NextProducer { get; set; }

        /// <summary>
        /// Previous link in the producer's list of live consumers.
        /// </summary>
        public ReactiveLink? PrevConsumer { get; set; }

        /// <summary>
        /// Next link in the producer's list of live consumers.
        /// </summary>
        public ReactiveLink? NextConsumer { get; set; }
    }
}
