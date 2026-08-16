<div align="center">
  <img src="assets/icon.png" alt="SingleFlight">
</div>

# SingleFlight

[![NuGet](https://img.shields.io/nuget/v/SingleFlight.svg)](https://www.nuget.org/packages/SingleFlight/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![main](https://github.com/Adolfok3/SingleFlight/actions/workflows/main.yml/badge.svg?branch=main)](https://github.com/Adolfok3/SingleFlight/actions/workflows/main.yml)
[![codecov](https://codecov.io/gh/Adolfok3/SingleFlight/branch/main/graph/badge.svg)](https://codecov.io/gh/Adolfok3/SingleFlight)

A simple, lightweight, zero-dependency .NET Package that stops duplicate concurrent calls: identical in-flight work runs just once and every caller shares the result.

## Requirements

Targets **.NET Standard 2.0**, so it runs on virtually any modern project:

- .NET / .NET Core 2.0 and later (including .NET 5 – .NET 11)
- .NET Framework 4.6.1 and later
- Mono, Xamarin, and Unity

## Installation

```bash
dotnet add package SingleFlight
```

Or via the Package Manager Console:

```powershell
Install-Package SingleFlight
```

## What it does

When many callers ask for the same thing at the same time — a cache miss stampede, a burst of
identical HTTP requests, a hot config reload — you usually want the expensive work to run **once**
and have everyone share that single result. That is the SingleFlight pattern (borrowed from Go's
`golang.org/x/sync/singleflight`).

`SingleFlight` coalesces concurrent calls that share a **key**: the first caller runs the factory,
every caller that arrives while the work is still in flight waits for and receives that same result.

> It is **not** a cache. An entry only lives while its call is in flight. As soon as the shared
> execution finishes (successfully or not) the key is released, so the next call starts fresh.

The simplest entry point is the static class `SingleFlight<T>` with `RunAsync`, which returns the value
directly (`T` is the type produced by the work). For isolated scopes or dependency injection there is
also the instance-based `SingleFlightGroup<T>`, and a `RunDetailedAsync` variant that reports whether a
call was coalesced — both covered below.

## Usage

Wrap the expensive work with a key at a single call site:

```csharp
using SingleFlight;

User user = await SingleFlight<User>.RunAsync($"user:{id}", () => LoadUserAsync(id));
```

That is the whole API. You call it once, from one place — the coalescing happens on its own: whenever
several callers reach this line for the same key at the same time, `LoadUserAsync` runs a single time
and they all receive that same result.

A typical cache-stampede guard:

```csharp
async Task<User> GetUserAsync(int id)
{
    if (_cache.TryGetValue(id, out User cached))
        return cached;

    // Only one call hits the database per key, even under a burst of misses.
    return await SingleFlight<User>.RunAsync($"user:{id}", async () =>
    {
        var user = await _repository.LoadAsync(id);
        _cache.Set(id, user);
        return user;
    });
}
```

Keys are scoped per `T`: `SingleFlight<User>` and `SingleFlight<Order>` never coalesce with each other,
even for an equal key.

### Knowing whether a call was coalesced

`RunDetailedAsync` returns a `SingleFlightResult<T>` that carries both the value and a `Joined` flag —
`true` when this caller attached to a call that was already in flight (it did **not** run the factory),
`false` when this caller owned the execution. Useful for metrics, logging, or deciding whether *you* are
responsible for a follow-up side effect:

```csharp
var (user, joined) = await SingleFlight<User>.RunDetailedAsync($"user:{id}", () => LoadUserAsync(id));
if (!joined)
    _metrics.RecordCacheStampedeAvoided();
```

### Isolated scopes and dependency injection

The static `SingleFlight<T>` is a single process-wide scope per `T`. When you want **isolated
keyspaces** — so unrelated features can't collide on an equal key — or you want to inject and unit-test
the coalescer, use `SingleFlightGroup<T>` (which implements `ISingleFlightGroup<T>`):

```csharp
// Registration
services.AddSingleton<ISingleFlightGroup<User>, SingleFlightGroup<User>>();

// Usage
public UserService(ISingleFlightGroup<User> flight) => _flight = flight;

Task<User> GetUserAsync(int id) =>
    _flight.RunAsync($"user:{id}", () => LoadUserAsync(id));
```

Each group owns its own set of in-flight keys, so two groups never coalesce with each other even for an
equal key. You can also pass a custom key comparer — e.g. `new SingleFlightGroup<User>(StringComparer.OrdinalIgnoreCase)`.

### Exceptions

If the factory fails, every caller joined to that call observes the **same** exception, and the key is
released so the next call is free to retry.

### API reference

| Member | Description |
| ------ | ----------- |
| `SingleFlight<T>.RunAsync(string key, Func<Task<T>> factory)` | Runs the factory once per in-flight key; concurrent callers with the same key share the returned value. |
| `SingleFlight<T>.RunDetailedAsync(string key, Func<Task<T>> factory)` | Like `RunAsync`, but returns a `SingleFlightResult<T>` reporting whether this caller `Joined` an in-flight call. |
| `SingleFlightGroup<T>` / `ISingleFlightGroup<T>` | An isolated, injectable coalescing scope with its own keyspace. Exposes the same `RunAsync` / `RunDetailedAsync`, plus a constructor taking a custom key comparer. |
| `SingleFlightResult<T>` | Readonly struct carrying `Value` and `Joined`; deconstructs to `(value, joined)`. |

## Contributing

Issues and pull requests are welcome. Please make sure `dotnet test` passes and warnings stay at zero before submitting.

## License

MIT - see [LICENSE](./LICENSE).
