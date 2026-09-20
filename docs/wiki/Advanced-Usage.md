# Advanced Usage & Safety

This guide covers advanced configuration, optimization techniques, and runtime safety mechanisms built into
`Vindur.Signals`.

## Table of Contents

- [Custom Equality Comparers](#custom-equality-comparers)
    - [Default Equality vs Custom Comparers](#default-equality-vs-custom-comparers)
    - [Passing Comparers to Signals](#passing-comparers-to-signals)
    - [Example: Collection & Custom Object Equality](#example-collection--custom-object-equality)
- [Write Safety & Permissions](#write-safety--permissions)
    - [Pure Contexts & `InvalidSignalWriteException`](#pure-contexts--invalidsignalwriteexception)
    - [Enforcing Unidirectional Flow with
      `allowSignalWrites: false`](#enforcing-unidirectional-flow-with-allowsignalwrites-false)
- [Cycle Detection & Graph Protection](#cycle-detection--graph-protection)
    - [Detecting Cycles with `CyclicSignalDependencyException`](#detecting-cycles-with-cyclicsignaldependencyexception)
    - [Best Practices to Prevent Cycles](#best-practices-to-prevent-cycles)
- [Exception Reference](#exception-reference)
    - [`SignalException`](#signalexception)
    - [`CyclicSignalDependencyException`](#cyclicsignaldependencyexception)
    - [`InvalidSignalWriteException`](#invalidsignalwriteexception)
    - [`ReadOnlySignalException`](#readonlysignalexception)
    - [`InvalidSignalReadException`](#invalidsignalreadexception)

## Custom Equality Comparers

Every signal (`Signal.State`, `Signal.Computed`, `Signal.Linked`) uses an `IEqualityComparer<T>` to determine whether a
value change actually occurred.

### Default Equality vs Custom Comparers

By default, signals use `EqualityComparer<T>.Default`. For primitives (`int`, `float`, `string`) and structs
implementing `IEquatable<T>`, this uses standard value equality. However, for reference types, default equality checks
reference identity (`object.ReferenceEquals`), which can trigger redundant downstream recalculations even when values
are semantically equivalent.

Supplying a custom `IEqualityComparer<T>` lets you suppress downstream graph notifications when data hasn't meaningfully
changed.

### Passing Comparers to Signals

Factory methods in `Signal` accept an optional `equalityComparer`:

```csharp
public static StateSignal<T> State<T>(
    T initialValue,
    IEqualityComparer<T>? equalityComparer = null,
    string debugName = "");

public static ComputedSignal<T> Computed<T>(
    Func<T> computation,
    IEqualityComparer<T>? equalityComparer = null,
    string debugName = "");
```

### Example: Collection & Custom Object Equality

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Vindur.Signals;

public record struct Vector2D(float X, float Y);

public class Vector2DApproxComparer : IEqualityComparer<Vector2D>
{
    private readonly float _epsilon;
    public Vector2DApproxComparer(float epsilon = 0.001f) => _epsilon = epsilon;

    public bool Equals(Vector2D a, Vector2D b)
    {
        return Math.Abs(a.X - b.X) < _epsilon && Math.Abs(a.Y - b.Y) < _epsilon;
    }

    public int GetHashCode(Vector2D obj) => HashCode.Combine(obj.X, obj.Y);
}

// Usage:
var position = Signal.State(
    new Vector2D(0f, 0f),
    equalityComparer: new Vector2DApproxComparer(0.01f),
    debugName: "PlayerPosition"
);

Signal.Effect(() =>
{
    Console.WriteLine($"Rendered at: {position.Value}");
});

// A tiny change smaller than epsilon will NOT re-trigger the effect:
position.Set(new Vector2D(0.0001f, 0.0002f)); // No recalculation or effect execution!

// A significant change triggers dependents as expected:
position.Set(new Vector2D(10.0f, 5.0f)); // Output: Rendered at: Vector2D { X = 10, Y = 5 }
```

## Write Safety & Permissions

A core tenet of fine-grained reactive state systems is **unidirectional data flow**. Updating signals during pure
computations or within unintended reactive phases can cause infinite cascades, non-deterministic bugs, and race
conditions.

### Pure Contexts & `InvalidSignalWriteException`

Computations inside `Signal.Computed` are strictly pure functions:

- They may **read** other signals to compute a derivative value.
- They must **never mutate** state.

If a signal write (`.Set()` or `.Update()`) is attempted inside a `Signal.Computed` computation, `Vindur.Signals`
immediately throws an `InvalidSignalWriteException`:

```csharp
var count = Signal.State(0);
var flag = Signal.State(false);

var badComputed = Signal.Computed(() =>
{
    // ILLEGAL: Writing to a signal inside a computed evaluation
    flag.Set(true); // Throws InvalidSignalWriteException!
    return count.Value * 2;
});

// Accessing badComputed triggers the exception:
var value = badComputed.Value;
```

### Enforcing Unidirectional Flow with `allowSignalWrites: false`

By default, `Signal.Effect` allows signal mutations inside its body. However, writing to signals inside effects can
easily cause feedback loops if the effect modifies a signal it also depends on.

To enforce that an effect only performs external side-effects (e.g., updating UI components, writing logs, or calling
external APIs) without modifying signals, set `allowSignalWrites: false`:

```csharp
var totalScore = Signal.State(100);
var uiLabelText = Signal.State("");

// Restrict effect to pure side-effects:
Signal.Effect(() =>
{
    // Permitted: Reading signals
    int score = totalScore.Value;

    // ILLEGAL: Attempting to write to a signal with allowSignalWrites = false
    uiLabelText.Set($"Score: {score}"); // Throws InvalidSignalWriteException!
},
allowSignalWrites: false,
debugName: "StrictScoreLogger");
```

## Cycle Detection & Graph Protection

A cyclic dependency occurs when signal `A` depends on signal `B`, and signal `B` directly or indirectly depends back on
signal `A`.

### Detecting Cycles with `CyclicSignalDependencyException`

`Vindur.Signals` maintains a reactive graph state tracking node evaluations. If a cycle is encountered during graph
traversal or computation, execution stops immediately and throws a `CyclicSignalDependencyException`:

```csharp
StateSignal<int>? a = null;
ComputedSignal<int>? b = null;

a = Signal.State(1, debugName: "SignalA");
b = Signal.Computed(() => a.Value + 1, debugName: "SignalB");

// Re-defining a computation that references 'b' back into 'a'
var c = Signal.Computed(() =>
{
    // If 'c' depends on 'b', and 'b' was made to depend on 'c':
    return b.Value + 1;
}, debugName: "SignalC");
```

If a cycle is detected during recomputation:

```
Vindur.Signals.Exceptions.CyclicSignalDependencyException:
Detected cycle in computed signal 'SignalA' execution.
```

### Best Practices to Prevent Cycles

1. **Keep Computed Signals Pure**: Computed signals should never directly set or update other signals.
2. **Break Dependency Loops with `Untracked`**: If an effect needs to read an auxiliary signal's current value without
   reacting to its changes, read it via `signal.Peek()` or wrap the read in `Signal.Untracked(...)`.
3. **Use Linked Signals for Overrides**: When a reactive value needs to reset when source signals change, use
   `Signal.Linked` instead of updating the value via an effect.
