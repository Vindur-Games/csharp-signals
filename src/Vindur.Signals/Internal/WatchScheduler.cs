using System.Collections.Generic;

namespace Vindur.Signals.Internal
{
    internal static class WatchScheduler
    {
        private const int MaxFlushIterations = 1000;
        private static readonly Queue<EffectNode> Queue = new();
        private static readonly HashSet<EffectNode> Enqueued = new();
        private static bool _isFlushing;

        public static void Schedule(EffectNode effect)
        {
            if (Enqueued.Add(effect))
            {
                Queue.Enqueue(effect);
            }
        }

        public static void OnNotificationPhaseEnded()
        {
            Flush();
        }

        public static void Flush()
        {
            if (_isFlushing)
            {
                return;
            }

            _isFlushing = true;
            try
            {
                var iterations = 0;
                while (Queue.Count > 0)
                {
                    iterations++;
                    
                    if (iterations > MaxFlushIterations)
                    {
                        Queue.Clear();
                        Enqueued.Clear();
                        throw new CyclicSignalDependencyException(
                            $"Cyclic effect dependencies detected: exceeded maximum flush iteration limit of {MaxFlushIterations}.");
                    }

                    EffectNode effect = Queue.Dequeue();
                    Enqueued.Remove(effect);
                    if (!effect.IsDestroyed)
                    {
                        effect.Run();
                    }
                }
            }
            finally
            {
                _isFlushing = false;
            }
        }
    }
}
