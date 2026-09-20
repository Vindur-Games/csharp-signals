# Vindur.Signals

[![NuGet](https://img.shields.io/nuget/v/Vindur.Signals.svg)](https://www.nuget.org/packages/Vindur.Signals)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/Vindur-Games/csharp-signals/blob/main/LICENSE)
[![Target Frameworks](https://img.shields.io/badge/.NET-.NET%20Standard%202.1%20%7C%20.NET%2010.0-512BD4)](https://dotnet.microsoft.com/)
[![Unity](https://img.shields.io/badge/Unity-6.0%2B-black?logo=unity)](https://unity.com)

A lightweight, high-performance C# implementation of fine-grained reactivity inspired by Angular signals. Designed for both modern **.NET** and **Unity** applications, `Vindur.Signals` lets you build declarative, reactive state graphs with zero boilerplate, automatic dependency tracking, and lazy evaluation.

## Features

- **Fine-Grained Reactivity**: Dependencies are tracked automatically at runtime without manual subscription plumbing or `INotifyPropertyChanged` boilerplate.
- **Lazy Evaluation & Memoization**: Computed signals are evaluated only on demand when read and cached until an upstream dependency changes.
- **Linked Signals**: Writable signals that track upstream reactive defaults while supporting local user overrides and previous-state tracking.
- **Side Effects with Cleanup**: Register effects that execute automatically when dependencies update, complete with cleanup callbacks and lifecycle disposal (`IEffectRef`).
- **Graph Safety & Cycle Detection**: Built-in protection detects cyclic dependencies and enforces write guards in pure contexts.
- **Unity & .NET Standard 2.1 Ready**: Targets `.NET Standard 2.1` and `.NET 10.0`. Works seamlessly across Unity (Mono / IL2CPP) and modern .NET runtimes.

## Limitations
Neither the computed signals nor effects currently support asynchronous logic. If that's something you need, then please create a GitHub issue in this repository and I will do my best to see it done! The same goes for any other feature requests.

As an olive branch, I can offer you to check out [fedeAlterio's implementation which is packed with way more features](https://github.com/fedeAlterio/SignalsDotnet) 😊

## Installation

### .NET (NuGet)

Install via the .NET CLI:
```bash
dotnet add package Vindur.Signals
```

Or add the package reference directly to your `.csproj`:
```xml
<PackageReference Include="Vindur.Signals" Version="1.0.0" />
```

### Unity Package Manager (UPM)

#### Option 1: Install via Git URL (Unity Editor)
1. In Unity, open **Window** > **Package Manager**.
2. Click the **+** (add) button in the upper-left corner.
3. Select **Add package from git URL...**.
4. Enter the repository URL with the `unity-package` query path:
   ```text
   https://github.com/Vindur-Games/csharp-signals.git?path=unity-package
   ```
   *(To pin a specific release tag, append `#v1.0.0`, e.g., `https://github.com/Vindur-Games/csharp-signals.git?path=unity-package#v1.0.0`)*

#### Option 2: Add to `Packages/manifest.json`
Add the dependency directly to your Unity project's `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.vindur.signals": "https://github.com/Vindur-Games/csharp-signals.git?path=unity-package"
  }
}
```

### Git Submodule / Source Reference
Clone or submodule the repository into your solution:
```bash
git submodule add https://github.com/Vindur-Games/csharp-signals.git
```
Then reference `src/Vindur.Signals/Vindur.Signals.csproj` directly in your project.

## Quick Start

```csharp
using System;
using Vindur.Signals;

// 1. Create a writable state signal
var count = Signal.State(1);

// 2. Derive computed state (lazily evaluated and memoized)
var doubleCount = Signal.Computed(() => count.Value * 2);

// 3. Register a side effect (runs immediately and whenever dependencies update)
using var effect = Signal.Effect(() =>
{
    Console.WriteLine($"Count: {count.Value}, Double: {doubleCount.Value}");
});
// Console Output: Count: 1, Double: 2

// 4. Update the state signal
count.Set(5);
// Console Output: Count: 5, Double: 10

// 5. Update using a transformation function
count.Update(current => current + 1);
// Console Output: Count: 6, Double: 12
```

## Documentation

For in-depth guides, complete API references, and architecture best practices, visit the **GitHub Wiki**:

- **[Core Reactive Primitives](https://github.com/Vindur-Games/csharp-signals/wiki/Core-Primitives)**  
  Detailed coverage of `StateSignal<T>`, `ComputedSignal<T>`, `LinkedSignal<TSource, TValue>`, `Signal.Effect`, and untracked evaluations (`Untracked` / `Peek`).

- **[Advanced Usage & Safety](https://github.com/Vindur-Games/csharp-signals/wiki/Advanced-Usage)**  
  Custom `IEqualityComparer<T>`, cycle detection (`CyclicSignalDependencyException`), write restrictions (`allowSignalWrites`), and exception handling.

- **[Unity Integration Guide](https://github.com/Vindur-Games/csharp-signals/wiki/Unity-Integration)**  
  Patterns for `MonoBehaviour` lifecycle management, TextMeshPro and UI Toolkit data binding, and Unity main-thread dispatching.

---

## License

This project is licensed under the [MIT License](https://github.com/Vindur-Games/csharp-signals/blob/main/LICENSE).

