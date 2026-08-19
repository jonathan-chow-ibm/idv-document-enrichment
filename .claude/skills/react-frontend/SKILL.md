---
name: react-frontend
description: Write, review, or refactor React frontend code using function components, hooks, and TypeScript. USE FOR creating components, custom hooks, managing state (local, context, server state), routing, forms, data fetching, component composition, prop typing, accessibility, and performance optimization (memoization, code splitting). Trigger phrases include "React component", "React hook", "useState", "useEffect", "custom hook", "React form", "React TypeScript", "component refactor", "React best practices", "Vite React".
---

# React Frontend

Guidelines for writing idiomatic, maintainable React code with TypeScript (Vite or similar modern toolchain).

## Core Principles

- Function components only — no class components
- TypeScript everywhere; `any` is a code smell, use `unknown` at boundaries
- Components are small, focused, and composable
- Derive state, don't duplicate it; compute values during render when cheap
- Keep server state out of `useState` — use a data-fetching library (TanStack Query, SWR)
- Prefer composition (`children`, render props, slots) over configuration flags

## Component Pattern

```tsx
type ProductCardProps = {
  product: Product;
  onAddToCart: (id: string) => void;
};

export function ProductCard({ product, onAddToCart }: ProductCardProps) {
  return (
    <article className="product-card">
      <h3>{product.name}</h3>
      <p>{formatPrice(product.price)}</p>
      <button type="button" onClick={() => onAddToCart(product.id)}>
        Add to cart
      </button>
    </article>
  );
}
```

**Rules:**

- Named exports over default exports (better refactoring, consistent imports)
- Props are a `type` alias (not `interface`) unless extension is needed
- No `React.FC` — it adds implicit `children` and obscures the signature
- Always specify `type="button"` on `<button>` unless it's a form submit
- Event handlers named `on*` for props, `handle*` for local functions

## Hooks

**Rules of Hooks:**

- Only call at the top level
- Only call from React functions (components or custom hooks)
- Custom hooks start with `use`

**When to extract a custom hook:**

- Logic is reused across components
- A component has multiple unrelated `useEffect`s or stateful concerns
- You want to unit-test logic in isolation

```tsx
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(id);
  }, [value, delayMs]);
  return debounced;
}
```

## State Management

| State type                 | Where it lives                                                     |
| -------------------------- | ------------------------------------------------------------------ |
| UI/local state             | `useState`, `useReducer` in the component that owns it             |
| Shared UI state            | Context (split by concern; don't put everything in one context)    |
| Server state               | TanStack Query / SWR — caching, revalidation, loading/error states |
| URL state                  | Router (search params, path params) — shareable, bookmarkable      |
| Global client state (rare) | Zustand or similar; avoid Redux unless justified                   |

**Do not** store server data in `useState` + `useEffect`. That path leads to stale data, race conditions, and manual cache invalidation.

## Effects

`useEffect` is for _synchronizing with external systems_, not for deriving data.

- ❌ Computing derived values in `useEffect`
- ❌ Resetting state when a prop changes (use `key` instead)
- ❌ Fetching data manually (use a data-fetching library)
- ✅ Subscribing to browser APIs, setting up timers, integrating non-React libraries

Always return a cleanup function when you subscribe to anything.

## Forms

Use React Hook Form + Zod (or Valibot) for anything beyond a trivial form:

- Uncontrolled inputs for performance
- Schema-based validation with type inference
- One source of truth for shape and rules

## Performance

Measure before optimizing.

- `React.memo`, `useMemo`, `useCallback` only when profiling shows a problem
- Virtualize long lists (`@tanstack/react-virtual`)
- Lazy-load routes and heavy components (`React.lazy` + `Suspense`)
- Stable keys in lists — never array index if the list can reorder

## Styling (Tailwind v4)

- Utilities in `className`, ordered roughly: layout → box → typography → color → state (`hover:`, `focus:`)
- Extract to a component when a className gets long or is reused 2+ times — not to a `@apply` rule
- Use `clsx` or `tailwind-merge` for conditional classes
- Theme tokens live in CSS via `@theme`:

```css
/* src/index.css */
@import "tailwindcss";

@theme {
  --color-brand-500: oklch(0.6 0.2 250);
  --font-sans: "Inter", system-ui, sans-serif;
}
```

- No `tailwind.config.js` unless you need JS plugins
- Dark mode: `@variant dark (...)` in CSS, not `darkMode: 'class'` in JS config

## Accessibility

- Semantic HTML first (`<button>`, `<nav>`, `<main>`, `<label>` for inputs)
- Every interactive element reachable and operable by keyboard
- `aria-*` attributes only when semantic HTML can't express the intent
- Images have meaningful `alt`; decorative images use `alt=""`

## File Organization

```
src/
  features/
    products/
      ProductList.tsx
      ProductCard.tsx
      useProducts.ts
      products.api.ts
      products.types.ts
  shared/
    components/
    hooks/
    lib/
  app/
    routes.tsx
    App.tsx
```

Colocate by feature, not by file type. A component, its hook, its types, and its tests live together.

## Common Anti-patterns

- Fetching in `useEffect` without cancellation
- Derived state stored in `useState` (sync bugs)
- Huge components doing fetching + rendering + form handling
- `useEffect` with missing deps silenced by `// eslint-disable-next-line`
- Default exports making renames painful
- Prop drilling more than 2 levels — reach for context or composition
