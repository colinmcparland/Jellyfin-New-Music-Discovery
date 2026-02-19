---
name: modern-csharp-coding-standards
description: Write modern, high-performance C# code using records, pattern matching, value objects, async/await, Span<T>/Memory<T>, and best-practice API design patterns. Emphasizes functional-style programming with C# 12+ features.
invocable: false
---

# Modern C# Coding Standards

## When to Use This Skill

Use this skill when:
- Writing new C# code or refactoring existing code
- Designing public APIs for libraries or services
- Optimizing performance-critical code paths
- Implementing domain models with strong typing
- Building async/await-heavy applications
- Working with binary data, buffers, or high-throughput scenarios

## Core Principles

1. **Immutability by Default** - Use `record` types and `init`-only properties
2. **Type Safety** - Leverage nullable reference types and value objects
3. **Modern Pattern Matching** - Use `switch` expressions and patterns extensively
4. **Async Everywhere** - Prefer async APIs with proper cancellation support
5. **Zero-Allocation Patterns** - Use `Span<T>` and `Memory<T>` for performance-critical code
6. **API Design** - Accept abstractions, return appropriately specific types
7. **Composition Over Inheritance** - Avoid abstract base classes, prefer composition
8. **Value Objects as Structs** - Use `readonly record struct` for value objects

---

## Language Patterns

### Records for Immutable Data (C# 9+)

Use `record` types for DTOs, messages, events, and domain entities.

```csharp
// Simple immutable DTO
public record CustomerDto(string Id, string Name, string Email);

// Record with validation in constructor
public record EmailAddress
{
    public string Value { get; init; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@'))
            throw new ArgumentException("Invalid email address", nameof(value));

        Value = value;
    }
}

// Record with computed properties
public record Order(string Id, decimal Subtotal, decimal Tax)
{
    public decimal Total => Subtotal + Tax;
}

// Records with collections - use IReadOnlyList
public record ShoppingCart(
    string CartId,
    string CustomerId,
    IReadOnlyList<CartItem> Items
)
{
    public decimal Total => Items.Sum(item => item.Price * item.Quantity);
}
```

**When to use `record class` vs `record struct`:**
- `record class` (default): Reference types, use for entities, aggregates, DTOs with multiple properties
- `record struct`: Value types, use for value objects (see next section)

---

### Value Objects as readonly record struct

Value objects should **always be `readonly record struct`** for performance and value semantics.

```csharp
// Single-value object
public readonly record struct OrderId(string Value)
{
    public OrderId(string value) : this(
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("OrderId cannot be empty", nameof(value)))
    {
    }

    public override string ToString() => Value;
}

// Multi-value object
public readonly record struct Money(decimal Amount, string Currency)
{
    public Money(decimal amount, string currency) : this(
        amount >= 0 ? amount : throw new ArgumentException("Amount cannot be negative", nameof(amount)),
        ValidateCurrency(currency))
    {
    }

    private static string ValidateCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter code", nameof(currency));
        return currency.ToUpperInvariant();
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} to {other.Currency}");

        return new Money(Amount + other.Amount, Currency);
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}
```

**Why `readonly record struct` for value objects:**
- **Value semantics**: Equality based on content, not reference
- **Stack allocation**: Better performance, no GC pressure
- **Immutability**: `readonly` prevents accidental mutation
- **Pattern matching**: Works seamlessly with switch expressions

**CRITICAL: NO implicit conversions.** Implicit operators defeat the purpose of value objects by allowing silent type coercion.

---

### Pattern Matching (C# 8-12)

Leverage modern pattern matching for cleaner, more expressive code.

