---
name: serialization
description: Choose the right serialization format for .NET applications. Prefer schema-based formats (Protobuf, MessagePack) over reflection-based (Newtonsoft.Json). Use System.Text.Json with AOT source generators for JSON scenarios.
invocable: false
---

# Serialization in .NET

## When to Use This Skill

Use this skill when:
- Choosing a serialization format for APIs, messaging, or persistence
- Migrating from Newtonsoft.Json to System.Text.Json
- Implementing AOT-compatible serialization
- Designing wire formats for distributed systems
- Optimizing serialization performance

---

## Schema-Based vs Reflection-Based

| Aspect | Schema-Based | Reflection-Based |
|--------|--------------|------------------|
| **Examples** | Protobuf, MessagePack, System.Text.Json (source gen) | Newtonsoft.Json, BinaryFormatter |
| **Type info in payload** | No (external schema) | Yes (type names embedded) |
| **Versioning** | Explicit field numbers/names | Implicit (type structure) |
| **Performance** | Fast (no reflection) | Slower (runtime reflection) |
| **AOT compatible** | Yes | No |
| **Wire compatibility** | Excellent | Poor |

**Recommendation**: Use schema-based serialization for anything that crosses process boundaries.

---

## Format Recommendations

| Use Case | Recommended Format | Why |
|----------|-------------------|-----|
| **REST APIs** | System.Text.Json (source gen) | Standard, AOT-compatible |
| **gRPC** | Protocol Buffers | Native format, excellent versioning |
| **Caching** | MessagePack | Compact, fast |
| **Configuration** | JSON (System.Text.Json) | Human-readable |
| **Logging** | JSON (System.Text.Json) | Structured, parseable |

### Formats to Avoid

| Format | Problem |
|--------|---------|
| **BinaryFormatter** | Security vulnerabilities, deprecated, never use |
| **Newtonsoft.Json default** | Type names in payload break on rename |
| **DataContractSerializer** | Complex, poor versioning |

---

## System.Text.Json with Source Generators

For JSON serialization, use System.Text.Json with source generators for AOT compatibility and performance.

### Setup

```csharp
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(OrderItem))]
[JsonSerializable(typeof(Customer))]
[JsonSerializable(typeof(List<Order>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonContext : JsonSerializerContext { }
```

### Usage

```csharp
// Serialize with context
var json = JsonSerializer.Serialize(order, AppJsonContext.Default.Order);

// Deserialize with context
var order = JsonSerializer.Deserialize(json, AppJsonContext.Default.Order);

// Configure in ASP.NET Core
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});
```

### Benefits

- **No reflection at runtime** - All type info generated at compile time
- **AOT compatible** - Works with Native AOT publishing
- **Faster** - No runtime type analysis
- **Trim-safe** - Linker knows exactly what's needed

---

## Migrating from Newtonsoft.Json

### Common Issues

| Newtonsoft | System.Text.Json | Fix |
|------------|------------------|-----|
| `$type` in JSON | Not supported by default | Use discriminators or custom converters |
| `JsonProperty` | `JsonPropertyName` | Different attribute |
| `DefaultValueHandling` | `DefaultIgnoreCondition` | Different API |
| Private setters | Requires `[JsonInclude]` | Explicit opt-in |
| Polymorphism | `[JsonDerivedType]` (.NET 7+) | Explicit discriminators |

### Migration Pattern

```csharp
// System.Text.Json (source-gen compatible)
public sealed record Order(
    [property: JsonPropertyName("order_id")]
    string Id,
    string? Notes
);

[JsonSerializable(typeof(Order))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class OrderJsonContext : JsonSerializerContext { }
```

### Polymorphism with Discriminators (.NET 7+)

```csharp
[JsonDerivedType(typeof(CreditCardPayment), "credit_card")]
[JsonDerivedType(typeof(BankTransferPayment), "bank_transfer")]
public abstract record Payment(decimal Amount);

public sealed record CreditCardPayment(decimal Amount, string Last4) : Payment(Amount);
public sealed record BankTransferPayment(decimal Amount, string AccountNumber) : Payment(Amount);
```

---

## Wire Compatibility Patterns

### Tolerant Reader

Old code must safely ignore unknown fields:

```csharp
// System.Text.Json: Configure to allow
var options = new JsonSerializerOptions
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
};
```

### Never Embed Type Names

```csharp
// BAD: Type name in payload - renaming class breaks wire format
{ "$type": "MyApp.Order, MyApp", "Id": 123 }

// GOOD: Explicit discriminator - refactoring safe
{ "type": "order", "id": 123 }
```

---

## Performance Comparison

| Format | Serialize | Deserialize | Size |
|--------|-----------|-------------|------|
| MessagePack | Fast | Fast | Small |
| Protobuf | Fast | Fast | Small |
| System.Text.Json (source gen) | Good | Good | Medium |
| System.Text.Json (reflection) | OK | OK | Medium |
| Newtonsoft.Json | Slow | Slow | Medium |

---

## Best Practices

### DO

- Use source generators for System.Text.Json
- Use explicit field numbers/keys for binary formats
- Use records for immutable message types
- Use the Tolerant Reader pattern

### DON'T

- Don't use BinaryFormatter (ever)
- Don't embed type names in wire format
- Don't use reflection serialization for hot paths

---

## Resources

- **System.Text.Json Source Generation**: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
- **Protocol Buffers**: https://protobuf.dev/
- **MessagePack-CSharp**: https://github.com/MessagePack-CSharp/MessagePack-CSharp
