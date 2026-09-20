namespace Vindur.Signals.Internal
{
    /// <summary>
    /// Base node representing a participant (producer, consumer, or both) in the reactive graph.
    /// </summary>
    internal abstract class ReactiveNode
    {
        public long Version { get; protected set; }
        public long LastCleanEpoch { get; set; }
        public int RecomputeId { get; private set; }
        public bool IsDirty { get; set; }
        public bool IsRecomputing { get; set; }
        public bool AllowSignalWrites { get; protected set; }
        public virtual bool IsLive => Consumers != null;

        public ReactiveLink? Producers { get; set; }
        public ReactiveLink? ProducersTail { get; set; }

        public ReactiveLink? Consumers { get; set; }
        public ReactiveLink? ConsumersTail { get; set; }

        public string DebugName { get; set; } = string.Empty;

        public virtual void RefreshVersionIfNeeded()
        {
        }

        public virtual void MarkDirty()
        {
            if (IsDirty)
            {
                return;
            }

            IsDirty = true;
            ReactiveGraph.NotifyConsumers(this);
        }

        public virtual void OnSignalRead(ReactiveNode producerNode)
        {
        }

        public void PrepareForRecomputation()
        {
            RecomputeId++;
            ProducersTail = null;
            IsRecomputing = true;
        }

        public void FinalizeRecomputation()
        {
            if (!IsRecomputing)
            {
                return;
            }

            ReactiveLink? unused = ProducersTail != null ? ProducersTail.NextProducer : Producers;
            if (unused != null)
            {
                if (ProducersTail != null)
                {
                    ProducersTail.NextProducer = null;
                }
                else
                {
                    Producers = null;
                }

                while (unused != null)
                {
                    ReactiveLink? next = unused.NextProducer;
                    if (IsLive)
                    {
                        ReactiveGraph.UnlinkLiveConsumer(unused);
                    }
                    unused = next;
                }
            }

            IsRecomputing = false;
        }

        public bool CheckIfDependenciesChanged()
        {
            if (!IsDirty && LastCleanEpoch == ReactiveGraph.Epoch)
            {
                return false;
            }

            for (ReactiveLink? link = Producers; link != null; link = link.NextProducer)
            {
                link.Producer.RefreshVersionIfNeeded();
                if (link.Producer.Version != link.LastReadVersion)
                {
                    return true;
                }
            }

            LastCleanEpoch = ReactiveGraph.Epoch;
            IsDirty = false;
            return false;
        }

        public virtual void Destroy()
        {
            PrepareForRecomputation();
            ProducersTail = null;
            FinalizeRecomputation();
        }
    }
}
