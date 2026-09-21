// Test suite for StateSignal


// ReSharper disable ReturnValueOfPureMethodIsNotUsed

using System.Numerics;

namespace Vindur.Signals.Tests
{
    /// <summary>A test suite that tests the correctness of state signals</summary>
    public class StateSignalTests
    {
        [Test]
        public void SignalsSaveAndReturnTheirInitialValue()
        {
            const int initialValue = 18;

            WritableSignal<int> signal = Signal.State(initialValue);

            Assert.That(signal.Get(), Is.EqualTo(initialValue));
        }

        [Test]
        public void SignalsCorrectlySetNewValue()
        {
            const int initialValue = 18;
            const int newValue = 42;

            WritableSignal<int> signal = Signal.State(initialValue);
            signal.Set(newValue);

            Assert.That(signal.Get(), Is.EqualTo(newValue));
        }

        [Test]
        public void SignalsCorrectlyUpdateTheirValue()
        {
            const int initialValue = 18;

            WritableSignal<int> signal = Signal.State(initialValue);
            signal.Update(UpdateFunction);

            Assert.That(signal.Get(), Is.EqualTo(UpdateFunction(initialValue)));

            return;

            int UpdateFunction(int number) => number + 42;
        }

        [Test]
        public void SignalsDontNotifySubscribersWhenTheirValueDoesntChange()
        {
            WritableSignal<Vector3> signal = Signal.State(new Vector3(1.0f, 2.0f, 3.0f));

            var counter = 0;

            _ = Signal.Effect(() =>
            {
                _ = signal.Get(); // create the dependency
                counter += 1;
            });

            Assert.That(counter, Is.EqualTo(1));

            signal.Set(new Vector3(1.0f, 2.0f, 3.0f));
            Assert.That(counter, Is.EqualTo(1));

            signal.Set(new Vector3(2.0f, 3.0f, 4.0f));
            Assert.That(counter, Is.EqualTo(2));
        }

        [Test]
        public void SignalsDontNotifySubscribersWhenTheyAreUntracked()
        {
            WritableSignal<int> trackedSignal = Signal.State(18);
            WritableSignal<int> untrackedSignal = Signal.State(42);

            var effectRunCounter = 0;
            using IEffectRef effectRef = Signal.Effect(() =>
            {
                // create the correct tracked/untracked dependencies
                _ =
                    $"Tracked signal's value is {trackedSignal.Get()} and untracked signal's value is {Signal.Untracked(untrackedSignal)}";

                effectRunCounter += 1;
            });

            // if I update the tracked signal, then the effect should run again
            trackedSignal.Update(value => value + 1);
            Assert.That(effectRunCounter, Is.EqualTo(2));

            // if I update the untracked signal, then the effect should not run again
            untrackedSignal.Update(value => value + 1);
            Assert.That(effectRunCounter, Is.EqualTo(2));
        }

        [Test]
        public void SignalsHonorEqualityComparerIfSuppliedByTheCaller()
        {
            WritableSignal<int> signal = Signal.State(18, new AbsoluteValueEqualityComparer());

            var effectRunCounter = 0;
            _ = Signal.Effect(() => effectRunCounter += 1);

            signal.Update(value => -value);

            Assert.That(effectRunCounter, Is.EqualTo(1));
        }

        /// <summary>An equality comparer that considers two numbers to be equal if their absolute values are so.</summary>
        private class AbsoluteValueEqualityComparer : IEqualityComparer<int>
        {
            public bool Equals(int x, int y)
            {
                return Math.Abs(x) == Math.Abs(y);
            }

            public int GetHashCode(int i)
            {
                return Math.Abs(i).GetHashCode();
            }
        }

        [Test]
        public void CannotWriteToStateSignalInsidePureContext()
        {
            WritableSignal<int> stateSignal = Signal.State(18);

            Signal<int> computed = Signal.Computed(() =>
            {
                stateSignal.Set(100);
                return 42;
            });

            Assert.That(
                code: () => { _ = computed.Get(); },
                constraint: Throws.TypeOf<InvalidSignalWriteException>());
        }

        [Test]
        public void WritableSignalSupportsImplicitConversionToValueType()
        {
            WritableSignal<int> signal = Signal.State(42);
            int value = signal;
            Assert.That(value, Is.EqualTo(42));
        }

        [Test]
        public void AsReadOnlyReturnsSignalWithImplicitConversion()
        {
            WritableSignal<int> stateSignal = Signal.State(42);
            Signal<int> readOnly = stateSignal.AsReadOnly();

            int value = readOnly;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(value, Is.EqualTo(42));
                Assert.That(readOnly, Is.SameAs(stateSignal));
            }
        }

        [Test]
        public void ImplicitConversionThrowsArgumentNullExceptionWhenSignalIsNull()
        {
            Signal<int>? nullSignal = null;
            Assert.That(
                code: () =>
                {
                    int _ = nullSignal!;
                },
                constraint: Throws.TypeOf<ArgumentNullException>());
        }
    }
}