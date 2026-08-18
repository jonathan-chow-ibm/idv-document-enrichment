---
name: testing
description: Write unit, integration, and end-to-end tests for .NET APIs and React frontends. USE FOR creating xUnit tests, integration tests with WebApplicationFactory and Testcontainers, Vitest component and hook tests with React Testing Library, and Playwright end-to-end tests driven through the Playwright MCP server. Trigger phrases include "write tests", "unit test", "integration test", "e2e test", "end-to-end test", "Playwright test", "xUnit", "Vitest", "React Testing Library", "test coverage", "TDD".
---

# Testing

Guidelines for writing tests in this workspace: .NET (xUnit), React (Vitest + React Testing Library), and end-to-end (Playwright via MCP).

## Core Principles

- Test **behavior**, not implementation. A refactor that preserves behavior should not break tests.
- **Arrange / Act / Assert** — one logical assertion group per test.
- Test names describe the scenario and expectation: `Returns404_WhenProductIsMissing`, `disables submit button when form is invalid`.
- Prefer **integration tests over heavy mocking** for the API. Prefer **user-facing assertions** over DOM-shape assertions for React.
- Tests are first-class code — same naming, formatting, and review standards as production code.

## Test Pyramid (this repo)

| Layer       | Tool                                                    | What it covers                                                     |
| ----------- | ------------------------------------------------------- | ------------------------------------------------------------------ |
| Unit        | xUnit (C#), Vitest (TS)                                 | Pure domain logic, small utilities, custom hooks                   |
| Integration | `WebApplicationFactory` + Testcontainers / Vitest + MSW | API endpoints against real DB / React features against mocked HTTP |
| End-to-end  | **Playwright MCP**                                      | Full user flows in a real browser against the running app          |

Write the cheapest test that meaningfully exercises the behavior. Don't write an E2E for what a unit test can verify.

---

## .NET Testing (xUnit)

### Project Layout

```
tests/
  Api.UnitTests/        # Fast, no I/O
  Api.IntegrationTests/ # WebApplicationFactory + Testcontainers
```

Test project naming: `<ProjectUnderTest>.UnitTests` / `.IntegrationTests`.

### Unit Test Pattern

```csharp
public class ProductServiceTests
{
    [Fact]
    public async Task GetById_ReturnsNull_WhenProductDoesNotExist()
    {
        // Arrange
        var repo = Substitute.For<IProductRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        var sut = new ProductService(repo);

        // Act
        var result = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
```

Stack: **xUnit** + **FluentAssertions** + **NSubstitute**. Avoid Moq unless already in the project.

### Integration Test Pattern

```csharp
public class ProductsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_CreatesProduct_AndReturns201()
    {
        var request = new CreateProductRequest("Widget", 9.99m);

        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ProductDto>();
        body!.Name.Should().Be("Widget");
    }
}
```

- Use **`WebApplicationFactory<TEntryPoint>`** — make `Program` partial or expose an `IApiMarker`.
- Use **Testcontainers** for SQL Server/PostgreSQL; do **not** use the EF in-memory provider (different semantics).
- Reset DB state per test class (Respawn) or wrap each test in a transaction.
- No sleeping, no shared mutable static state, no order-dependent tests.

### Don'ts

- Don't unit-test controllers — cover them with integration tests.
- Don't assert on log output unless logging _is_ the feature.
- Don't mock `DbContext` — use a real DB in a container.

---

## React Testing (Vitest + React Testing Library)

### Project Layout

Colocate tests next to the code: `ProductCard.tsx` + `ProductCard.test.tsx`.

### Component Test Pattern

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ProductCard } from "./ProductCard";

describe("ProductCard", () => {
  it("calls onAddToCart with the product id when the button is clicked", async () => {
    const user = userEvent.setup();
    const onAdd = vi.fn();
    render(
      <ProductCard
        product={{ id: "p1", name: "Widget", price: 9.99 }}
        onAddToCart={onAdd}
      />,
    );

    await user.click(screen.getByRole("button", { name: /add to cart/i }));

    expect(onAdd).toHaveBeenCalledWith("p1");
  });
});
```

### Rules

- Query by **accessible role / label / text** — never by class name or test id unless there's no alternative.
- Use `userEvent` (not `fireEvent`) to drive interactions.
- Test **custom hooks** with `renderHook` from `@testing-library/react`.
- Mock HTTP with **MSW** at the network boundary, not by stubbing fetch/query clients.
- Don't snapshot large DOM trees — they rot. Snapshot small, stable outputs only.

---

## End-to-End Testing (Playwright via MCP)

**All E2E tests in this repo are driven through the Playwright MCP server.** The Coder agent must use Playwright MCP tools to:

1. **Explore the running app** interactively to design the test scenario
2. **Record / verify the selectors and flows** before committing them to code
3. **Produce Playwright test files** (`*.spec.ts`) that mirror what was verified through MCP

### Workflow

1. Ensure the app is running locally (or delegate to Verifier to start it).
2. Use Playwright MCP to open the app, navigate, and interact — capture the page structure with `read_page` / snapshots before writing selectors.
3. Confirm each critical assertion works via MCP (element visible, URL changed, API state updated).
4. Translate the verified interactions into a `.spec.ts` file under `e2e/` (or the repo's existing Playwright folder).
5. Run the spec with `npx playwright test` and iterate until green.

### Test File Pattern

```ts
import { expect, test } from "@playwright/test";

test.describe("Products", () => {
  test("user can create a product", async ({ page }) => {
    await page.goto("/products");

    await page.getByRole("button", { name: "New product" }).click();
    await page.getByLabel("Name").fill("Widget");
    await page.getByLabel("Price").fill("9.99");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("row", { name: /Widget/ })).toBeVisible();
  });
});
```

### Rules

- **Selectors**: prefer role/label/text, same as React Testing Library. Fall back to `data-testid` only when semantics aren't available.
- **No hard waits** (`waitForTimeout`). Use web-first assertions (`expect(locator).toBeVisible()`) or `waitFor` on network events.
- **Isolation**: each test starts from a known state. Use API calls or DB seeding (not UI clicks) to set up preconditions.
- **Parallel-safe**: no shared users, no shared fixture data that mutates.
- **Traces on failure** enabled in `playwright.config.ts` so failures are diagnosable.
- **One flow per test.** If the setup is expensive, use `test.beforeEach` or storage state — don't chain unrelated scenarios.

### When to write an E2E

- Critical user journeys (login, checkout, the primary "happy path")
- Flows that cross multiple systems (frontend ↔ API ↔ DB)
- Regressions for bugs that only reproduce in the browser

### When NOT to write an E2E

- Component logic already covered by RTL tests
- Business rules already covered by API integration tests
- Exhaustive permutations (use unit/integration for breadth; E2E for depth on one path)

---

## Coverage & Quality

- Coverage is a signal, not a target. Don't chase 100%.
- A change without a test is a change without a safety net — every bug fix adds a regression test first.
- Flaky tests get **fixed or quarantined**, never retried-until-green.
