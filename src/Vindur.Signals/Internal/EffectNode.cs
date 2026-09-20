using System;

namespace Vindur.Signals.Internal
{
    internal class EffectNode : ReactiveNode, IEffectRef
    {
        private readonly Action<Action<Action>> _effectFn;
        private Action? _cleanupFn;
        private bool _initialized;

        public bool IsDestroyed { get; private set; }
        public override bool IsLive => !IsDestroyed;

        public EffectNode(Action<Action<Action>> effectFn, bool allowSignalWrites, string debugName)
        {
            _effectFn = effectFn;
            AllowSignalWrites = allowSignalWrites;
            DebugName = debugName;
            IsDirty = true;
        }

        public override void MarkDirty()
        {
            if (IsDirty || IsDestroyed)
            {
                return;
            }

            IsDirty = true;
            WatchScheduler.Schedule(this);
        }

        public void Run()
        {
            if (IsDestroyed)
            {
                return;
            }

            bool depsChanged = CheckIfDependenciesChanged();

            if (_initialized && !depsChanged)
            {
                IsDirty = false;
                return;
            }

            if (_cleanupFn != null)
            {
                try
                {
                    _cleanupFn();
                }
                finally
                {
                    _cleanupFn = null;
                }
            }

            PrepareForRecomputation();
            ReactiveNode? prevConsumer = ReactiveGraph.SetActiveConsumer(this);

            IsDirty = false;
            try
            {
                _effectFn(cleanup => _cleanupFn = cleanup);
            }
            finally
            {
                ReactiveGraph.SetActiveConsumer(prevConsumer);
                FinalizeRecomputation();
                if (!IsDirty)
                {
                    LastCleanEpoch = ReactiveGraph.Epoch;
                }
                _initialized = true;
            }
        }

        public override void Destroy()
        {
            if (IsDestroyed)
            {
                return;
            }

            if (_cleanupFn != null)
            {
                try
                {
                    _cleanupFn();
                }
                finally
                {
                    _cleanupFn = null;
                }
            }

            base.Destroy();

            IsDestroyed = true;
        }

        public void Dispose()
        {
            Destroy();
        }
    }
}
