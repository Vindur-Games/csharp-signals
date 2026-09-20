# Core Reactive Primitives

`Vindur.Signals` provides a comprehensive set of reactive primitives inspired by modern reactive architectures (such as
Angular Signals). This guide explains how each primitive works, when to use it, and provides runnable C# code examples.

## Table of Contents

- [State Signals (`Signal.State`)](#state-signals-signalstate)
    - [Creating State Signals](#creating-state-signals)
    - [Reading Values](#reading-values)
    - [Mutating State: `Set` vs `Update`](#mutating-state-set-vs-update)
    - [Read-Only Views: `AsReadOnly`](#read-only-views-asreadonly)
- [Computed Signals (`Signal.Computed`)](#computed-signals-signalcomputed)
    - [Lazy Evaluation & Automatic Memoization](#lazy-evaluation--automatic-memoization)
    - [Dynamic Dependency Tracking](#dynamic-dependency-tracking)
- [Linked Signals (`Signal.Linked`)](#linked-signals-signallinked)
    - [Why Linked Signals?](#why-linked-signals)
    - [Creating a Linked Signal](#creating-a-linked-signal)
    - [Accessing Previous State with `LinkedSignalPrevious`](#accessing-previous-state-with-linkedsignalprevious)
- [Effects (`Signal.Effect`)](#effects-signaleffect)
    - [Registering Side Effects](#registering-side-effects)
    - [Effect Cleanup Callbacks](#effect-cleanup-callbacks)
    - [Lifetime Management & Disposal (`IEffectRef`)](#lifetime-management--disposal-ieffectref)
- [Untracked Reads (`Untracked` & `Peek`)](#untracked-reads-untracked--peek)
    - [Using `signal.Peek()`](#using-signalpeek)
    - [Using `Signal.Untracked(...)`](#using-signaluntracked)

## State Signals (`Signal.State`)

A **State Signal** is a writable reactive container that holds a value. When you update its value, any dependent
computed signals or active effects are automatically notified and scheduled for re-evaluation.

### Creating State Signals

Use the static `Signal.State<T>` factory method:

```csharp
using Vindur.Signals;

// Explicit type specification
StateSignal<int> score = Signal.State(0);

// Type inferred from argument
var playerName = Signal.State("Alice");

// Optional debug name for diagnostics
var health = Signal.State(100, debugName: "player_health");
```

### Reading Values

Values can be read in three equivalent ways. In all three cases, reading a signal inside a reactive context (such as a
computed signal or an effect) automatically registers it as a dependency:

- **`.Value` property**: The primary, idiomatic way to read the current value.
- **`.Get()` method**: Method syntax equivalent to `.Value`.
- **Implicit conversion**: A `Signal<T>` can be passed or assigned directly wherever `T` is expected.

```csharp
var count = Signal.State(42);

// Reading via .Value
int current = count.Value;

// Implicit conversion
int implicitVal = count;

Console.WriteLine($"Current count is: {count}"); // Implicitly converts to int
```

### Mutating State: `Set` vs `Update`

State signals provide two mutation methods:

- **`Set(newValue)`**: Replaces the current value directly. If the new value is semantically the same as the previous
  value (determined by the equality comparer), consumers will **not** be notified.
- **`Update(func)`**: Takes a transformation delegate `Func<T, T>` that receives the current value and returns the next
  value.

```csharp
var gold = Signal.State(100);

// Directly set new value
gold.Set(150);

// Or assign to .Value
gold.Value = 200;

// Transform based on previous value
gold.Update(current => current + 25); // New value: 225
```

### Read-Only Views: `AsReadOnly`

When exposing reactive state from services, classes, or view-models, it is best practice to expose only read-only
signals so external callers cannot mutate internal state. This can be done by calling `.AsReadOnly()` on a signal. It's
a very lightweight operation, so exposing it as a computed property is perfectly safe:

```csharp
public class PlayerService
{
    // Internal writable state
    private readonly StateSignal<int> _lives = Signal.State(3);

    // Exposed to consumers as read-only Signal<int>
    public Signal<int> Lives => _lives.AsReadOnly();

    public void LoseLife()
    {
        _lives.Update(l => Math.Max(0, l - 1));
    }
}
```

## Computed Signals (`Signal.Computed`)

A **Computed Signal** is a read-only signal that derives its value from one or more other signals.

### Lazy Evaluation & Automatic Memoization

Computed signals have two vital performance characteristics:

1. **Lazy Evaluation**: The computation function does not run until `.Value` or `.Get()` is read.
2. **Memoization**: Once computed, the result is cached. Subsequent reads return the cached value immediately without
   re-running the computation. The cache is only invalidated when one of its tracked dependencies changes.

```csharp
var firstName = Signal.State("Ada");
var lastName = Signal.State("Lovelace");

var fullName = Signal.Computed(() =>
{
    Console.WriteLine("Computing full name...");
    return $"{firstName.Value} {lastName.Value}";
});

// Nothing is computed yet!

// First read: runs computation and caches result
Console.WriteLine(fullName.Value); // Logs: "Computing full name..." then "Ada Lovelace"

// Second read: dependencies haven't changed, returns cached result
Console.WriteLine(fullName.Value); // Prints "Ada Lovelace" (does not log "Computing...")

// Modify dependency
firstName.Set("Augusta Ada");

// Next read: cache is dirty, recomputes
Console.WriteLine(fullName.Value); // Logs: "Computing full name..." then "Augusta Ada Lovelace"
```

### Dynamic Dependency Tracking

Dependencies are tracked dynamically during each computation run. If your logic contains branches (`if`/`else` or
ternary conditions), `Vindur.Signals` only subscribes to signals that were actually read during the last evaluation:

```csharp
var isCelsius = Signal.State(true);
var celsius = Signal.State(25.0);
var fahrenheit = Signal.State(77.0);

var displayTemp = Signal.Computed(() =>
{
    if (isCelsius.Value)
    {
        return $"{celsius.Value:F1} °C";
    }
    else
    {
        return $"{fahrenheit.Value:F1} °F";
    }
});

// While isCelsius is true, changes to fahrenheit will NOT invalidate displayTemp!
fahrenheit.Set(100.0); // displayTemp remains clean, no recalculation
```

## Linked Signals (`Signal.Linked`)

A **Linked Signal** is a writable signal whose value is tied to a source signal or computation. It provides a reactive
default value that resets whenever the source changes but still allows you to override its value in between resets.

### Why Linked Signals?

A common UI pattern is a form or dropdown whose selection defaults based on a selected entity (e.g., selecting a country
defaults the currency), but the user is permitted to override the selection. If the country changes, the currency should
reset back to the default currency for that new country.

Without linked signals, implementing this pattern requires manual event subscriptions, state synchronization flags, and
cleanup code. `Signal.Linked` solves this declaratively.

### Creating a Linked Signal

```csharp
public class ShippingOption
{
    public string Name { get; set; } = "";
    public decimal DefaultCost { get; set; }
}

var standardShipping = new ShippingOption { Name = "Standard", DefaultCost = 5.0m };
var expressShipping = new ShippingOption { Name = "Express", DefaultCost = 15.0m };

// Source signal
var selectedOption = Signal.State(standardShipping);

// Linked signal derives initial value from selectedOption
var customCost = Signal.Linked(
    source: selectedOption,
    computation: opt => opt.DefaultCost
);

Console.WriteLine(customCost.Value); // 5.0m

// User overrides the cost locally
customCost.Set(3.5m);
Console.WriteLine(customCost.Value); // 3.5m (overridden)

// Source option changes -> Linked signal automatically resets to the new default!
selectedOption.Set(expressShipping);
Console.WriteLine(customCost.Value); // 15.0m (automatically reset)
```

### Accessing Previous State with `LinkedSignalPrevious`

You can inspect the previous source and target values when recomputing by accepting a
`LinkedSignalPrevious<TSource, TValue>?` parameter:

```csharp
var selectedUser = Signal.State("UserA");

// Retain customized role if valid for the new user, otherwise fallback to "Viewer"
var userRole = Signal.Linked<string, string>(
    source: selectedUser,
    computation: (newUser, prev) =>
    {
        if (prev.HasValue && prev.Value.Value == "SuperAdmin")
        {
            return "SuperAdmin"; // Preserve elevated privileges across user switch
        }
        return "Viewer"; // Default role
    }
);
```

## Effects (`Signal.Effect`)

An **Effect** is an operation that runs side effects whenever one or more tracked signals change. Effects are
automatically scheduled and flushed as soon as possible when signals are modified.

### Registering Side Effects

```csharp
var count = Signal.State(0);

// The effect runs immediately upon creation, and subsequently when count changes
IEffectRef effectRef = Signal.Effect(() =>
{
    Console.WriteLine($"[Audit Log] Current counter: {count.Value}");
});

count.Set(1); // Output: [Audit Log] Current counter: 1
count.Set(2); // Output: [Audit Log] Current counter: 2
```

### Effect Cleanup Callbacks

Effects frequently interact with external resources, like timers, file watchers, or network sockets. You can pass a
cleanup callback delegate `onCleanup` to teardown the previous run's resources before the next run or when the effect is
destroyed:

```csharp
var connectionUrl = Signal.State("https://api.example.com/stream-1");

IEffectRef connectionEffect = Signal.Effect(onCleanup =>
{
    string url = connectionUrl.Value;
    Console.WriteLine($"Opening connection to: {url}");

    // Register cleanup callback
    onCleanup(() =>
    {
        Console.WriteLine($"Closing connection to: {url}");
    });
});

// Changing url triggers cleanup of stream-1, then opens stream-2:
connectionUrl.Set("https://api.example.com/stream-2");
// Output:
// Closing connection to: https://api.example.com/stream-1
// Opening connection to: https://api.example.com/stream-2
```

### Lifetime Management & Disposal (`IEffectRef`)

`Signal.Effect` returns an `IEffectRef` handle that implements `IDisposable`. Always dispose effects when they are no
longer needed to prevent memory leaks and unregister them from the reactive graph:

```csharp
IEffectRef effectRef = Signal.Effect(() =>
{
    Console.WriteLine($"Current: {count.Value}");
});

// Check if effect is active
Console.WriteLine(effectRef.IsDestroyed); // false

// Manually destroy or dispose
effectRef.Destroy(); // or effectRef.Dispose()

Console.WriteLine(effectRef.IsDestroyed); // true

// Modifying count now has no effect
count.Set(99); // No console output
```

In modern C#, you can also use `using var effect = Signal.Effect(...)` when scoped to a block.

## Untracked Reads (`Untracked` & `Peek`)

By default, reading a signal's `.Value` inside an effect, computed signal or linked signal registers it as a dependency.
If you need to read a signal *without* subscribing to its changes, use `Peek()` or `Signal.Untracked()`.

### Using `signal.Peek()`

`signal.Peek()` retrieves the current value without registering a dependency on the active consumer:

```csharp
var primary = Signal.State(10);
var secondary = Signal.State(20);

var combined = Signal.Computed(() =>
{
    int a = primary.Value;       // Tracking dependency on primary
    int b = secondary.Peek();    // NOT tracking dependency on secondary

    return a + b;
});

// Changing primary triggers recomputation
primary.Set(15); // combined is invalidated

// Changing secondary does NOT trigger recomputation
secondary.Set(50); // combined is NOT invalidated
```

### Using `Signal.Untracked(...)`

Use `Signal.Untracked` to run an entire block of code or expression outside any active reactive tracking:

```csharp
var userId = Signal.State(101);
var currentTheme = Signal.State("Dark");

Signal.Effect(() =>
{
    int id = userId.Value; // Tracked

    // Untracked block: reading currentTheme will not re-trigger this effect
    Signal.Untracked(() =>
    {
        Console.WriteLine($"User {id} loaded with theme {currentTheme.Value}");
    });
});
```
