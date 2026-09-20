using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Vindur.Signals.Internal
{
    internal class StateSignalNode<T> : ReactiveNode
    {
        private T _value;
        private readonly IEqualityComparer<T> _equalityComparer;

        public StateSignalNode(T initialValue, IEqualityComparer<T> equalityComparer, string debugName)
        {
            _value = initialValue;
            _equalityComparer = equalityComparer;
            DebugName = debugName;
            Version = 1;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        [Pure]
        public T Get()
        {
            ReactiveGraph.ProducerAccessed(this);
            return _value;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        [Pure]
        public T Peek()
        {
            return _value;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Set(T newValue)
        {
            if (ReactiveGraph.ActiveConsumer != null && !ReactiveGraph.ActiveConsumer.AllowSignalWrites)
            {
                throw new InvalidSignalWriteException(
                    $"Cannot write to state signal '{DebugName}' inside a pure reactive context (e.g., computed signal computation).");
            }

            if (_equalityComparer.Equals(_value, newValue))
            {
                return;
            }

            _value = newValue;
            Version++;
            ReactiveGraph.IncrementEpoch();
            ReactiveGraph.NotifyConsumers(this);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Update(Func<T, T> updateFn)
        {
            Set(updateFn(Peek()));
        }
    }
}
