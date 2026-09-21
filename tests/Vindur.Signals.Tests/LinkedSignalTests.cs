// ReSharper disable ReturnValueOfPureMethodIsNotUsed
// ReSharper disable AccessToModifiedClosure

using System.Numerics;

namespace Vindur.Signals.Tests
{
    /// <summary>A test suite that tests the correctness of linked signals</summary>
    public class LinkedSignalTests
    {
        /// <summary>A linked signal's initial value should be the result of its first computation</summary>
        [Test]
        public void InitialValueEqualsFirstComputationResult()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            Assert.That(linkedSignal.Get(), Is.EqualTo(36));
        }

        /// <summary>You should be able to `.Set` a linked signal's value directly</summary>
        [Test]
        public void Set()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            linkedSignal.Set(42);

            Assert.That(linkedSignal.Get(), Is.EqualTo(42));
        }

        /// <summary>You should be able to `.Update` a linked signal's value directly</summary>
        [Test]
        public void Update()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            linkedSignal.Update(value => value * 2);

            Assert.That(linkedSignal.Get(), Is.EqualTo(72));
        }

        /// <summary>
        /// A linked signal's value should reset to its computation result when its source signal's value is changed (after
        /// `.Set`)
        /// </summary>
        [Test]
        public void ResetsWhenSourceChangesValue_Set()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            linkedSignal.Set(42);

            sourceSignal.Set(100);

            Assert.That(linkedSignal.Get(), Is.EqualTo(200));
        }

        /// <summary>
        /// A linked signal's value should reset to its computation result when its source signal's value is changed (after
        /// `.Set`)
        /// </summary>
        [Test]
        public void ResetsWhenSourceChangesValue_Update()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            linkedSignal.Update(value => value * 2);

            sourceSignal.Set(100);

            Assert.That(linkedSignal.Get(), Is.EqualTo(200));
        }

        /// <summary>A linked signal should receive its previous computation's result</summary>
        [Test]
        public void ReceivesPreviousComputationResult()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            LinkedSignalPrevious<int, int>? previousValue = null;

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, prev) =>
                {
                    previousValue = prev;
                    return sourceValue * 2;
                });

            linkedSignal.Get(); // force computation to run

            sourceSignal.Set(100);

            _ = linkedSignal.Get(); // force computation to run again

            Assert.That(previousValue?.Value, Is.EqualTo(36));
        }

        /// <summary>A linked signal should receive its source signal's previous value</summary>
        [Test]
        public void ReceivesPreviousSourceValue()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            LinkedSignalPrevious<int, int>? previousValue = null;

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, prev) =>
                {
                    previousValue = prev;
                    return sourceValue * 2;
                });

            _ = linkedSignal.Get(); // force computation to run

            sourceSignal.Set(100);

            _ = linkedSignal.Get(); // force computation to run again

            Assert.That(previousValue?.Source, Is.EqualTo(18));
        }

        /// <summary>A linked signal should notify its subscribers when its value changes (`.Set`)</summary>
        [Test]
        public void NotifiesSubscribers_Set()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = linkedSignal.Get(); // create the dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Set(42);

            Assert.That(effectRunCounter, Is.EqualTo(2));
        }

        /// <summary>A linked signal should notify its subscribers when its value changes (`.Update`)</summary>
        [Test]
        public void NotifiesSubscribers_Update()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                linkedSignal.Get(); // create the dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Update(value => value * 2);

            Assert.That(effectRunCounter, Is.EqualTo(2));
        }

        /// <summary>A linked signal should notify its subscribers when its value changes (source value change)</summary>
        [Test]
        public void NotifiesSubscribers_SourceValueChange()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = linkedSignal.Get(); // create the dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            sourceSignal.Set(100);

            Assert.That(effectRunCounter, Is.EqualTo(2));
        }

        /// <summary>A linked signal should not notify its subscribers when its value doesn't semantically change (`.Set`)</summary>
        [Test]
        public void DoesNotNotifySubscribersWhenValueStaysTheSame_Set()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (_, _) => 42);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                linkedSignal.Get(); // create the dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Set(42);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>A linked signal should not notify its subscribers when its value doesn't semantically change (`.Update`)</summary>
        [Test]
        public void DoesNotNotifySubscribersWhenValueStaysTheSame_Update()
        {
            WritableSignal<Vector3> sourceSignal = Signal.State(new Vector3(1.0f, 2.0f, 3.0f));

            WritableSignal<Vector3> linkedSignal = Signal.Linked<Vector3, Vector3>(
                sourceSignal,
                (source, _) => source * 2);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = linkedSignal.Get(); // create the dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Update(value => value);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>A linked signal should not notify its subscribers when its value doesn't semantically change (source value change)</summary>
        [Test]
        public void DoesNotNotifySubscribersWhenValueStaysTheSame_SourceValueChange()
        {
            WritableSignal<Vector2> navigateVector = Signal.State(Vector2.Zero);
            Signal<bool> isNavigatingUp = Signal.Linked<Vector2, bool>(
                source: navigateVector,
                computation: (vector, _) => vector.Y > 0);

            var effectCounter = 0;
            Signal.Effect(() =>
            {
                _ = isNavigatingUp.Get(); // create dependency
                effectCounter += 1;
            });

            // effect should've run once when it was created
            Assert.That(effectCounter, Is.EqualTo(1));

            // effect should run again when producer is updated
            navigateVector.Set(new Vector2(0, 1));
            Assert.That(effectCounter, Is.EqualTo(2));

            // effect should not run again when producer is updated to the same value
            navigateVector.Set(new Vector2(1, 1));
            Assert.That(effectCounter, Is.EqualTo(2));
        }

        /// <summary>A linked signal should not notify its subscribers when it's inside an `Untracked` block (`.Set`)</summary>
        [Test]
        public void DoesNotNotifySubscribersWhenUntracked_Set()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = Signal.Untracked(linkedSignal); // create the untracked dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Set(100);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>A linked signal should not notify its subscribers when it's inside an `Untracked` block (`.Update`)</summary>
        [Test]
        public void DoesNotNotifySubscribersWhenUntracked_Update()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = Signal.Untracked(linkedSignal); // create the untracked dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Update(value => value * 2);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>A linked signal should not notify its subscribers when it's inside an `Untracked` block (source value change)</summary>
        [Test]
        public void DoesNotNotifySubscribersWhenUntracked_SourceValueChange()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2);

            var effectRunCounter = 0;

            using IEffectRef effectRef = Signal.Effect(() =>
            {
                _ = Signal.Untracked(linkedSignal); // create the untracked dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            sourceSignal.Set(100);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>A linked signal should not re-run when a value it consumes stays unchanged</summary>
        [Test]
        public void DoesNotReRunWhenConsumedValueIsUnchanged()
        {
            WritableSignal<Vector3> sourceSignal = Signal.State(new Vector3(1.0f, 2.0f, 3.0f));

            var counter = 0;

            WritableSignal<Vector3> linkedSignal = Signal.Linked<Vector3, Vector3>(
                source: sourceSignal,
                computation: (source, _) =>
                {
                    counter += 1;
                    return source;
                });

            _ = linkedSignal.Get(); // force computation to run if needed
            Assert.That(counter, Is.EqualTo(1));

            sourceSignal.Set(new Vector3(1.0f, 2.0f, 3.0f));
            _ = linkedSignal.Get(); // force computation to run if needed
            Assert.That(counter, Is.EqualTo(1));

            sourceSignal.Set(new Vector3(2.0f, 3.0f, 4.0f));
            _ = linkedSignal.Get(); // force computation to run if needed
            Assert.That(counter, Is.EqualTo(2));
        }

        /// <summary>A linked signal should honor the equality comparer if supplied by the caller (`.Set`)</summary>
        [Test]
        public void EqualityComparer_Set()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            var equalityComparer = new MockEqualityComparer();

            WritableSignal<int> linkedSignal = Signal.Linked(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2,
                equalityComparer);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = Signal.Untracked(linkedSignal); // create the untracked dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Set(100);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>A linked signal should honor the equality comparer if supplied by the caller (`.Update`)</summary>
        [Test]
        public void EqualityComparer_Update()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            var equalityComparer = new MockEqualityComparer();

            WritableSignal<int> linkedSignal = Signal.Linked(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2,
                equalityComparer);

            var effectRunCounter = 0;

            using IEffectRef effectRef = Signal.Effect(() =>
            {
                _ = Signal.Untracked(linkedSignal); // create the untracked dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            linkedSignal.Update(value => value * 2);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>A linked signal should honor the equality comparer if supplied by the caller (source value change)</summary>
        [Test]
        public void EqualityComparer_SourceValueChange()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);

            var equalityComparer = new MockEqualityComparer();

            WritableSignal<int> linkedSignal = Signal.Linked(
                sourceSignal,
                (sourceValue, _) => sourceValue * 2,
                equalityComparer);

            var effectRunCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = Signal.Untracked(linkedSignal); // create the untracked dependency
                effectRunCounter += 1;
            });

            Assert.That(effectRunCounter, Is.EqualTo(1));

            sourceSignal.Set(100);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>Writing to a linked signal inside a pure reactive context should throw InvalidSignalWriteException</summary>
        [Test]
        public void CannotWriteToLinkedSignalInsidePureContext()
        {
            WritableSignal<int> sourceSignal = Signal.State(18);
            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(sourceSignal, (source, _) => source * 2);

            Signal<int> computed = Signal.Computed(() =>
            {
                linkedSignal.Set(100);
                return 42;
            });

            Assert.That(
                code: () => computed.Get(),
                constraint: Throws.TypeOf<InvalidSignalWriteException>());
        }

        /// <summary>A cyclic dependency in a linked signal computation should throw CyclicSignalDependencyException</summary>
        [Test]
        public void CyclicLinkedSignalShouldThrowCyclicSignalDependencyException()
        {
            WritableSignal<int> linkedB = null!;

            WritableSignal<int> linkedA = Signal.Linked<int, int>(
                source: () => linkedB.Get(),
                computation: (source, _) => source + 1,
                debugName: "linkedA");

            linkedB = Signal.Linked<int, int>(
                source: linkedA.Get,
                computation: (source, _) => source + 1,
                debugName: "linkedB");

            Assert.That(
                code: () => linkedA.Get(),
                constraint: Throws.TypeOf<CyclicSignalDependencyException>()
                    .With.Message.Contains("Detected cycle in linked signal 'linkedA' execution."));
        }

        [Test]
        public void LinkedSignalAsWritableSignalSupportsImplicitConversion()
        {
            WritableSignal<int> source = Signal.State(21);
            WritableSignal<int> linked = Signal.Linked(source, s => s * 2);

            int value = linked;
            Assert.That(value, Is.EqualTo(42));
        }

        [Test]
        public void AsReadOnlyReturnsSignalWithImplicitConversion()
        {
            WritableSignal<int> source = Signal.State(21);
            WritableSignal<int> linked = Signal.Linked(source, s => s * 2);
            Signal<int> readOnly = linked.AsReadOnly();

            int value = readOnly;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(value, Is.EqualTo(42));
                Assert.That(readOnly, Is.SameAs(linked));
            }
        }
    }

    public class MockEqualityComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => true;
        public int GetHashCode(int obj) => 0;
    }
}