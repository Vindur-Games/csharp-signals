using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Vindur.Signals.Internal
{
    internal class LinkedSignalNode<TSource, TValue> : ReactiveNode
    {
        private TValue _value = default!;
        private TSource _sourceValue = default!;
        private bool _initialized;
        private readonly Func<TSource> _source;
        private readonly Func<TSource, LinkedSignalPrevious<TSource, TValue>?, TValue> _computation;
        private readonly IEqualityComparer<TValue> _equalityComparer;

        public LinkedSignalNode(
            Func<TSource> source,
            Func<TSource, LinkedSignalPrevious<TSource, TValue>?, TValue> computation,
            IEqualityComparer<TValue> equalityComparer,
            string debugName)
        {
            _source = source;
            _computation = computation;
            _equalityComparer = equalityComparer;
            DebugName = debugName;
            AllowSignalWrites = false;
            IsDirty = true;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        [Pure]
        public TValue Get()
        {
            ReactiveGraph.ProducerAccessed(this);
            RefreshVersionIfNeeded();
            return _value;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        [Pure]
        public TValue Peek()
        {
            RefreshVersionIfNeeded();
            return _value;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Set(TValue newValue)
        {
            if (ReactiveGraph.ActiveConsumer != null && !ReactiveGraph.ActiveConsumer.AllowSignalWrites)
            {
                throw new InvalidSignalWriteException(
                    $"Cannot write to linked signal '{DebugName}' inside a pure reactive context.");
            }

            if (Producers == null)
            {
                ReactiveNode? prev = ReactiveGraph.SetActiveConsumer(this);
                try
                {
                    _sourceValue = _source();
                }
                finally
                {
                    ReactiveGraph.SetActiveConsumer(prev);
                }
            }

            if (_initialized && _equalityComparer.Equals(_value, newValue))
            {
                return;
            }

            _value = newValue;
            _initialized = true;
            Version++;
            LastCleanEpoch = ReactiveGraph.Epoch;
            IsDirty = false;
            ReactiveGraph.IncrementEpoch();
            ReactiveGraph.NotifyConsumers(this);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Update(Func<TValue, TValue> updateFn)
        {
            Set(updateFn(Peek()));
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
                throw new CyclicSignalDependencyException($"Detected cycle in linked signal '{DebugName}' execution.");
            }

            PrepareForRecomputation();
            ReactiveNode? prevConsumer = ReactiveGraph.SetActiveConsumer(this);

            TValue newValue;
            try
            {
                TSource newSourceValue = _source();
                LinkedSignalPrevious<TSource, TValue>? previous = _initialized
                    ? new LinkedSignalPrevious<TSource, TValue>(_sourceValue, _value)
                    : null;

                newValue = _computation(newSourceValue, previous);
                _sourceValue = newSourceValue;
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
