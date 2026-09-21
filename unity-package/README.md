# Vindur.Signals (Unity Package)

[![Unity](https://img.shields.io/badge/Unity-6.0%2B-black?logo=unity)](https://unity.com)
[![UPM](https://img.shields.io/badge/UPM-com.vindur.signals-blue?logo=unity)](https://github.com/Vindur-Games/csharp-signals)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/Vindur-Games/csharp-signals/blob/main/LICENSE)

A lightweight, zero-boilerplate fine-grained reactivity library for Unity inspired by Angular signals. `Vindur.Signals` allows you to build declarative state graphs that automatically track dependencies and trigger UI or gameplay reactions without manual event subscription plumbing or `INotifyPropertyChanged`.

Compatible with **Unity 6.0+**, Mono, and IL2CPP code stripping across all platforms.

## Installation

### Via Git URL (Unity Package Manager)

1. Open **Window** > **Package Manager**.
2. Click **+** > **Add package from git URL...**.
3. Enter:
   ```text
   https://github.com/Vindur-Games/csharp-signals.git?path=unity-package
   ```

### Via `Packages/manifest.json`

Add the dependency directly to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.vindur.signals": "https://github.com/Vindur-Games/csharp-signals.git?path=unity-package"
  }
}
```

## Quick Example: MonoBehaviour Binding

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
        // Bind UI to signal - runs immediately and auto-updates when PlayerScore changes
        _scoreEffect = Signal.Effect(() =>
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"Score: {GameManager.Instance.PlayerScore.Value:N0}";
            }
        });
    }

    private void OnDestroy()
    {
        // Dispose effect to prevent updates after component is destroyed
        _scoreEffect?.Dispose();
    }
}
```

## Documentation

For full guides, detailed code examples, and best practices, see the **GitHub Wiki**:

- **[Unity Integration Guide](https://github.com/Vindur-Games/csharp-signals/wiki/Unity-Integration)**  
  In-depth guide covering `MonoBehaviour` lifecycle synchronization, composite disposal patterns, UI binding (TextMeshPro, uGUI, UI Toolkit), performance tips, and main-thread execution.
- **[Core Reactive Primitives](https://github.com/Vindur-Games/csharp-signals/wiki/Core-Primitives)**  
  Learn about `Signal.State`, `Signal.Computed`, `Signal.Linked`, `Signal.Effect`, and untracked evaluations.
- **[Advanced Usage & Safety](https://github.com/Vindur-Games/csharp-signals/wiki/Advanced-Usage)**  
  Custom equality comparers (`IEqualityComparer<T>`), cycle detection, and write safety rules.

## License

This package is licensed under the [MIT License](https://github.com/Vindur-Games/csharp-signals/blob/main/LICENSE).