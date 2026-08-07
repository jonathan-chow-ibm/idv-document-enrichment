---
name: dotnet-webapi
description: Write, review, or refactor ASP.NET Core Web API code following idiomatic .NET patterns and Clean Code principles. USE FOR creating controllers, minimal APIs, DTOs, dependency injection setup, configuration, validation, model binding, filters, middleware, problem details error handling, API versioning, OpenAPI/Swagger, and async controller actions. Trigger phrases include "ASP.NET Core controller", ".NET Web API", "Web API endpoint", "minimal API", "add controller", "refactor API", "API best practices", "ProblemDetails", "model validation".
---

# ASP.NET Core Web API

Guidelines for writing idiomatic, maintainable ASP.NET Core Web API code on **.NET 10**.

## Core Principles

- Keep controllers thin — they route, validate, and translate; they don't contain business logic
- Use async/await end-to-end for any I/O (DB, HTTP, file); never `.Result` or `.Wait()`
- Nullable reference types ON; treat warnings as errors where possible
- Constructor injection only; no service locator, no static singletons for services
- DTOs are `record` types; entities and DTOs are separate
- Return typed results: `Results<Ok<T>, NotFound, ValidationProblem>` for minimal APIs, `ActionResult<T>` for controllers

## Controller Pattern

```csharp
[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(
        CreateProductRequest request,
        CancellationToken ct)
    {
        var created = await products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
```

**Rules:**

- `sealed` classes by default
- Primary constructors for DI (C# 12+)
- Always accept and forward `CancellationToken`
- Route constraints (`{id:guid}`, `{id:int}`) are required, not optional
- `[ProducesResponseType]` for every status the action can return
- No `try/catch` in controllers — let middleware handle it

## DTOs and Validation

```csharp
public sealed record CreateProductRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Range(0.01, 100_000)] decimal Price);
```

Prefer FluentValidation for anything beyond trivial rules. Register validators via DI; don't call them manually in controllers.

## Error Handling

Use `ProblemDetails` (RFC 7807) for all errors. Configure once:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
app.UseExceptionHandler();
```

Throw domain-specific exceptions from services; translate them in an `IExceptionHandler`. Never return raw exception messages to clients.

## Program.cs Structure

Keep `Program.cs` short. Group registrations into extension methods:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddApiServices(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddAuthenticationAndAuthorization(builder.Configuration);

var app = builder.Build();
app.UseApiPipeline();
app.MapControllers();
app.Run();
```

## Configuration

- Bind configuration sections to `record` options classes with `IOptions<T>` / `IOptionsSnapshot<T>`
- Use `ValidateDataAnnotations().ValidateOnStart()` so misconfiguration fails fast
- Never read `IConfiguration` directly inside services

## Common Anti-patterns

- Fat controllers with business logic or direct `DbContext` use
- Returning entities instead of DTOs (leaks persistence model, over-posting risk)
- `async void`, blocking calls, missing `CancellationToken`
- Catching `Exception` in controllers
- Using `dynamic` or `object` in public APIs
- Manual JSON serialization inside actions
- Adding a repository layer on top of EF Core just for the sake of it — `DbContext` is already a Unit of Work + Repository

## Testing

- Integration tests via `WebApplicationFactory<TEntryPoint>` over mocking
- Unit test services and domain logic, not controllers
- Use `Testcontainers` for real DB in integration tests when feasible
