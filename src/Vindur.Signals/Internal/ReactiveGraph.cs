namespace Vindur.Signals.Internal
{
    internal static class ReactiveGraph
    {
        public static long Epoch { get; private set; } = 1;
        public static ReactiveNode? ActiveConsumer { get; private set; }
        public static bool IsInNotificationPhase { get; private set; }

        public static ReactiveNode? SetActiveConsumer(ReactiveNode? consumer)
        {
            ReactiveNode? prev = ActiveConsumer;
            ActiveConsumer = consumer;
            return prev;
        }

        public static void IncrementEpoch()
        {
            Epoch++;
        }

        public static void ProducerAccessed(ReactiveNode producer)
        {
            if (IsInNotificationPhase)
            {
                throw new InvalidSignalReadException("Signal read during notification phase");
            }

            if (ActiveConsumer == null)
            {
                return;
            }

            ActiveConsumer.OnSignalRead(producer);

            ReactiveLink? link = null;

            // 1. Check if same as immediate previous producer read by active consumer
            ReactiveLink? prevProducerLink = ActiveConsumer.ProducersTail;
            if (prevProducerLink != null && prevProducerLink.Producer == producer)
            {
                link = prevProducerLink;
            }
            else
            {
                // 2. Check if the active consumer is recomputing and matches the next producer in the existing chain
                if (ActiveConsumer.IsRecomputing)
                {
                    ReactiveLink? nextProducerLink = prevProducerLink != null ? prevProducerLink.NextProducer : ActiveConsumer.Producers;
                    if (nextProducerLink != null && nextProducerLink.Producer == producer)
                    {
                        ActiveConsumer.ProducersTail = nextProducerLink;
                        link = nextProducerLink;
                    }
                }

                if (link == null)
                {
                    // 3. Search for an existing link in the current consumer's list of producers.
                    // This handles deduplication even for non-live consumers.
                    for (ReactiveLink? l = ActiveConsumer.Producers; l != null; l = l.NextProducer)
                    {
                        if (l.Producer == producer)
                        {
                            link = l;
                            break;
                        }
                    }
                }

                if (link == null)
                {
                    // 4. Create new link between producer and active consumer
                    ReactiveLink? targetNext = prevProducerLink != null ? prevProducerLink.NextProducer : ActiveConsumer.Producers;
                    link = new ReactiveLink
                    {
                        Producer = producer,
                        Consumer = ActiveConsumer,
                        NextProducer = targetNext
                    };

                    if (prevProducerLink != null)
                    {
                        prevProducerLink.NextProducer = link;
                    }
                    else
                    {
                        ActiveConsumer.Producers = link;
                    }
                    ActiveConsumer.ProducersTail = link;

                    if (ActiveConsumer.IsLive)
                    {
                        AddLiveConsumer(producer, link);
                    }
                }
                else
                {
                    // 5. Existing link found, but it wasn't the "next" link in the previous recomputation chain.
                    // If we're currently recomputing, this means the dependency order has changed or we're seeing
                    // a duplicate access.
                    if (ActiveConsumer.IsRecomputing)
                    {
                        // If it was already accessed in this recomputation, we don't need to do anything.
                        if (link.LastRecomputeId == ActiveConsumer.RecomputeId)
                        {
                            return;
                        }

                        // Otherwise, we move it to the current tail of the producers list to maintain order.
                        // First, unlink it from its current position in ActiveConsumer.Producers
                        UnlinkProducerFromConsumer(link);

                        // Then, insert it after ProducersTail
                        ReactiveLink? targetNext = prevProducerLink != null ? prevProducerLink.NextProducer : ActiveConsumer.Producers;
                        link.NextProducer = targetNext;
                        if (prevProducerLink != null)
                        {
                            prevProducerLink.NextProducer = link;
                        }
                        else
                        {
                            ActiveConsumer.Producers = link;
                        }
                        ActiveConsumer.ProducersTail = link;
                    }
                }
            }

            // Ensure producer is refreshed and record the producer's version, epoch & recompute ID
            producer.RefreshVersionIfNeeded();
            link.LastReadVersion = producer.Version;
            link.KnownValidAtEpoch = Epoch;
            link.LastRecomputeId = ActiveConsumer.RecomputeId;
        }

        private static void UnlinkProducerFromConsumer(ReactiveLink link)
        {
            ReactiveNode consumer = link.Consumer;
            if (consumer.Producers == link)
            {
                consumer.Producers = link.NextProducer;
            }
            else
            {
                for (ReactiveLink? l = consumer.Producers; l != null; l = l.NextProducer)
                {
                    if (l.NextProducer == link)
                    {
                        l.NextProducer = link.NextProducer;
                        break;
                    }
                }
            }
            link.NextProducer = null;
        }

        public static void AddLiveConsumer(ReactiveNode producer, ReactiveLink link)
        {
            if (link.PrevConsumer != null || producer.Consumers == link)
            {
                return;
            }

            bool wasLive = producer.IsLive;

            if (producer.ConsumersTail != null)
            {
                producer.ConsumersTail.NextConsumer = link;
                link.PrevConsumer = producer.ConsumersTail;
            }
            else
            {
                producer.Consumers = link;
            }

            producer.ConsumersTail = link;

            if (!wasLive && producer.IsLive)
            {
                for (ReactiveLink? p = producer.Producers; p != null; p = p.NextProducer)
                {
                    AddLiveConsumer(p.Producer, p);
                }
            }
        }

        public static void UnlinkLiveConsumer(ReactiveLink link)
        {
            ReactiveNode producer = link.Producer;
            if (link.PrevConsumer == null && producer.Consumers != link)
            {
                return;
            }

            bool wasLive = producer.IsLive;

            if (link.PrevConsumer != null)
            {
                link.PrevConsumer.NextConsumer = link.NextConsumer;
            }
            else
            {
                producer.Consumers = link.NextConsumer;
            }

            if (link.NextConsumer != null)
            {
                link.NextConsumer.PrevConsumer = link.PrevConsumer;
            }
            else
            {
                producer.ConsumersTail = link.PrevConsumer;
            }

            link.PrevConsumer = null;
            link.NextConsumer = null;

            if (wasLive && !producer.IsLive)
            {
                for (ReactiveLink? p = producer.Producers; p != null; p = p.NextProducer)
                {
                    UnlinkLiveConsumer(p);
                }
            }
        }

        public static void NotifyConsumers(ReactiveNode producer)
        {
            bool wasInNotificationPhase = IsInNotificationPhase;
            IsInNotificationPhase = true;

            try
            {
                for (ReactiveLink? link = producer.Consumers; link != null; link = link.NextConsumer)
                {
                    if (!link.Consumer.IsDirty)
                    {
                        link.Consumer.MarkDirty();
                    }
                }
            }
            finally
            {
                IsInNotificationPhase = wasInNotificationPhase;
                if (!wasInNotificationPhase)
                {
                    WatchScheduler.OnNotificationPhaseEnded();
                }
            }
        }
    }
}
