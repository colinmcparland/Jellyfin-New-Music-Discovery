---
name: type-design-performance
description: Design .NET types for performance. Seal classes, use readonly structs, prefer static pure functions, avoid premature enumeration, and choose the right collection types.
invocable: false
---

# Type Design for Performance

## When to Use This Skill

Use this skill when:
- Designing new types and APIs
- Reviewing code for performance issues
- Choosing between class, struct, and record
- Working with collections and enumerables

---

## Core Principles

1. **Seal your types** - Unless explicitly designed for inheritance
2. **Prefer readonly structs** - For small, immutable value types
3. **Prefer static pure functions** - Better performance and testability
4. **Defer enumeration** - Don't materialize until you need to
5. **Return immutable collections** - From API boundaries

---

## Seal Classes by Default

Sealing classes enables JIT devirtualization and communicates API intent.

```csharp
// DO: Seal classes not designed for inheritance
public sealed class OrderProcessor
{
    public void Process(Order order) { }
}

// DO: Seal records (they're classes)
public sealed record OrderCreated(OrderId Id, CustomerId CustomerId);

// DON'T: Leave unsealed without reason
public class OrderProcessor  // Can be subclassed - intentional?
{
    public virtual void Process(Order order) { }  // Virtual = slower
}
```

---

## Readonly Structs for Value Types

Structs should be `readonly` when immutable. This prevents defensive copies.

```csharp
// DO: Readonly struct for immutable value types
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

// DO: Readonly struct for small, short-lived data
public readonly struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
}
```

### When to Use Structs

| Use Struct When | Use Class When |
|-----------------|----------------|
| Small (16 bytes or less typically) | Larger objects |
| Short-lived | Long-lived |
| Frequently allocated | Shared references needed |
| Value semantics required | Identity semantics required |
| Immutable | Mutable state |

---

## Prefer Static Pure Functions

Static methods with no side effects are faster and more testable.

```csharp
// DO: Static pure function
public static class OrderCalculator
{
    public static Money CalculateTotal(IReadOnlyList<OrderItem> items)
    {
        var total = items.Sum(i => i.Price * i.Quantity);
        return new Money(total, "USD");
    }
}
```

**Benefits:**
- No vtable lookup (faster)
- No hidden state
- Easier to test (pure input to output)
- Thread-safe by design
- Forces explicit dependencies

---

## Defer Enumeration

Don't materialize enumerables until necessary.

```csharp
// BAD: Premature materialization
public IReadOnlyList<Order> GetActiveOrders()
{
    return _orders
        .Where(o => o.IsActive)
        .ToList()  // Materialized!
        .OrderBy(o => o.CreatedAt)  // Another iteration
        .ToList();  // Materialized again!
}

// GOOD: Defer until the end
public IReadOnlyList<Order> GetActiveOrders()
{
    return _orders
        .Where(o => o.IsActive)
        .OrderBy(o => o.CreatedAt)
        .ToList();  // Single materialization
}

// GOOD: Return IEnumerable if caller might not need all items
public IEnumerable<Order> GetActiveOrders()
{
    return _orders
        .Where(o => o.IsActive)
        .OrderBy(o => o.CreatedAt);
}
```

---

## ValueTask vs Task

Use `ValueTask` for hot paths that often complete synchronously. For real I/O, just use `Task`.

```csharp
// DO: ValueTask for cached/synchronous paths
public ValueTask<User?> GetUserAsync(UserId id)
{
    if (_cache.TryGetValue(id, out var user))
        return ValueTask.FromResult<User?>(user);  // No allocation

    return new ValueTask<User?>(FetchUserAsync(id));
}

// DO: Task for real I/O (simpler, no footguns)
public Task<Order> CreateOrderAsync(CreateOrderCommand cmd)
{
    return _repository.CreateAsync(cmd);
}
```

**ValueTask rules:**
- Never await a ValueTask more than once
- Never use `.Result` or `.GetAwaiter().GetResult()` before completion
- If in doubt, use Task

---

## Collection Return Types

### Return Immutable Collections from APIs

```csharp
// DO: Return immutable collection
public IReadOnlyList<Order> GetOrders()
{
    return _orders.ToList();
}

// DO: Use frozen collections for static data (.NET 8+)
private static readonly FrozenDictionary<string, Handler> _handlers =
    new Dictionary<string, Handler>
    {
        ["create"] = new CreateHandler(),
        ["update"] = new UpdateHandler(),
    }.ToFrozenDictionary();

// DON'T: Return mutable collection
public List<Order> GetOrders()
{
    return _orders;  // Caller can modify!
}
```

### Collection Guidelines

| Scenario | Return Type |
|----------|-------------|
| API boundary | `IReadOnlyList<T>`, `IReadOnlyCollection<T>` |
| Static lookup data | `FrozenDictionary<K,V>`, `FrozenSet<T>` |
| Internal building | `List<T>`, then return as readonly |
| Single item or none | `T?` (nullable) |
| Zero or more, lazy | `IEnumerable<T>` |

---

## Quick Reference

| Pattern | Benefit |
|---------|---------|
| `sealed class` | Devirtualization, clear API |
| `readonly record struct` | No defensive copies, value semantics |
| Static pure functions | No vtable, testable, thread-safe |
| Defer `.ToList()` | Single materialization |
| `ValueTask` for hot paths | Avoid Task allocation |
| `Span<T>` for bytes | Stack allocation, no copying |
| `IReadOnlyList<T>` return | Immutable API contract |
| `FrozenDictionary` | Fastest lookup for static data |

---

## Anti-Patterns

- Don't leave classes unsealed without reason
- Don't use mutable structs
- Don't use instance methods that could be static
- Don't call `.ToList()` multiple times in a chain
- Don't return `List<T>` from public APIs
- Don't use `ValueTask` for always-async operations

---

## Resources

- **Performance Best Practices**: https://learn.microsoft.com/en-us/dotnet/standard/performance/
- **Span<T> Guidance**: https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/
- **Frozen Collections**: https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen
