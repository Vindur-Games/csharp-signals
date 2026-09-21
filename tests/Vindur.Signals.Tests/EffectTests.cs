using System.Numerics;
using System.Reflection;

// ReSharper disable ReturnValueOfPureMethodIsNotUsed
// ReSharper disable NotAccessedVariable

namespace Vindur.Signals.Tests
{
    public class EffectTests
    {
        [Test]
        public void ShouldBeRunExactlyOnceWhenConstructed()
        {
            var effectCounter = 0;

            _ = Signal.Effect(() => effectCounter += 1);

            Assert.That(effectCounter, Is.EqualTo(1));
        }

        [Test]
        public void ShouldReRunWhenTrackedSignalIsUpdated()
        {
            WritableSignal<int> signal = Signal.State(18);

            var effectCounter = 0;

            _ = Signal.Effect(() =>
            {
                signal.Get();
                effectCounter += 1;
            });

            signal.Update(value => value + 1);

            Assert.That(effectCounter, Is.EqualTo(2));
        }

        [Test]
        public void ShouldReRunWhenTrackedComputedSignalIsUpdated()
        {
            WritableSignal<int> signal = Signal.State(18);
            Signal<int> computed = Signal.Computed(() => signal.Get() * 2);

            var effectCounter = 0;

            _ = Signal.Effect(() =>
            {
                computed.Get();
                effectCounter += 1;
            });

            signal.Update(value => value + 1);

            Assert.That(effectCounter, Is.EqualTo(2));
        }

        [Test]
        public void ShouldNotReRunWhenUntrackedSignalIsUpdated()
        {
            WritableSignal<int> signal = Signal.State(18);

            var effectCounter = 0;

            _ = Signal.Effect(() =>
            {
                Signal.Untracked(signal);
                effectCounter += 1;
            });

            signal.Update(value => value + 1);

            Assert.That(effectCounter, Is.EqualTo(1));
        }

        [Test]
        public void ShouldNotReRunWhenUntrackedComputedSignalIsUpdated()
        {
            WritableSignal<int> signal = Signal.State(18);
            Signal<int> computed = Signal.Computed(() => signal.Get() * 2);

            var effectCounter = 0;

            _ = Signal.Effect(() =>
            {
                _ = Signal.Untracked(computed);
                effectCounter += 1;
            });

            signal.Update(value => value + 1);

            Assert.That(effectCounter, Is.EqualTo(1));
        }

        [Test]
        public void ShouldNotReRunWhenUnwatchedSignalIsUpdated()
        {
            WritableSignal<int> signal = Signal.State(18);

            var effectCounter = 0;

            _ = Signal.Effect(() => effectCounter += 1);

            signal.Update(value => value + 1);

            Assert.That(effectCounter, Is.EqualTo(1));
        }

        [Test]
        public void ShouldNotReRunWhenConsumedValueIsUnchanged()
        {
            WritableSignal<Vector3> signal = Signal.State(new Vector3(1.0f, 2.0f, 3.0f));

            var counter = 0;

            _ = Signal.Effect(() =>
            {
                signal.Get(); // create the dependency
                counter += 1;
            });

            Assert.That(counter, Is.EqualTo(1));

            signal.Set(new Vector3(1.0f, 2.0f, 3.0f));
            Assert.That(counter, Is.EqualTo(1));

            signal.Set(new Vector3(4.0f, 5.0f, 6.0f));
            Assert.That(counter, Is.EqualTo(2));
        }

        [Test]
        public void ShouldBeAbleToHaveMultipleEffectsSubscribedToTheSameSignal()
        {
            WritableSignal<int> signal = Signal.State(18);

            var effectCounter = 0;

            _ = Signal.Effect(() =>
            {
                signal.Get();
                effectCounter += 1;
            });

            _ = Signal.Effect(() =>
            {
                _ = signal.Get();
                effectCounter += 1;
            });

            Assert.That(effectCounter, Is.EqualTo(2));

            signal.Update(value => value + 1);

            Assert.That(effectCounter, Is.EqualTo(4));
        }

        [Test]
        public void CleanupFunctionShouldNotRunWhenEffectIsCreated()
        {
            var cleanupCounter = 0;

            _ = Signal.Effect(onCleanup => { onCleanup(() => cleanupCounter += 1); });

            Assert.That(cleanupCounter, Is.EqualTo(0));
        }

