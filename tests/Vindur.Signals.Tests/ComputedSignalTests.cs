using System.Diagnostics.CodeAnalysis;
using System.Numerics;

// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Vindur.Signals.Tests
{
    /// <summary>A test suite that tests the correctness of computed signals</summary>
    public class ComputedSignalTests
    {
        [Test]
        public void ShouldCorrectlySaveAndReturnItsInitialValue()
        {
            WritableSignal<int> signal = Signal.State(18);
            Signal<int> computed = Signal.Computed(() => signal.Get() * 2);

            Assert.That(computed.Get(), Is.EqualTo(36));
        }

        [Test]
        public void ShouldComputeOnceWhenAccessedForTheFirstTime()
        {
            var counter = 0;

            WritableSignal<int> signal = Signal.State(18);

            Signal<int> computed = Signal.Computed(() =>
            {
                counter += 1;
                return signal.Get() * 2;
            });

            // the computed signal should not be computed because we haven't accessed it yet
            Assert.That(counter, Is.EqualTo(0));

            // we access the computed signal to force it to compute
            _ = computed.Get();

            // now it should've computed exactly once
            Assert.That(counter, Is.EqualTo(1));
        }

        [Test]
        public void ShouldRecomputeWhenTrackedSignalIsUpdated()
        {
            var counter = 0;

            WritableSignal<int> trackedSignal = Signal.State(42);

            Signal<int> computed = Signal.Computed(() =>
            {
                counter += 1;
                return Double(trackedSignal.Get());
            });

            // force computed signal to be computed once
            _ = computed.Get(); // force recompute
            Assert.That(counter, Is.EqualTo(1));

            // computed signal should recompute if `watchedSignal` is updated
            trackedSignal.Update(value => value + 1);
            _ = computed.Get(); // force recompute

            using (Assert.EnterMultipleScope())
            {
                Assert.That(counter, Is.EqualTo(2));

                // as a bonus, make sure the computed signal returns the correct value
                Assert.That(
                    computed.Get(),
                    Is.EqualTo(Double(trackedSignal.Get())));
            }

            return;

            int Double(int number)
            {
                return number * 2;
            }
        }

        [Test]
        public void ShouldOnlyRecomputeWhenTrackedSignalIsUpdated()
        {
            var counter = 0;

            WritableSignal<int> watchedSignal = Signal.State(42);
            WritableSignal<int> unwatchedSignal = Signal.State(18);
            WritableSignal<int> untrackedSignal = Signal.State(69);

            Signal<int> computed = Signal.Computed(() =>
            {
                counter += 1;
                return watchedSignal.Get() + Signal.Untracked(untrackedSignal);
            });

            // force computed signal to run once
            _ = computed.Get();
            Assert.That(counter, Is.EqualTo(1));

            // computed signal should recompute when `watchedSignal` is updated
            watchedSignal.Update(value => value + 1);
            _ = computed.Get(); // force recompute
            Assert.That(counter, Is.EqualTo(2));

            // computed signal should not recompute when `unwatchedSignal` is updated
            unwatchedSignal.Update(value => value + 1);
            _ = computed.Get(); // force recompute
            Assert.That(counter, Is.EqualTo(2));

            // computed signal should not recompute either when `untrackedSignal` is updated
            untrackedSignal.Update(value => value + 1);
            _ = computed.Get(); // force recompute
            Assert.That(counter, Is.EqualTo(2));
        }

        [Test]
        public void ShouldRecomputeWhenTrackedComputedSignalIsUpdated()
        {
            var counter = 0;

            WritableSignal<int> underlyingSignal = Signal.State(18);

            Signal<int> trackedComputed =
                Signal.Computed(() => Double(underlyingSignal.Get()));

            Signal<int> computed = Signal.Computed(() =>
            {
                counter += 1;
                return Double(trackedComputed.Get());
            });

            // at this point, `computed` should still only be computed once
            _ = computed.Get(); // force recompute
            Assert.That(counter, Is.EqualTo(1));

            // `computed` should recompute if `underlyingSignal` is updated
            underlyingSignal.Update(value => value * 3);
            _ = computed.Get(); // force recompute

            using (Assert.EnterMultipleScope())
            {
                Assert.That(counter, Is.EqualTo(2));

                // as a bonus, make sure `computed` returns the correct value
                Assert.That(
                    computed.Get(),
                    Is.EqualTo(Double(Double(underlyingSignal.Get()))));
            }

            return;

            int Double(int number)
            {
                return number * 2;
            }
        }

        [Test]
        public void ComputedSignalsShouldOnlyNotifyConsumersIfValueChanges()
        {
            var effectCounter = 0;

            WritableSignal<int> underlyingSignal = Signal.State(4);

            Signal<int> computed = Signal.Computed(() =>
            {
                int value = underlyingSignal.Get();
                return value * value;
            });

            using IEffectRef effectRef = Signal.Effect(() =>
            {
                effectCounter += 1;
                _ = computed.Get();
            });

            // effect should've forced the computed signal to run
            Assert.That(effectCounter, Is.EqualTo(1));

            // Semantically changing the value of the signal which will result in the semantically same
            // value for `computed`. This should force the effect to check if it has to re-run, and it
            // should realize that it doesn't have to
            underlyingSignal.Set(-4);
            Assert.That(effectCounter, Is.EqualTo(1));

            // semantically changing the value of the signal which will also semantically change the
            // value of `computed`. This should force the effect to check if it has to re-urn, and it
            // should realize that it actually has to
            underlyingSignal.Set(8);
            Assert.That(effectCounter, Is.EqualTo(2));
        }

        [Test]
        public void AChainOfComputedSignalsShouldUpdateInTheCorrectOrder()
        {
            /* create a dependency graph that kinda looks like this (leaves are writable signals and
               branches are computed signals that sum two strings):

         Test 1:      x   ""              Test 2:        x   y
                      ^   ^                              ^   ^
                       \ /                                \ /
                     (1)x   z                           (1)xy   z
                        ^   ^                              ^    ^
                       / \  |                             / \   |
                 (2)xxz--->xz(3)                  (2)xyxyz--->xyz(3)
                     ^     ^                             ^   ^
                      \   /                               \ /
                  (4)xxzxz                          (4)xyxyzxyz
             */

            WritableSignal<string> nodeX = Signal.State("x");
            WritableSignal<string> nodeY = Signal.State("");
            WritableSignal<string> nodeZ = Signal.State("z");

            Signal<string> node1 =
                Signal.Computed(() => nodeX.Get() + nodeY.Get());

            Signal<string> node3 =
                Signal.Computed(() => node1.Get() + nodeZ.Get());

            Signal<string> node2 =
                Signal.Computed(() => node1.Get() + node3.Get());

            Signal<string> node4 =
                Signal.Computed(() => node2.Get() + node3.Get());

            // assert that node 4 returns the correct value (test 1)
            Assert.That(node4.Get(), Is.EqualTo("xxzxz"));

            // assert that node 4 returns the correct value (test 2)
            nodeY.Set("y");
            Assert.That(node4.Get(), Is.EqualTo("xyxyzxyz"));
        }

        [Test]
        public void ComputedSignalWillNotNotifyUntrackedConsumers()
        {
            WritableSignal<int> trackedSignal = Signal.State(18);

            Signal<int> trackedComputed =
                Signal.Computed(() => trackedSignal.Get() * 2);

            WritableSignal<int> untrackedSignal = Signal.State(42);

            Signal<int> untrackedComputed =
                Signal.Computed(() => untrackedSignal.Get() * 2);

            var effectCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = trackedComputed.Get(); // create dependency to `trackedComputed`
                _ = Signal.Untracked(untrackedComputed); // create untracked dependency to `untrackedComputed`

                effectCounter += 1;
            });

            // at this point, the effect should've run once already
            Assert.That(effectCounter, Is.EqualTo(1));

            // the effect should run again when we update `trackedComputed`
            trackedSignal.Update(value => value + 1);
            Assert.That(effectCounter, Is.EqualTo(2));

            // the effect should not run again when we update `untrackedComputed`
            untrackedSignal.Update(value => value + 1);
            Assert.That(effectCounter, Is.EqualTo(2));
        }

        [Test]
        public void ComputedSignalsShouldHaveDynamicDependencies()
        {
            WritableSignal<bool> showCount = Signal.State(false);
            WritableSignal<int> count = Signal.State(0);

            var computedCounter = 0;

            Signal<string> computed = Signal.Computed(() =>
            {
                computedCounter += 1;

                return showCount.Get()
                    ? $"The count is {count.Get()}"
                    : "Nothing to see here";
            });

            // force `computed` to run once, just to make sure it establishes its dependencies
            _ = computed.Get();
            Assert.That(computedCounter, Is.EqualTo(1));

            // At this point, `computed` should not depend on `count` because it didn't access it in the
            // first computation. Therefore, `computed` should not re-compute if I update `count`.
            count.Update(c => c + 1);
            _ = computed.Get(); // force re-compute
            Assert.That(computedCounter, Is.EqualTo(1));

            // make `showCount` true so that `computed` now depends on `count` as well
            showCount.Set(true);
            _ = computed.Get(); // force re-compute
            Assert.That(computedCounter, Is.EqualTo(2));

            // now `computed` should re-compute if I update `count`
            count.Update(c => c + 1);
            _ = computed.Get(); // force re-compute
            Assert.That(computedCounter, Is.EqualTo(3));
        }

        [Test]
        public void DoesNotNotifyConsumerIfValueDoesNotChange()
        {
            WritableSignal<Vector2> navigateVector = Signal.State(Vector2.Zero, debugName: "signal");
            Signal<bool> isNavigatingUp = Signal.Computed(
                () => navigateVector.Get().Y > 0,
                debugName: "computed");

            var effectCounter = 0;
            Signal.Effect(() =>
            {
                _ = isNavigatingUp.Get(); // create dependency
                effectCounter += 1;
            }, debugName: "effect");

            // effect should've run once when it was created
            Assert.That(effectCounter, Is.EqualTo(1));

            // effect should run again when producer is updated
            navigateVector.Set(new Vector2(0, 1));
            Assert.That(effectCounter, Is.EqualTo(2));

            // effect should not run again when producer is updated to the same value
            navigateVector.Set(new Vector2(1, 1));
            Assert.That(effectCounter, Is.EqualTo(2));
        }

        [Test]
        public void ComputedSignalsShouldBePure()
        {
            WritableSignal<int> stateSignal = Signal.State(10);
            WritableSignal<int> sideEffectSignal = Signal.State(0);

            Signal<int> computed = Signal.Computed(() =>
            {
                sideEffectSignal.Set(42);
                return stateSignal.Get() * 2;
            });

            Assert.That(
                code: () => { _ = computed.Get(); },
                constraint: Throws.TypeOf<InvalidSignalWriteException>());
        }

        [Test]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public void CyclicComputedSignalShouldThrowCyclicSignalDependencyException()
        {
            Signal<int> computedB = null!;

            Signal<int> computedA = Signal.Computed(() => computedB.Get() + 1, debugName: "computedA");
            computedB = Signal.Computed(() => computedA.Get() + 1, debugName: "computedB");

            Assert.That(
                code: () => { _ = computedA.Get(); },
                constraint: Throws.TypeOf<CyclicSignalDependencyException>()
                    .With.Message.Contains("Detected cycle in computed signal 'computedA' execution."));
        }

        [Test]
        public void ComputedSignalAsBaseSignalSupportsImplicitConversion()
        {
            Signal<int> signal = Signal.Computed(() => 42);
            int value = signal;
            Assert.That(value, Is.EqualTo(42));
        }

        [Test]
        public void ShouldPropagateDirtyNotificationsToSubsequentEffectWhenEvaluatedBeforeEffectRegistration()
        {
            WritableSignal<int> source = Signal.State(10);
            Signal<int> computed = Signal.Computed(() => source.Get() * 2);

            // Evaluate computed signal outside an active consumer (untracked)
            int initialComputedValue = Signal.Untracked(computed);
            Assert.That(initialComputedValue, Is.EqualTo(20));

            var effectCounter = 0;
            var lastObservedValue = 0;
            Signal.Effect(() =>
            {
                lastObservedValue = computed.Get();
                effectCounter++;
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(effectCounter, Is.EqualTo(1));
                Assert.That(lastObservedValue, Is.EqualTo(20));
            }

            // When source signal updates, the effect observing the pre-evaluated computed signal should re-run
            source.Set(25);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(effectCounter, Is.EqualTo(2));
                Assert.That(lastObservedValue, Is.EqualTo(50));
            }
        }

        [Test]
        public void ShouldPropagateDirtyNotificationsThroughChainedComputedSignalsEvaluatedPriorToEffect()
        {
            WritableSignal<int> source = Signal.State(5);
            Signal<int> computedA = Signal.Computed(() => source.Get() + 10);
            Signal<int> computedB = Signal.Computed(() => computedA.Get() * 2);

            // Evaluate both computed signals prior to effect registration
            int initialA = Signal.Untracked(computedA);
            int initialB = Signal.Untracked(computedB);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(initialA, Is.EqualTo(15));
                Assert.That(initialB, Is.EqualTo(30));
            }

            var effectCounter = 0;
            var lastObservedValue = 0;
            Signal.Effect(() =>
            {
                lastObservedValue = computedB.Get();
                effectCounter++;
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(effectCounter, Is.EqualTo(1));
                Assert.That(lastObservedValue, Is.EqualTo(30));
            }

            // Mutate root state signal
            source.Set(10);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(effectCounter, Is.EqualTo(2));
                Assert.That(lastObservedValue, Is.EqualTo(40));
            }
        }
    }
}