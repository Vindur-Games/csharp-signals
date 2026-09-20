using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Vindur.Signals.Internal;

namespace Vindur.Signals
{
    /// <summary>
    /// Abstract base class for read-only reactive signals, providing implicit conversion to the underlying value type.
    /// </summary>
    /// <typeparam name="T">The type of value wrapped by the signal.</typeparam>
    public abstract class Signal<T>
    {
        public virtual T Value => Get();
        [Pure]
        public abstract T Get();
        [Pure]
        public abstract T Peek();

        /// <summary>
        /// Implicitly converts the signal to its current value in a reactive context.
        /// Throws <see cref="ArgumentNullException"/> if <paramref name="signal"/> is null.
        /// </summary>
        public static implicit operator T(Signal<T> signal)
        {
            return signal == null
                ? throw new ArgumentNullException(nameof(signal))
                : signal.Value;
        }
    }

    /// <summary>
    /// Abstract base class for writable reactive signals whose value can be set or updated directly.
    /// </summary>
    /// <typeparam name="T">The type of value wrapped by the signal.</typeparam>
    public abstract class WritableSignal<T> : Signal<T>
    {
        /// <summary>
        /// Gets or sets the value of the signal.
        /// Reading registers a dependency; setting notifies dependents if value changed.
        /// </summary>
        public new abstract T Value { get; set; }

        /// <summary>
        /// Sets the value of the signal.
        /// </summary>
        public abstract void Set(T value);

        /// <summary>
        /// Updates the value of the signal using an update function based on current value.
        /// </summary>
        public abstract void Update(Func<T, T> updateFn);

        /// <summary>
        /// Exposes this writable signal as a read-only signal.
        /// </summary>
        [Pure]
        public abstract Signal<T> AsReadOnly();
    }

    /// <summary>
    /// Implementation of a writable state signal.
    /// </summary>
    public sealed class StateSignal<T> : WritableSignal<T>
    {
        private readonly StateSignalNode<T> _node;

        internal StateSignal(T initialValue, IEqualityComparer<T> equalityComparer, string debugName)
        {
            _node = new StateSignalNode<T>(initialValue, equalityComparer, debugName);
        }

        public override T Value
        {
            get => _node.Get();
            set => _node.Set(value);
        }

        [Pure]
        public override T Get() => _node.Get();
        [Pure]
        public override T Peek() => _node.Peek();

        public override void Set(T value) => _node.Set(value);
        public override void Update(Func<T, T> updateFn) => _node.Update(updateFn);

        [Pure]
        public override Signal<T> AsReadOnly() => this;
    }

    /// <summary>
    /// Implementation of a computed signal.
    /// </summary>
    public sealed class ComputedSignal<T> : Signal<T>
    {
        private readonly ComputedSignalNode<T> _node;

        internal ComputedSignal(Func<T> computation, IEqualityComparer<T> equalityComparer, string debugName)
        {
            _node = new ComputedSignalNode<T>(computation, equalityComparer, debugName);
        }

        public override T Value => _node.Get();
        [Pure]
        public override T Get() => _node.Get();
        [Pure]
        public override T Peek() => _node.Peek();
    }

    /// <summary>
    /// Implementation of a linked signal.
    /// </summary>
    public sealed class LinkedSignal<TSource, TValue> : WritableSignal<TValue>
    {
        private readonly LinkedSignalNode<TSource, TValue> _node;

        internal LinkedSignal(
            Func<TSource> source,
            Func<TSource, LinkedSignalPrevious<TSource, TValue>?, TValue> computation,
            IEqualityComparer<TValue> equalityComparer,
            string debugName)
        {
            _node = new LinkedSignalNode<TSource, TValue>(source, computation, equalityComparer, debugName);
        }

        public override TValue Value
        {
            get => _node.Get();
            set => _node.Set(value);
        }

        [Pure]
        public override TValue Get() => _node.Get();
        [Pure]
        public override TValue Peek() => _node.Peek();

        public override void Set(TValue value) => _node.Set(value);
        public override void Update(Func<TValue, TValue> updateFn) => _node.Update(updateFn);

        [Pure]
        public override Signal<TValue> AsReadOnly() => this;
    }

    /// <summary>
    /// Static factory methods for creating signals, effects, and untracked evaluations.
    /// </summary>
    public static class Signal
    {
        /// <summary>
        /// Creates a writable state signal initialized with <paramref name="initialValue"/>.
        /// </summary>
        public static StateSignal<T> State<T>(
            T initialValue,
            IEqualityComparer<T>? equalityComparer = null,
            string debugName = "")
        {
            return new StateSignal<T>(initialValue, equalityComparer ?? EqualityComparer<T>.Default, debugName);
        }