        [Test]
        public void CleanupFunctionShouldRunBeforeReRuns()
        {
            // it is enough to test the 2nd and 3rd run of an effect

            WritableSignal<int> signal = Signal.State(18);

            var cleanupCounter = 0;

            _ = Signal.Effect(onCleanup =>
            {
                _ = signal.Get();

                onCleanup(() => cleanupCounter += 1);
            });

            signal.Update(value => value + 1);

            Assert.That(cleanupCounter, Is.EqualTo(1));

            signal.Update(value => value + 1);

            Assert.That(cleanupCounter, Is.EqualTo(2));
        }

        [Test]
        public void CleanupFunctionShouldRunWhenEffectIsDestroyed()
        {
            var cleanupCounter = 0;

            IEffectRef effectRef = Signal.Effect(onCleanup => { onCleanup(() => cleanupCounter += 1); });

            effectRef.Destroy();

            Assert.That(cleanupCounter, Is.EqualTo(1));
        }

        [Test]
        public void EffectsShouldAllowSignalWritesByDefault()
        {
            WritableSignal<int> signal = Signal.State(18);

            Assert.That(
                () => { _ = Signal.Effect(() => signal.Update(value => value + 1)); },
                Throws.Nothing);
        }

        [Test]
        public void EffectsCanDisallowSignalWritesExplicitly()
        {
            WritableSignal<int> signal = Signal.State(18);

            Assert.That(
                () =>
                {
                    _ = Signal.Effect(
                        () => signal.Update(value => value + 1),
                        false);
                },
                Throws.TypeOf<InvalidSignalWriteException>());
        }

        [Test]
        public void CyclicEffectsShouldThrowCyclicSignalDependencyException()
        {
            WritableSignal<int> signal = Signal.State(0);

            Assert.That(
                () =>
                {
                    _ = Signal.Effect(() =>
                    {
                        int current = signal.Get();
                        signal.Set(current + 1);
                    });
                },
                Throws.TypeOf<CyclicSignalDependencyException>()
                    .With.Message.Contains("Cyclic effect dependencies detected"));
        }

        [Test]
        public void EffectsShouldHaveDynamicDependencies()
        {
            WritableSignal<bool> showCount = Signal.State(false);
            WritableSignal<int> count = Signal.State(0);

            var message = "";
            var effectCounter = 0;

            _ = Signal.Effect(() =>
            {
                effectCounter += 1;

                message = showCount.Get() ? $"The count is {count.Get()}" : "Nothing to see here";
            });

            // The effect should've run once when created. At this point, it should not depend on
            // `count` because it didn't access it in the first run. Therefore, the effect should not
            // re-run if I update `count`.
            Assert.That(effectCounter, Is.EqualTo(1));
            count.Update(c => c + 1);
            Assert.That(effectCounter, Is.EqualTo(1));

            // make `showCount` true so the effect now depends on `count` as well
            showCount.Set(true);
            Assert.That(effectCounter, Is.EqualTo(2));

            // now the effect should re-run if I update `count`
            count.Update(c => c + 1);
            Assert.That(effectCounter, Is.EqualTo(3));
        }

        [Test]
        public void DestroyingEffectShouldUnlinkLiveConsumersFromProducers()
        {
            WritableSignal<int> state = Signal.State(42);
            Signal<int> computed = Signal.Computed(() => state.Get() * 2);

            var effectCounter = 0;
            IEffectRef effect = Signal.Effect(() =>
            {
                _ = computed.Get();
                effectCounter++;
            });

            Assert.That(effectCounter, Is.EqualTo(1));

            effect.Destroy();

            // Updating the source state after destruction should not trigger the effect
            state.Set(100);
            Assert.That(effectCounter, Is.EqualTo(1));

            // Verify that state and computed signals have no remaining live consumers
            object stateNode = state
                .GetType()
                .GetField(
                    "_node",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                !.GetValue(state)!;
            object computedNode = computed
                .GetType()
                .GetField(
                    "_node",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                !.GetValue(computed)!;

            object? stateConsumers = stateNode.GetType().GetProperty("Consumers")!.GetValue(stateNode, null);
            object? computedConsumers = computedNode.GetType().GetProperty("Consumers")!.GetValue(computedNode, null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(stateConsumers, Is.Null);
                Assert.That(computedConsumers, Is.Null);
            }
        }
    }
}