```csharp
// Switch expressions with property patterns
public string GetPaymentMethodDescription(PaymentMethod payment) => payment switch
{
    { Type: PaymentType.CreditCard, Last4: var last4 } => $"Credit card ending in {last4}",
    { Type: PaymentType.BankTransfer, AccountNumber: var account } => $"Bank transfer from {account}",
    { Type: PaymentType.Cash } => "Cash payment",
    _ => "Unknown payment method"
};

// Relational and logical patterns
public string ClassifyTemperature(int temp) => temp switch
{
    < 0 => "Freezing",
    >= 0 and < 10 => "Cold",
    >= 10 and < 20 => "Cool",
    >= 20 and < 30 => "Warm",
    >= 30 => "Hot",
    _ => throw new ArgumentOutOfRangeException(nameof(temp))
};

// List patterns (C# 11+)
public bool IsValidSequence(int[] numbers) => numbers switch
{
    [] => false,
    [_] => true,
    [var first, .., var last] when first < last => true,
    _ => false
};

// Type patterns with null checks
public string FormatValue(object? value) => value switch
{
    null => "null",
    string s => $"\"{s}\"",
    int i => i.ToString(),
    double d => d.ToString("F2"),
    DateTime dt => dt.ToString("yyyy-MM-dd"),
    IEnumerable<object> collection => $"[{string.Join(", ", collection)}]",
    _ => value.ToString() ?? "unknown"
};
```

---

### Nullable Reference Types (C# 8+)

Enable nullable reference types in your project and handle nulls explicitly.

```csharp
// In .csproj
<PropertyGroup>
    <Nullable>enable</Nullable>
</PropertyGroup>

// Pattern matching with null checks
public decimal GetDiscount(Customer? customer) => customer switch
{
    null => 0m,
    { IsVip: true } => 0.20m,
    { OrderCount: > 10 } => 0.10m,
    _ => 0.05m
};

// Null-coalescing patterns
public string GetDisplayName(User? user) =>
    user?.PreferredName ?? user?.Email ?? "Guest";

// Guard clauses with ArgumentNullException.ThrowIfNull (C# 11+)
public void ProcessOrder(Order? order)
{
    ArgumentNullException.ThrowIfNull(order);
    // order is now non-nullable in this scope
    Console.WriteLine(order.Id);
}
```

---

## Composition Over Inheritance

**Avoid abstract base classes and inheritance hierarchies.** Use composition and interfaces instead.

```csharp
// GOOD: Composition with interfaces
public interface IPaymentProcessor
{
    Task<PaymentResult> ProcessAsync(Money amount, CancellationToken cancellationToken);
}

public sealed class CreditCardProcessor : IPaymentProcessor
{
    private readonly IPaymentValidator _validator;
    private readonly ICreditCardGateway _gateway;

    public CreditCardProcessor(IPaymentValidator validator, ICreditCardGateway gateway)
    {
        _validator = validator;
        _gateway = gateway;
    }

    public async Task<PaymentResult> ProcessAsync(Money amount, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(amount, cancellationToken);
        if (!validation.IsValid)
            return PaymentResult.Failed(validation.Error);

        return await _gateway.ChargeAsync(amount, cancellationToken);
    }
}
```

**When inheritance is acceptable:**
- Framework requirements (e.g., `ControllerBase` in ASP.NET Core)
- Library integration (e.g., custom exceptions inheriting from `Exception`)

---

## Performance Patterns

### Async/Await Best Practices

```csharp
// Async all the way
public async Task<Order> GetOrderAsync(string orderId, CancellationToken cancellationToken)
{
    var order = await _repository.GetAsync(orderId, cancellationToken);
    return order;
}

// ValueTask for frequently-called, often-synchronous methods
public ValueTask<Order?> GetCachedOrderAsync(string orderId, CancellationToken cancellationToken)
{
    if (_cache.TryGetValue(orderId, out var order))
        return ValueTask.FromResult<Order?>(order);

    return GetFromDatabaseAsync(orderId, cancellationToken);
}

// Always accept CancellationToken
public async Task<List<Order>> GetOrdersAsync(
    string customerId,
    CancellationToken cancellationToken = default)
{
    return await _repository.GetOrdersByCustomerAsync(customerId, cancellationToken);
}
```

