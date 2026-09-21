// ReSharper disable ReturnValueOfPureMethodIsNotUsed
// ReSharper disable NotAccessedVariable

namespace Vindur.Signals.Tests
{
    /// <summary>
    /// A test suite that tests that dependencies between signals are deduplicated correctly
    /// in reactive contexts (computed signals, effects, and linked signals).
    /// </summary>
    public class DeduplicationTests
    {
        [Test]
        public void ReadingSameSignalMultipleTimesInComputedDoesNotCreateDuplicateDependencies()
        {
            WritableSignal<int> source = Signal.State(10);
            var evalCount = 0;

            // Computed accesses `source` 3 times in a single calculation
            Signal<int> triple = Signal.Computed(() =>
            {
                evalCount++;
                return source.Get() + source.Get() + source.Get();
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(triple.Get(), Is.EqualTo(30));
                Assert.That(evalCount, Is.EqualTo(1));
            }

            // Changing source should notify dependent only ONCE and trigger re-evaluation only ONCE
            source.Set(20);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(triple.Get(), Is.EqualTo(60));
                Assert.That(evalCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void NonConsecutiveReadsOfSameSignalInSingleReactiveContextAreDeduplicated()
        {
            WritableSignal<int> s1 = Signal.State(1);
            WritableSignal<int> s2 = Signal.State(2);

            var effectRunCount = 0;

            // Interleaved reads of s1: s1 -> s2 -> s1
            _ = Signal.Effect(() =>
            {
                effectRunCount++;
                _ = s1.Get() + s2.Get() + s1.Get();
            });

            Assert.That(effectRunCount, Is.EqualTo(1));

            // Updating s1 triggers effect exactly once
            s1.Set(10);
            Assert.That(effectRunCount, Is.EqualTo(2));

            // Updating s2 triggers effect exactly once
            s2.Set(20);
            Assert.That(effectRunCount, Is.EqualTo(3));
        }

        [Test]
        public void InterleavedReadsByDifferentConsumersAreDeduplicatedCorrectly()
        {
            WritableSignal<int> s1 = Signal.State(1);

            Signal<int> c2 = Signal.Computed(s1.Get);

            var c1EvalCount = 0;
            Signal<int> c1 = Signal.Computed(() =>
            {
                c1EvalCount++;
                int v1 = s1.Get();
                int v2 = c2.Get(); // Interleaved access to s1 by c2
                int v3 = s1.Get(); // Second access by c1
                return v1 + v2 + v3;
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(c1.Get(), Is.EqualTo(3));
                Assert.That(c1EvalCount, Is.EqualTo(1));
            }

            // Trigger re-evaluation
            s1.Set(10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(c1.Get(), Is.EqualTo(30));
                Assert.That(c1EvalCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void ConsecutiveAndNonConsecutiveReadsInLinkedSignalAreDeduplicated()
        {
            WritableSignal<int> sourceSignal = Signal.State(10);
            WritableSignal<int> extraSignal = Signal.State(5);

            var evalCount = 0;
            WritableSignal<int> linkedSignal = Signal.Linked<int, int>(
                sourceSignal,
                (sourceVal, _) =>
                {
                    evalCount++;
                    // Reads sourceVal (from source), extraSignal twice, and sourceSignal again
                    return sourceVal + extraSignal.Get() + extraSignal.Get() + sourceSignal.Get();
                });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(linkedSignal.Get(), Is.EqualTo(30)); // 10 + 5 + 5 + 10 = 30
                Assert.That(evalCount, Is.EqualTo(1));
            }

            // Updating extraSignal triggers recomputation exactly once
            extraSignal.Set(10);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(linkedSignal.Get(), Is.EqualTo(40)); // 10 + 10 + 10 + 10 = 40
                Assert.That(evalCount, Is.EqualTo(2));
            }

            // Updating sourceSignal triggers recomputation exactly once
            sourceSignal.Set(20);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(linkedSignal.Get(), Is.EqualTo(60)); // 20 + 10 + 10 + 20 = 60
                Assert.That(evalCount, Is.EqualTo(3));
            }
        }

        [Test]
        public void DynamicDependencyBranchesWithDuplicateReadsAreDeduplicatedAndPruned()
        {
            WritableSignal<bool> condition = Signal.State(true);
            WritableSignal<int> s1 = Signal.State(10);
            WritableSignal<int> s2 = Signal.State(20);

            var evalCount = 0;
            Signal<int> computed = Signal.Computed(() =>
            {
                evalCount++;
                if (condition.Get())
                {
                    // Branch 1: read s1 3 times
                    return s1.Get() + s1.Get() + s1.Get();
                }

                // Branch 2: read s2 2 times
                return s2.Get() + s2.Get();
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(30));
                Assert.That(evalCount, Is.EqualTo(1));
            }

            // Update s1: recomputes once
            s1.Set(15);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(45));
                Assert.That(evalCount, Is.EqualTo(2));
            }

            // Update s2 (inactive branch): should NOT recompute
            s2.Set(100);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(45));
                Assert.That(evalCount, Is.EqualTo(2));
            }

            // Switch branch to false
            condition.Set(false);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(200));
                Assert.That(evalCount, Is.EqualTo(3));
            }

            // Now update s2: recomputes once
            s2.Set(50);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(100));
                Assert.That(evalCount, Is.EqualTo(4));
            }

            // Now update s1 (now inactive): should NOT recompute
            s1.Set(999);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(100));
                Assert.That(evalCount, Is.EqualTo(4));
            }

            // Switch back to true branch: s1 dependencies are re-established with deduplication
            condition.Set(true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(2997)); // 999 * 3
                Assert.That(evalCount, Is.EqualTo(5));
            }

            s1.Set(1);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Get(), Is.EqualTo(3));
                Assert.That(evalCount, Is.EqualTo(6));
            }
        }

        [Test]
        public void MultipleReadsViaImplicitConversionAndValuePropertyAreDeduplicated()
        {
            StateSignal<int> s = Signal.State(5);
            var evalCount = 0;

            ComputedSignal<int> computed = Signal.Computed(() =>
            {
                evalCount++;
                int v1 = s; // implicit conversion
                int v2 = s.Value; // .Value property
                int v3 = s.Get(); // .Get() method
                return v1 + v2 + v3;
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Value, Is.EqualTo(15));
                Assert.That(evalCount, Is.EqualTo(1));
            }

            s.Value = 10;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(computed.Value, Is.EqualTo(30));
                Assert.That(evalCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void MultipleConsecutiveAndNonConsecutiveReadsInEffectDoNotAccumulateDuplicateLinksAcrossReRuns()
        {
            WritableSignal<int> s1 = Signal.State(1);
            WritableSignal<int> s2 = Signal.State(10);

            var effectRunCount = 0;

            _ = Signal.Effect(() =>
            {
                effectRunCount++;
                _ = s1.Get() + s1.Get() + s2.Get() + s1.Get() + s2.Get();
            });

            Assert.That(effectRunCount, Is.EqualTo(1));

            // Repeated updates to s1: each update triggers the effect exactly once
            s1.Set(2);
            Assert.That(effectRunCount, Is.EqualTo(2));

            s1.Set(3);
            Assert.That(effectRunCount, Is.EqualTo(3));

            // Repeated updates to s2: each update triggers the effect exactly once
            s2.Set(20);
            Assert.That(effectRunCount, Is.EqualTo(4));

            s2.Set(30);
            Assert.That(effectRunCount, Is.EqualTo(5));
        }
    }
}