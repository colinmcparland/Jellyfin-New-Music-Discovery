---
name: dependency-injection-patterns
description: Organize DI registrations using IServiceCollection extension methods. Group related services into composable Add* methods for clean Program.cs and reusable configuration in tests.
invocable: false
---

# Dependency Injection Patterns

## When to Use This Skill

Use this skill when:
- Organizing service registrations in ASP.NET Core applications
- Avoiding massive Program.cs/Startup.cs files with hundreds of registrations
- Making service configuration reusable between production and tests
- Designing libraries that integrate with Microsoft.Extensions.DependencyInjection

---

## The Solution: Extension Method Composition

Group related registrations into extension methods:

```csharp
// Clean, composable Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddUserServices()
    .AddOrderServices()
    .AddEmailServices()
    .AddPaymentServices();

var app = builder.Build();
```

---

## Extension Method Pattern

### Basic Structure

```csharp
namespace MyApp.Users;

public static class UserServiceCollectionExtensions
{
    public static IServiceCollection AddUserServices(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
```

### With Configuration

```csharp
namespace MyApp.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        string configSectionName = "EmailSettings")
    {
        services.AddOptions<EmailOptions>()
            .BindConfiguration(configSectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IEmailLinkGenerator, EmailLinkGenerator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
```

---

## File Organization

Place extension methods near the services they register:

```
src/
  MyApp.Api/
    Program.cs                    # Composes all Add* methods
  MyApp.Users/
    Services/
      UserService.cs
    UserServiceCollectionExtensions.cs   # AddUserServices()
  MyApp.Orders/
    Services/
      OrderService.cs
    OrderServiceCollectionExtensions.cs  # AddOrderServices()
```

**Convention**: `{Feature}ServiceCollectionExtensions.cs` next to the feature's services.

---

## Naming Conventions

| Pattern | Use For |
|---------|---------|
| `Add{Feature}Services()` | General feature registration |
| `Add{Feature}()` | Short form when unambiguous |
| `Configure{Feature}()` | When primarily setting options |
| `Use{Feature}()` | Middleware (on IApplicationBuilder) |

---

## Common Patterns

### Conditional Registration

```csharp
public static IServiceCollection AddEmailServices(
    this IServiceCollection services,
    IHostEnvironment environment)
{
    services.AddSingleton<IEmailComposer, MjmlEmailComposer>();

    if (environment.IsDevelopment())
        services.AddSingleton<IEmailSender, MailpitEmailSender>();
    else
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

    return services;
}
```

### Factory-Based Registration

```csharp
public static IServiceCollection AddPaymentServices(
    this IServiceCollection services,
    string configSection = "Stripe")
{
    services.AddOptions<StripeOptions>()
        .BindConfiguration(configSection)
        .ValidateOnStart();

    services.AddSingleton<IPaymentProcessor>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
        var logger = sp.GetRequiredService<ILogger<StripePaymentProcessor>>();
        return new StripePaymentProcessor(options.ApiKey, options.WebhookSecret, logger);
    });

    return services;
}
```

### Keyed Services (.NET 8+)

```csharp
public static IServiceCollection AddNotificationServices(this IServiceCollection services)
{
    services.AddKeyedSingleton<INotificationSender, EmailNotificationSender>("email");
    services.AddKeyedSingleton<INotificationSender, SmsNotificationSender>("sms");
    services.AddKeyedSingleton<INotificationSender, PushNotificationSender>("push");
    services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

    return services;
}
```

---

## Lifetime Management

| Lifetime | Use When | Examples |
|----------|----------|----------|
| **Singleton** | Stateless, thread-safe, expensive to create | Configuration, HttpClient factories, caches |
| **Scoped** | Stateful per-request, database contexts | DbContext, repositories, user context |
| **Transient** | Lightweight, stateful, cheap to create | Validators, short-lived helpers |

### Common Mistakes

```csharp
// BAD: Singleton captures scoped service - stale DbContext!
public class CacheService  // Registered as Singleton
{
    private readonly IUserRepository _repo;  // Scoped - captured at startup!
}

// GOOD: Inject factory or IServiceProvider
public class CacheService
{
    private readonly IServiceProvider _serviceProvider;

    public async Task<User> GetUserAsync(string id)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        return await repo.GetByIdAsync(id);
    }
}
```

---

## Anti-Patterns

- Don't register everything in Program.cs (use extension methods)
- Don't create overly generic `AddServices()` methods
- Don't hide important configuration (accept parameters explicitly)
- Don't inject scoped services into singletons
- Don't forget to create scopes in background services

---

## Resources

- **Microsoft.Extensions.DependencyInjection**: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection
- **Options Pattern**: https://learn.microsoft.com/en-us/dotnet/core/extensions/options
