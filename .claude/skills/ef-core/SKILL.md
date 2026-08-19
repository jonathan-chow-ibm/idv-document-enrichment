---
name: ef-core
description: Design and write Entity Framework Core data access code — DbContext setup, entity configuration, migrations, and queries. USE FOR creating or refactoring DbContext, IEntityTypeConfiguration classes, fluent API mappings, migrations, async queries, projections, tracking vs no-tracking, Include/ThenInclude, performance tuning (N+1, cartesian explosion, AsSplitQuery), concurrency tokens, and value converters. Trigger phrases include "DbContext", "EF Core", "Entity Framework", "migration", "add migration", "LINQ query", "IQueryable", "AsNoTracking", "Include", "value converter", "EF Core performance".
---

# Entity Framework Core

Guidelines for writing idiomatic, performant EF Core 8+ data access code.

## Core Principles

- `DbContext` is a Unit of Work and a Repository — **do not** wrap it in another repository layer without a real reason
- One `DbContext` per logical bounded context; don't share one giant context across the app
- Async everywhere for I/O: `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`
- Always pass `CancellationToken` through
- Configure entities with `IEntityTypeConfiguration<T>`, not inline in `OnModelCreating`
- Register `DbContext` as **scoped** (default); never singleton

## DbContext Setup

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

Registration:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options
        .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
        .UseSnakeCaseNamingConvention());
```

## Entity Configuration

Keep mapping out of the entity class:

```csharp
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.RowVersion).IsRowVersion();
        builder.HasIndex(p => p.Sku).IsUnique();
    }
}
```

## Queries

**Read defaults:**
- Use `AsNoTracking()` for read-only queries (everything returning DTOs)
- **Project** with `Select` to DTOs — don't return entities from query methods
- Don't `Include` what you won't use

```csharp
public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct) =>
    await dbContext.Products
        .AsNoTracking()
        .Where(p => p.Id == id)
        .Select(p => new ProductDto(p.Id, p.Name, p.Price))
        .FirstOrDefaultAsync(ct);
```

**Write path:**
- Load with tracking, mutate the entity, `SaveChangesAsync`
- Never `Update()` a detached entity you loaded with `AsNoTracking` — bugs and over-writes

## Includes and Performance

- Single collection + owner → `Include`
- Multiple collections → `AsSplitQuery()` to avoid cartesian explosion
- N+1? Look for a `foreach` calling the DB; rewrite as one query with `Include` or `Select`

```csharp
var orders = await dbContext.Orders
    .AsNoTracking()
    .Include(o => o.Lines)
    .Include(o => o.Customer)
    .AsSplitQuery()
    .Where(o => o.CreatedAt >= since)
    .ToListAsync(ct);
```

## Migrations

- One migration per logical schema change with a descriptive name
- Review the generated migration before committing; hand-edit if needed
- Check destructive operations (`DropColumn`, `DropTable`) — EF won't warn loudly
- Never edit an applied migration; add a new one instead

```bash
dotnet ef migrations add AddProductSkuIndex --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

## Concurrency

Add a `RowVersion` (`byte[]`) or concurrency token to anything mutable by multiple users. Catch `DbUpdateConcurrencyException` and surface a 409 to the API.

## Value Converters and Owned Types

- Strongly-typed IDs, enums-as-strings, `DateOnly`/`TimeOnly` → use value converters
- Value objects (e.g., `Address`, `Money`) → `OwnsOne` / `OwnsMany`

## Transactions

`SaveChangesAsync` is already transactional. Only start an explicit transaction when you need to span multiple `SaveChangesAsync` calls or multiple contexts. Use `IDbContextTransaction` with `await using`.

## Common Anti-patterns

- Generic `IRepository<T>` on top of `DbSet<T>` — adds nothing, hides EF features
- Returning `IQueryable<T>` from services (leaks persistence concerns up the stack)
- `.ToList()` early, then filtering in memory
- `Include` chains that pull half the database
- Tracking queries for read-only paths (wastes memory, slower)
- Sync methods (`ToList`, `First`) on async paths
- Re-attaching DTOs as entities via `Update()` — risks overwriting unloaded columns
- One mega-context with 100+ `DbSet`s

## Testing

- Integration tests against a real provider (SQL Server in Testcontainers, or PostgreSQL) — **not** the in-memory provider, which behaves differently from real SQL
- Reset the database per test class (respawn or transaction rollback)
- Unit-test pure domain logic on entities without EF
