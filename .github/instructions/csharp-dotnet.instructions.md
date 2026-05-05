---
description: "Use when writing, reviewing, or refactoring C# code, .NET Core projects, ASP.NET, Worker Services, Dependency Injection, Entity Framework, or any .NET-related task. Covers naming conventions, async/await, DI patterns, configuration, error handling, and project structure."
applyTo: "**/*.cs"
---

# C# and .NET Core Guidelines

## Naming Conventions

- **Classes / Interfaces / Records**: PascalCase → `OrderService`, `IOrderRepository`
- **Methods and Properties**: PascalCase → `GetOrderById()`, `TotalAmount`
- **Private fields**: camelCase with underscore prefix → `_orderRepository`
- **Local variables and parameters**: camelCase → `orderId`, `cancellationToken`
- **Constants**: PascalCase → `MaxRetryCount`
- **Async methods**: suffix with `Async` → `GetOrderByIdAsync()`

## Project Structure

- Separate concerns by layer: `Domain / Models`, `Application / Business`, `Infrastructure`, `API / Worker`
- Each project in its own `.csproj`; keep cross-cutting models in a shared `Models` project
- Never reference `Infrastructure` from `Business`/`Application` directly; use interfaces

## Async / Await

- Always propagate `CancellationToken` through the call chain
- Never use `.Result` or `.Wait()` on tasks — use `await`
- Use `ConfigureAwait(false)` in library/infrastructure code
- Prefer `IAsyncEnumerable<T>` for streaming results

```csharp
// Good
public async Task<Order> GetOrderAsync(int id, CancellationToken ct)
{
    return await _repository.FindAsync(id, ct).ConfigureAwait(false);
}
```

## Dependency Injection

- Register services in `Program.cs` using `builder.Services`
- Prefer constructor injection; avoid service locator pattern
- Use correct lifetime: `Singleton` for stateless shared services, `Scoped` for per-request, `Transient` for lightweight stateless

```csharp
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<IBarcodeService, BarcodeService>();
```

## Configuration

- Read configuration via `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>` — never `IConfiguration` directly in business logic
- Bind strongly-typed settings in `Program.cs`:

```csharp
builder.Services.Configure<RutasSettings>(builder.Configuration.GetSection("Rutas"));
```

- Keep secrets out of `appsettings.json`; use environment variables, User Secrets (`dotnet user-secrets`), or Azure Key Vault

## Error Handling

- Use specific exceptions over generic `Exception`
- Validate at system boundaries (controllers, hosted service entry points)
- Use `ILogger<T>` for structured logging — never `Console.WriteLine` in production code

```csharp
_logger.LogError(ex, "Failed to process file {FileName}", fileName);
```

## Records and Immutability

- Use `record` for DTOs and value objects
- Prefer `init` properties for immutable data

```csharp
public record OrderDto(int Id, string Status, decimal Total);
```

## Null Safety

- Enable `<Nullable>enable</Nullable>` in all `.csproj` files
- Use `?.` and `??` operators; avoid explicit null checks where possible
- Document nullable intent with `?` annotation on reference types

## Collections

- Return `IReadOnlyList<T>` or `IEnumerable<T>` from methods; expose `List<T>` only when mutation is required
- Prefer LINQ over manual loops for transformations; avoid LINQ in hot paths

## Worker Services (.NET BackgroundService)

- Override `ExecuteAsync(CancellationToken stoppingToken)` and respect `stoppingToken`
- Use `IHostApplicationLifetime` to handle graceful shutdown
- Log start/stop lifecycle events

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("Worker started at {Time}", DateTimeOffset.UtcNow);
    while (!stoppingToken.IsCancellationRequested)
    {
        await DoWorkAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
}
```

## Testing

- Follow Arrange-Act-Assert structure
- Use `xUnit` with `FluentAssertions`
- Mock dependencies with `Moq` or `NSubstitute`
- Name tests: `MethodName_StateUnderTest_ExpectedBehavior`
