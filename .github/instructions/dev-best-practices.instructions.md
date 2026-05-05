---
description: "Use when reviewing code quality, designing APIs, writing tests, managing Git commits, structuring services, applying SOLID principles, clean code, refactoring, code reviews, or any general software engineering best-practice topic."
---

# Software Development Best Practices

## SOLID Principles

| Principle | Rule |
|-----------|------|
| **S** — Single Responsibility | One class = one reason to change |
| **O** — Open/Closed | Open for extension, closed for modification (use interfaces/abstractions) |
| **L** — Liskov Substitution | Subtypes must be replaceable for their base types |
| **I** — Interface Segregation | Prefer small, focused interfaces over large, general ones |
| **D** — Dependency Inversion | Depend on abstractions, not concrete implementations |

## Clean Code Rules

- **Names communicate intent**: `CalculateTotalDiscount()` not `Calc()` or `DoStuff()`
- **Functions do one thing**: If a method description contains "and", split it
- **Avoid magic numbers/strings**: Replace with named constants or enums
- **Keep methods short**: Aim for ≤20 lines; extract if logic branches deeply
- **Avoid deep nesting**: Use early returns (guard clauses) instead of nested `if/else`

```csharp
// Bad
if (user != null) {
    if (user.IsActive) {
        if (user.HasPermission("admin")) { /* ... */ }
    }
}

// Good (guard clauses)
if (user is null) return;
if (!user.IsActive) return;
if (!user.HasPermission("admin")) return;
// main logic here
```

## Error Handling

- Fail fast at boundaries; let exceptions propagate unless you can genuinely recover
- Log errors with enough context to reproduce: include IDs, file names, operation names
- Never swallow exceptions silently: at minimum log them
- Return `Result<T>` / `OneOf` patterns instead of throwing for expected/business failures

## API Design

- Use consistent resource naming: plural nouns (`/orders`, `/files`)
- Validate input at the entry point; don't pass invalid state deeper
- Use DTOs to decouple the internal model from the API surface
- Version APIs from day one (`/api/v1/`)
- Return meaningful HTTP status codes (200, 201, 400, 404, 409, 422, 500)

## Git & Version Control

- **Commit messages**: `<type>(<scope>): <imperative summary>` — e.g., `feat(orders): add barcode validation`
- Types: `feat`, `fix`, `refactor`, `test`, `chore`, `docs`
- Commit small, focused changesets — one logical change per commit
- Never commit secrets, credentials, or generated build artifacts
- Use feature branches; merge via PR with at least one review

## Code Reviews

Check for:
- Business logic correctness
- Security: input validation, auth checks, no secrets in code
- Performance: N+1 queries, unnecessary allocations in hot paths
- Error handling: all failure paths covered
- Test coverage for new logic

## Security (OWASP Top 10 Reminders)

- **Injection**: Parameterize all queries; never interpolate user input into SQL/commands
- **Broken Auth**: Validate tokens/sessions server-side; short expiry + refresh
- **Sensitive Data**: Encrypt at rest and in transit; never log PII
- **Insecure Deserialization**: Validate and restrict types when deserializing untrusted input
- **Logging & Monitoring**: Log security events (auth failures, permission denials) with timestamps

## Performance

- Avoid premature optimization; profile before optimizing
- Use `async/await` for I/O-bound work; use `Task.Run` only for CPU-bound work on background threads
- Cache expensive results with appropriate invalidation strategies
- Minimize allocations in hot paths: use `Span<T>`, `ArrayPool<T>`, `StringBuilder`

## Testing Pyramid

```
       /\
      /E2E\          Few — validate critical user journeys
     /------\
    /Integration\    Some — validate service boundaries
   /------------\
  /  Unit Tests  \   Many — fast, isolated, cover all edge cases
 /______________\
```

- Write tests before fixing bugs (reproduce the bug first)
- Tests should be deterministic: no random data, no clock dependency without abstraction
- Cover happy path + edge cases + error cases

## Documentation

- Code should be self-documenting; add comments only for **why**, not **what**
- Keep `README.md` up to date with: setup, build, run, deploy instructions
- Document public APIs with XML doc comments in C#