        /// <summary>
        /// Creates a computed signal that lazily computes its value using <paramref name="computation"/>.
        /// </summary>
        public static ComputedSignal<T> Computed<T>(
            Func<T> computation,
            IEqualityComparer<T>? equalityComparer = null,
            string debugName = "")
        {
            return new ComputedSignal<T>(computation, equalityComparer ?? EqualityComparer<T>.Default, debugName);
        }

        /// <summary>
        /// Creates a linked signal whose default value is linked to a source signal.
        /// </summary>
        public static LinkedSignal<TSource, TValue> Linked<TSource, TValue>(
            Signal<TSource> source,
            Func<TSource, TValue> computation,
            IEqualityComparer<TValue>? equalityComparer = null,
            string debugName = "")
        {
            return new LinkedSignal<TSource, TValue>(
                source.Get,
                (src, _) => computation(src),
                equalityComparer ?? EqualityComparer<TValue>.Default,
                debugName);
        }

        /// <summary>
        /// Creates a linked signal whose default value is linked to a source function.
        /// </summary>
        public static LinkedSignal<TSource, TValue> Linked<TSource, TValue>(
            Func<TSource> source,
            Func<TSource, TValue> computation,
            IEqualityComparer<TValue>? equalityComparer = null,
            string debugName = "")
        {
            return new LinkedSignal<TSource, TValue>(
                source,
                (src, _) => computation(src),
                equalityComparer ?? EqualityComparer<TValue>.Default,
                debugName);
        }

        /// <summary>
        /// Creates a linked signal with access to previous source and target values.
        /// </summary>
        public static LinkedSignal<TSource, TValue> Linked<TSource, TValue>(
            Signal<TSource> source,
            Func<TSource, LinkedSignalPrevious<TSource, TValue>?, TValue> computation,
            IEqualityComparer<TValue>? equalityComparer = null,
            string debugName = "")
        {
            return new LinkedSignal<TSource, TValue>(
                source.Get,
                computation,
                equalityComparer ?? EqualityComparer<TValue>.Default,
                debugName);
        }

        /// <summary>
        /// Creates a linked signal with access to previous source and target values from a source function.
        /// </summary>
        public static LinkedSignal<TSource, TValue> Linked<TSource, TValue>(
            Func<TSource> source,
            Func<TSource, LinkedSignalPrevious<TSource, TValue>?, TValue> computation,
            IEqualityComparer<TValue>? equalityComparer = null,
            string debugName = "")
        {
            return new LinkedSignal<TSource, TValue>(
                source,
                computation,
                equalityComparer ?? EqualityComparer<TValue>.Default,
                debugName);
        }

        /// <summary>
        /// Creates a linked signal derived from a dynamic computation function.
        /// </summary>
        public static LinkedSignal<TValue, TValue> Linked<TValue>(
            Func<TValue> computation,
            IEqualityComparer<TValue>? equalityComparer = null,
            string debugName = "")
        {
            return new LinkedSignal<TValue, TValue>(
                computation,
                (val, _) => val,
                equalityComparer ?? EqualityComparer<TValue>.Default,
                debugName);
        }

        /// <summary>
        /// Registers a side effect function that executes automatically when its signal dependencies update.
        /// </summary>
        public static IEffectRef Effect(
            Action effectFn,
            bool allowSignalWrites = true,
            string debugName = "")
        {
            var node = new EffectNode(_ => effectFn(), allowSignalWrites, debugName);
            WatchScheduler.Schedule(node);
            WatchScheduler.Flush();
            return node;
        }

        /// <summary>
        /// Registers a side effect function with a cleanup callback registrar.
        /// </summary>
        public static IEffectRef Effect(
            Action<Action<Action>> effectFn,
            bool allowSignalWrites = true,
            string debugName = "")
        {
            var node = new EffectNode(effectFn, allowSignalWrites, debugName);
            WatchScheduler.Schedule(node);
            WatchScheduler.Flush();
            return node;
        }

        /// <summary>
        /// Executes <paramref name="function"/> outside the active reactive context.
        /// </summary>
        public static T Untracked<T>(Func<T> function)
        {
            ReactiveNode? prev = ReactiveGraph.SetActiveConsumer(null);
            try
            {
                return function();
            }
            finally
            {
                ReactiveGraph.SetActiveConsumer(prev);
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Executes <paramref name="action"/> outside the active reactive context.
        /// </summary>
        public static void Untracked(Action action)
        {
            ReactiveNode? prev = ReactiveGraph.SetActiveConsumer(null);
            try
            {
                action();
            }
            finally
            {
                ReactiveGraph.SetActiveConsumer(prev);
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Reads <paramref name="signal"/> without registering a dependency in the active reactive context.
        /// </summary>
        [Pure]
        public static T Untracked<T>(Signal<T> signal)
        {
            return signal.Peek();
        }
    }
}