### Span<T> and Memory<T> for Zero-Allocation Code

```csharp
// Span<T> for synchronous, zero-allocation operations
public int ParseOrderId(ReadOnlySpan<char> input)
{
    if (!input.StartsWith("ORD-"))
        throw new FormatException("Invalid order ID format");

    var numberPart = input.Slice(4);
    return int.Parse(numberPart);
}

// Memory<T> for async operations (Span can't cross await)
public async Task<int> ReadDataAsync(
    Memory<byte> buffer, CancellationToken cancellationToken)
{
    return await _stream.ReadAsync(buffer, cancellationToken);
}

// ArrayPool for temporary large buffers
public async Task ProcessLargeFileAsync(Stream stream, CancellationToken cancellationToken)
{
    var buffer = ArrayPool<byte>.Shared.Rent(8192);
    try
    {
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            ProcessChunk(buffer.AsSpan(0, bytesRead));
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
```

---

## API Design Principles

### Accept Abstractions, Return Appropriately Specific

| Scenario | Accept | Return |
|----------|--------|--------|
| Only iterate once | `IEnumerable<T>` | `IEnumerable<T>` (if lazy) |
| Need count | `IReadOnlyCollection<T>` | `IReadOnlyCollection<T>` |
| Need indexing | `IReadOnlyList<T>` | `IReadOnlyList<T>` |
| High-performance, sync | `ReadOnlySpan<T>` | `Span<T>` (rarely) |
| Async streaming | `IAsyncEnumerable<T>` | `IAsyncEnumerable<T>` |
| Caller needs mutation | - | `List<T>`, `T[]` |

### Method Signatures Best Practices

```csharp
// Primary constructors (C# 12+) for simple classes
public sealed class OrderService(IOrderRepository repository, ILogger<OrderService> logger)
{
    public async Task<Order> GetOrderAsync(OrderId orderId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching order {OrderId}", orderId);
        return await repository.GetAsync(orderId, cancellationToken);
    }
}

// Use record for multiple related parameters
public record SearchOrdersRequest(
    string? CustomerId,
    DateTime? StartDate,
    DateTime? EndDate,
    OrderStatus? Status,
    int PageSize = 20,
    int PageNumber = 1
);
```

---

## Error Handling

### Result Type Pattern

For expected errors, use a `Result<T, TError>` type instead of exceptions.

**When to use Result vs Exceptions:**
- **Use Result**: Expected errors (validation, business rules, not found)
- **Use Exceptions**: Unexpected errors (network failures, system errors, programming bugs)

---

## Avoid Reflection-Based Metaprogramming

**Prefer statically-typed, explicit code over reflection-based "magic" libraries.**

### Banned Libraries

| Library | Problem |
|---------|---------|
| **AutoMapper** | Reflection magic, hidden mappings, runtime failures |
| **Mapster** | Same issues as AutoMapper |

### Use Explicit Mapping Methods Instead

```csharp
public static class UserMappings
{
    public static UserDto ToDto(this UserEntity entity) => new(
        Id: entity.Id.ToString(),
        Name: entity.FullName,
        Email: entity.EmailAddress);
}

// Usage - explicit and traceable
var dto = entity.ToDto();
```

---

## Anti-Patterns to Avoid

- Don't use mutable DTOs (use records)
- Don't use classes for value objects (use `readonly record struct`)
- Don't create deep inheritance hierarchies
- Don't ignore nullable reference type warnings
- Don't block on async code (`.Result`, `.Wait()`)
- Don't use `byte[]` when `Span<byte>` suffices
- Don't forget `CancellationToken` parameters
- Don't return mutable collections from APIs
- Don't throw exceptions for expected business errors

---

## Resources

- **C# Language Specification**: https://learn.microsoft.com/en-us/dotnet/csharp/
- **Pattern Matching**: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching
- **Span<T> and Memory<T>**: https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/
- **Async Best Practices**: https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming
