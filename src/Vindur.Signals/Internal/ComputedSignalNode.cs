using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Vindur.Signals.Internal
{
    internal class ComputedSignalNode<T> : ReactiveNode
    {
        private T _value = default!;
        private bool _initialized;
        private readonly Func<T> _computation;
        private readonly IEqualityComparer<T> _equalityComparer;

        public ComputedSignalNode(Func<T> computation, IEqualityComparer<T> equalityComparer, string debugName)
        {
            _computation = computation;
            _equalityComparer = equalityComparer;
            DebugName = debugName;
            AllowSignalWrites = false;
            IsDirty = true;
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        [Pure]
        public T Get()
        {
            ReactiveGraph.ProducerAccessed(this);
            RefreshVersionIfNeeded();
            return _value;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        [Pure]
        public T Peek()
        {
            RefreshVersionIfNeeded();
            return _value;
        }

        public override void RefreshVersionIfNeeded()
        {
            if (!_initialized || CheckIfDependenciesChanged())
            {
                Recompute();
            }
        }

        private void Recompute()
        {
            if (IsRecomputing)
            {
                throw new CyclicSignalDependencyException($"Detected cycle in computed signal '{DebugName}' execution.");
            }

            PrepareForRecomputation();
            ReactiveNode? prevConsumer = ReactiveGraph.SetActiveConsumer(this);

            T newValue;
            try
            {
                newValue = _computation();
            }
            finally
            {
                ReactiveGraph.SetActiveConsumer(prevConsumer);
                FinalizeRecomputation();
            }

            if (!_initialized || !_equalityComparer.Equals(_value, newValue))
            {
                _value = newValue;
                Version++;
            }

            _initialized = true;
            LastCleanEpoch = ReactiveGraph.Epoch;
            IsDirty = false;
        }
    }
}
