---
name: api-design
description: Design stable, compatible public APIs using extend-only design principles. Manage API compatibility, wire compatibility, and versioning for NuGet packages and distributed systems.
invocable: false
---

# Public API Design and Compatibility

## When to Use This Skill

Use this skill when:
- Designing public APIs for NuGet packages or libraries
- Making changes to existing public APIs
- Planning wire format changes for distributed systems
- Implementing versioning strategies
- Reviewing pull requests for breaking changes

---

## The Three Types of Compatibility

| Type | Definition | Scope |
|------|------------|-------|
| **API/Source** | Code compiles against newer version | Public method signatures, types |
| **Binary** | Compiled code runs against newer version | Assembly layout, method tokens |
| **Wire** | Serialized data readable by other versions | Network protocols, persistence formats |

---

## Extend-Only Design

The foundation of stable APIs: **never remove or modify, only extend**.

### Three Pillars

1. **Previous functionality is immutable** - Once released, behavior and signatures are locked
2. **New functionality through new constructs** - Add overloads, new types, opt-in features
3. **Removal only after deprecation period** - Years, not releases

---

## API Change Guidelines

### Safe Changes (Any Release)

```csharp
// ADD new overloads with default parameters
public void Process(Order order, CancellationToken ct = default);

// ADD new optional parameters to existing methods
public void Send(Message msg, Priority priority = Priority.Normal);

// ADD new types, interfaces, enums
public interface IOrderValidator { }

// ADD new members to existing types
public class Order
{
    public DateTimeOffset? ShippedAt { get; init; }  // NEW
}
```

### Unsafe Changes (Never or Major Version Only)

```csharp
// REMOVE or RENAME public members
// CHANGE parameter types or order
// CHANGE return types
// CHANGE access modifiers
// ADD required parameters without defaults
```

### Deprecation Pattern

```csharp
// Step 1: Mark as obsolete with version (any release)
[Obsolete("Obsolete since v1.5.0. Use ProcessAsync instead.")]
public void Process(Order order) { }

// Step 2: Add new recommended API (same release)
public Task ProcessAsync(Order order, CancellationToken ct = default);

// Step 3: Remove in next major version (v2.0+)
```

---

## Encapsulation Patterns

### Sealing Classes

```csharp
// DO: Seal classes not designed for inheritance
public sealed class OrderProcessor { }

// DON'T: Leave unsealed by accident
public class OrderProcessor { }  // Users might inherit, blocking changes
```

### Interface Segregation

```csharp
// DO: Small, focused interfaces
public interface IOrderReader
{
    Order? GetById(OrderId id);
}

public interface IOrderWriter
{
    Task SaveAsync(Order order);
}

// DON'T: Monolithic interfaces (can't add methods without breaking)
public interface IOrderRepository
{
    Order? GetById(OrderId id);
    Task SaveAsync(Order order);
    // Adding new methods breaks all implementations!
}
```

---

## Wire Compatibility

For distributed systems, serialized data must be readable across versions.

### Requirements

| Direction | Requirement |
|-----------|-------------|
| **Backward** | Old writers -> New readers (current version reads old data) |
| **Forward** | New writers -> Old readers (old version reads new data) |

### Safely Evolving Wire Formats

**Phase 1: Add read-side support (opt-in)**
**Phase 2: Enable write-side (opt-out, next minor version)**
**Phase 3: Make default (future version)**

### Never Embed Type Names

```csharp
// BAD: Type name in payload - renaming class breaks wire format
{ "$type": "MyApp.Order, MyApp", "Id": 123 }

// GOOD: Explicit discriminator - refactoring safe
{ "type": "order", "id": 123 }
```

---

## Versioning Strategy

### Semantic Versioning (Practical)

| Version | Changes Allowed |
|---------|----------------|
| **Patch** (1.0.x) | Bug fixes, security patches |
| **Minor** (1.x.0) | New features, deprecations |
| **Major** (x.0.0) | Breaking changes, old API removal |

---

## Pull Request Checklist

When reviewing PRs that touch public APIs:

- [ ] **No removed public members** (use `[Obsolete]` instead)
- [ ] **No changed signatures** (add overloads instead)
- [ ] **No new required parameters** (use defaults)
- [ ] **Wire format changes are opt-in** (read-side first)
- [ ] **Breaking changes documented** (release notes, migration guide)

---

## Resources

- [Extend-Only Design](https://aaronstannard.com/extend-only-design/)
- [OSS Compatibility Standards](https://aaronstannard.com/oss-compatibility-standards/)
- [Semantic Versioning](https://semver.org/)
