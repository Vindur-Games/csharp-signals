# Unity Integration Guide

`Vindur.Signals` is designed to be fully compatible with Unity (only Unity 6.0+ and IL2CPP runtimes have been tested –
other versions are probably fine, though). This guide demonstrates how to integrate fine-grained reactivity into Unity's
component architecture, connect signals to UI elements, and manage object lifecycles cleanly.

## Table of Contents

- [Installing into Unity](#installing-into-unity)
- [Performance & Best Practices](#performance--best-practices)
- [MonoBehaviour Lifecycle & Effect Disposal](#monobehaviour-lifecycle--effect-disposal)
    - [The Subscription & Cleanup Pattern](#the-subscription--cleanup-pattern)
    - [Handling Multiple Effects with Composite Disposal](#handling-multiple-effects-with-composite-disposal)
- [UI Data Binding Examples](#ui-data-binding-examples)
    - [One-Way Binding: TextMeshPro (`TMP_Text`)](#one-way-binding-textmeshpro-tmp_text)
    - [Two-Way Binding: Sliders and Input Fields](#two-way-binding-sliders-and-input-fields)
    - [UI Toolkit (`UIDocument`) Binding](#ui-toolkit-uidocument-binding)
- [Threading & Main Thread Synchronization](#threading--main-thread-synchronization)

## Installing into Unity

For complete installation instructions—including installing via Git URL in the Package Manager window or direct
configuration in `Packages/manifest.json` — see
the [Unity Package Manager (UPM) installation guide in the README](https://github.com/Vindur-Games/csharp-signals#unity-package-manager-upm).

## Performance & Best Practices

1. **Leverage Value Types**: Prefer using structs and primitive types for high-frequency signals to avoid GC
   allocations.
2. **Use `AsReadOnly()` on Public APIs**: Encapsulate `StateSignal<T>` fields inside services and expose only
   `Signal<T>` to your MonoBehaviours.
3. **IL2CPP & Code Stripping**: `Vindur.Signals` is lightweight and uses pure standard generic code without reflection
   emitters, making it fully compatible with IL2CPP code stripping on iOS, Android, WebGL, and Consoles.

## MonoBehaviour Lifecycle & Effect Disposal

When an effect references Unity `GameObject`s or `Component`s, it is vital to dispose of the effect when the component
is destroyed or disabled. Failing to dispose effects can lead to `MissingReferenceException`s when signals change after
a GameObject is destroyed.

### The Subscription & Cleanup Pattern

Store the returned `IEffectRef` in a private field and call `.Dispose()` (or `.Destroy()`) in `OnDestroy()`:

```csharp
using UnityEngine;
using TMPro;
using Vindur.Signals;

public class PlayerScoreView : MonoBehaviour
{
    [SerializeField] private TMP_Text _scoreText;

    private IEffectRef _scoreEffect;

    private void Start()
    {
        // Subscribe to player score state
        _scoreEffect = Signal.Effect(() =>
        {
            // Read signal value
            int score = GameManager.Instance.PlayerScore.Value;

            // Update Unity UI component
            if (_scoreText != null)
            {
                _scoreText.text = $"Score: {score:N0}";
            }
        });
    }

    private void OnDestroy()
    {
        // Unregister effect from reactive graph when GameObject is destroyed
        _scoreEffect?.Dispose();
    }
}
```

### Handling Multiple Effects with Composite Disposal

If your component binds multiple signals, store them in a list or array of `IDisposable`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Vindur.Signals;

public abstract class ReactiveMonoBehaviour : MonoBehaviour
{
    private readonly List<IDisposable> _subscriptions = new();

    protected void BindEffect(Action effectFn)
    {
        _subscriptions.Add(Signal.Effect(effectFn));
    }

    protected virtual void OnDestroy()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
    }
}
```

## UI Data Binding Examples

### One-Way Binding: TextMeshPro (`TMP_Text`)

Bind player stats directly to UI labels:

```csharp
using UnityEngine;
using TMPro;
using Vindur.Signals;

public class HealthBarView : MonoBehaviour
{
    [SerializeField] private TMP_Text _healthLabel;

    private IEffectRef _healthEffect;

    public void Initialize(Signal<int> currentHealth, Signal<int> maxHealth)
    {
        // Clean up any existing effect
        _healthEffect?.Dispose();

        _healthEffect = Signal.Effect(() =>
        {
            // Automatically re-runs if either currentHealth or maxHealth changes
            _healthLabel.text = $"{currentHealth.Value} / {maxHealth.Value} HP";
        });
    }

    private void OnDestroy()
    {
        _healthEffect?.Dispose();
    }
}
```

### Two-Way Binding: Sliders and Input Fields

For controls like `UnityEngine.UI.Slider` that can both display and modify state:

```csharp
using UnityEngine;
using UnityEngine.UI;
using Vindur.Signals;

public class VolumeSliderBinder : MonoBehaviour
{
    [SerializeField] private Slider _slider;

    // State signal for master volume (0.0 to 1.0)
    public StateSignal<float> MasterVolume { get; } = Signal.State(0.8f);

    private IEffectRef _effect;

    private void Awake()
    {
        // Signal -> Slider UI
        _effect = Signal.Effect(() =>
        {
            float volume = MasterVolume.Value;```
            if (!Mathf.Approximately(_slider.value, volume))
            {
                _slider.SetValueWithoutNotify(volume);
            }
        });

        // Slider UI -> Signal
        _slider.onValueChanged.AddListener(val =>
        {
            MasterVolume.Set(val);
        });
    }

    private void OnDestroy()
    {
        _effect?.Dispose();
        _slider.onValueChanged.RemoveAllListeners();
    }
}
```

### UI Toolkit (`UIDocument`) Binding

When using Unity UI Toolkit:

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using Vindur.Signals;

[RequireComponent(typeof(UIDocument))]
public class InventoryController : MonoBehaviour
{
    private Label _goldLabel;
    private IEffectRef _goldBinding;

    public StateSignal<int> Gold { get; } = Signal.State(500);

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _goldLabel = root.Q<Label>("gold-counter");

        _goldBinding = Signal.Effect(() =>
        {
            _goldLabel.text = $"Gold: {Gold.Value}";
        });
    }

    private void OnDisable()
    {
        _goldBinding?.Dispose();
    }
}
```

## Threading & Main Thread Synchronization

- **Unity API Restrictions**: All Unity engine APIs (such as modifying `Transform`, accessing `GameObject`, or updating
  UI components) must be executed on the **Unity Main Thread**.
- **Signal Updates from Background Threads**:
    - `Vindur.Signals` executes effects synchronously on whatever thread invokes `.Set()` or `.Update()`.
    - If you modify signals from a background task (e.g. `Task.Run` or network callback), ensure you dispatch the signal
      update to the main thread (using a synchronization context or main thread dispatcher) before updating the signal
      if dependent effects touch Unity components:

```csharp
// Inside background worker / async thread
async Task FetchPlayerDataAsync()
{
    var data = await DownloadStatsAsync();

    // Dispatch to Unity's main thread before mutating signals bound to UI
    UnityMainThreadDispatcher.Enqueue(() =>
    {
        playerScore.Set(data.Score);
    });
}
```
