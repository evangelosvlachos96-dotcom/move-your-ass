# 02 — Backend Architecture

## 1. Shape

Four projects, layered (horizontal), with feature folders inside each layer. Dependencies point
inward only:

```
     Mya.Api  ──────────────┐
        │                  │
        ▼                  ▼
  Mya.Application ────▶ Mya.Domain
        ▲                  ▲
        │                  │
  Mya.Infrastructure ───────┘
```

- **Domain** references nothing. Entities, enums, domain exceptions. No EF attributes.
- **Application** references Domain. Use cases, DTOs, validators, and *interfaces* for
  everything external (`IAppDbContext`, `IEmailSender`, `IVideoStorage`, `IClock`, `ICurrentUser`).
- **Infrastructure** references Application + Domain. Implements those interfaces. The database
  provider, the `DbContext`, configurations and migrations live here and nowhere else.
- **Api** references Application + Infrastructure. Controllers, middleware, DI composition.

The rule that keeps this honest, stated precisely:

- `Mya.Application` **may** reference the `Microsoft.EntityFrameworkCore` package — the
  abstractions: `DbSet<T>`, `FirstOrDefaultAsync`, `ToListAsync`, `ExecuteUpdateAsync`. That is
  what lets `IAppDbContext` expose `DbSet<T>` and handlers stay free of repository boilerplate.
- `Mya.Application` **must not** reference `Microsoft.EntityFrameworkCore.SqlServer` (or
  `Microsoft.Data.SqlClient`) — the provider is an Infrastructure concern.
- `Mya.Application` **must not** reference `Microsoft.AspNetCore.*`.
- `Mya.Domain` references nothing outside the BCL.

`Mya.ArchitectureTests/LayeringTests.cs` asserts exactly these four statements and fails the
build if any is broken.

## 2. Full tree

```
src/
  Mya.Api/
    Features/
      Auth/AuthController.cs
      Users/UsersController.cs                    [Admin]
      Health/HealthController.cs
    Middleware/
      ExceptionHandlingMiddleware.cs
      RequestContextMiddleware.cs                 correlation id, current user
    Filters/
      ValidationFilter.cs                         runs FluentValidation before the handler
      IdempotencyFilter.cs                        see docs/03 §5
    Extensions/
      ServiceCollectionExtensions.cs              AddApi / AddApplication / AddInfrastructure
      ResultExtensions.cs                         Result<T> → IActionResult
    appsettings.json
    Program.cs

  Mya.Application/
    Features/
      Auth/
        Register/{RegisterCommand.cs, RegisterHandler.cs, RegisterValidator.cs}
        Login/{LoginCommand.cs, LoginHandler.cs, LoginValidator.cs, LoginResult.cs}
        VerifyTwoFactor/...
        RefreshToken/...
        Logout/...
        ConfirmEmail/...
      Users/
        ListUsers/{ListUsersQuery.cs, ListUsersHandler.cs, UserListItemDto.cs}
        ApproveUser/{ApproveUserCommand.cs, ApproveUserHandler.cs}
        SuspendUser/...
    Abstractions/
      Persistence/IAppDbContext.cs
      Identity/{ITokenService.cs, IUserService.cs}
      Notifications/IEmailSender.cs
      Storage/IVideoStorage.cs
      System/{IClock.cs, ICurrentUser.cs}
    Common/
      Results/{Result.cs, Result{T}.cs, Error.cs, ErrorCodes.cs}
      Behaviour/                                  cross-cutting helpers, no pipeline framework

  Mya.Domain/
    Entities/{AppUser.cs, RefreshToken.cs, OutboxMessage.cs, IdempotencyRecord.cs}
    Enums/{UserStatus.cs, OutboxStatus.cs}
    Exceptions/DomainException.cs

  Mya.Infrastructure/
    Persistence/
      AppDbContext.cs
      Configurations/{AppUserConfiguration.cs, RefreshTokenConfiguration.cs, ...}
      Migrations/
      Seed/DatabaseSeeder.cs                      roles + first admin
    Identity/
      JwtTokenService.cs
      CurrentUserAccessor.cs
      SecurityStampValidator.cs
    Notifications/
      SmtpEmailSender.cs
      EmailTemplates/                             razor-lite string templates
      OutboxDispatcher.cs                         BackgroundService
    Storage/BlobVideoStorage.cs
    DependencyInjection.cs

tests/
  Mya.Application.UnitTests/Features/...
  Mya.Api.IntegrationTests/{ApiFactory.cs, AuthTests.cs, UserApprovalTests.cs}
  Mya.ArchitectureTests/LayeringTests.cs
```

Phase 1 builds only the `Auth` and `Users` features. Everything else is scaffolding for later.

## 3. Why no mediator

MediatR moved to a paid licence, and at ~20 use cases the abstraction costs more than it
returns. A handler is a class. Register it, inject it, call it.

```csharp
// Mya.Application/Features/Users/ApproveUser/ApproveUserHandler.cs
public sealed class ApproveUserHandler(
    IAppDbContext db,
    IUserService users,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result> Handle(ApproveUserCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);
        if (user is null)
            return Result.NotFound(ErrorCodes.UserNotFound);

        if (user.Status is not UserStatus.PendingApproval)
            return Result.Conflict(ErrorCodes.UserNotPending);

        user.Status       = UserStatus.Active;
        user.ApprovedAtUtc = clock.UtcNow;
        user.ApprovedBy   = currentUser.Id;

        await users.AddToRoleAsync(user, Roles.Client, ct);

        db.OutboxMessages.Add(OutboxMessage.AccountApproved(user.Id, user.Email));

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

Registration is one line per handler, or scan the assembly:

```csharp
services.Scan(s => s.FromAssemblyOf<ApproveUserHandler>()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Handler")))
    .AsSelf().WithScopedLifetime());
```

If you later need cross-cutting behaviour (logging, transactions), wrap with a decorator —
still cheaper than a pipeline framework.

## 4. Thin controllers

```csharp
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController(
    ListUsersHandler list,
    ApproveUserHandler approve,
    SuspendUserHandler suspend) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListUsersQuery q, CancellationToken ct)
        => Ok(await list.Handle(q, ct));

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(string id, CancellationToken ct)
        => (await approve.Handle(new ApproveUserCommand(id), ct)).ToActionResult(this);

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(string id, SuspendUserCommand cmd, CancellationToken ct)
        => (await suspend.Handle(cmd with { UserId = id }, ct)).ToActionResult(this);
}
```

A controller action is allowed exactly three things: bind, call one handler, map the result.
If you find yourself writing `if` in a controller, the logic belongs in the handler.

## 5. Result and error model

```csharp
public sealed record Error(string Code, string Message);

public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }
    public ResultStatus Status { get; }   // Success, NotFound, Conflict, Forbidden, Invalid
}
```

Every failure carries a **stable machine-readable code**. The Angular error interceptor
switches on the code, never on the message — messages change, and they get translated to Greek
in the UI.

```csharp
public static class ErrorCodes
{
    public const string AccountPending      = "ACCOUNT_PENDING";
    public const string AccountSuspended    = "ACCOUNT_SUSPENDED";
    public const string EmailNotConfirmed   = "EMAIL_NOT_CONFIRMED";
    public const string InvalidCredentials  = "INVALID_CREDENTIALS";
    public const string TwoFactorRequired   = "TWO_FACTOR_REQUIRED";
    public const string TwoFactorInvalid    = "TWO_FACTOR_INVALID";
    public const string SessionSuperseded   = "SESSION_SUPERSEDED";
    public const string SlotTaken           = "SLOT_TAKEN";
    public const string BookingLimitReached = "BOOKING_LIMIT_REACHED";
}
```

On the wire, RFC 7807:

```json
{
  "type": "https://pt.app/errors/account-pending",
  "title": "Account pending approval",
  "status": 403,
  "code": "ACCOUNT_PENDING",
  "traceId": "00-4bf92f...-01"
}
```

`ExceptionHandlingMiddleware` catches everything unhandled, logs with the correlation ID, and
returns a 500 with a generic title and the trace ID. It never leaks stack traces or exception
messages to the client in production.

## 6. Validation

FluentValidation validates **shape**. Handlers enforce **invariants that need the database**.

```csharp
public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(10)
            .Matches("[A-Z]").WithMessage("Must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Must contain a digit.");
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+[1-9]\d{7,14}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone must be in E.164 format.");
    }
}
```

"Is this email already taken?" is **not** a validator rule — it needs the DB and it races.
Let the unique index decide and handle the `DbUpdateException`.

Wire it as an action filter, not in each handler:

```csharp
services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
services.AddControllers(o => o.Filters.Add<ValidationFilter>());
```

## 7. Persistence conventions

- `AppDbContext : IdentityDbContext<AppUser>` and implements `IAppDbContext`.
- One `IEntityTypeConfiguration<T>` file per entity. **No data annotations on entities.**
- All datetimes are `DateTime` with `DateTimeKind.Utc`, column type `datetime2(3)`.
  Add a global value converter so nothing ever gets stored as Local.
- Money: none in this system. Keep it that way.
- Enums stored as `int`, with a `HasConversion<int>()` and a check constraint.
- `RowVersion` (`byte[]`, `IsRowVersion()`) on anything editable by two actors.

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder b)
{
    b.Properties<DateTime>()
     .HaveConversion<UtcDateTimeConverter>()
     .HaveColumnType("datetime2(3)");
    b.Properties<string>().HaveMaxLength(256);
}
```

## 8. Outbox

Any side effect that leaves the process (email, later: push) is written as an
`OutboxMessage` row **inside the same transaction** as the state change. A `BackgroundService`
polls every 15 seconds and dispatches with exponential backoff (1m, 5m, 30m, 2h, then dead-letter).

This means a dead SMTP server can never roll back a user approval, and a user approval can
never succeed without its email eventually being sent.

```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;       // "AccountApproved"
    public string PayloadJson { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
```

On App Service F1, remember the host can be recycled at any time. The dispatcher must be
idempotent and must claim rows with an `UPDATE ... OUTPUT` so two instances never send twice.

## 9. Testing

**Unit tests** (`Mya.Application.UnitTests`) — handlers and validators, with `IAppDbContext`
substituted or an in-memory provider where the query is trivial. Fast, no I/O.

**Integration tests** (`Mya.Api.IntegrationTests`) — `WebApplicationFactory` + **SQL Server in
Testcontainers**. Not SQLite: SQLite has no `rowversion`, no filtered indexes, and different
concurrency semantics, so it would pass where production fails.

**Architecture tests** — NetArchTest asserting the dependency rules in §1. Three tests, runs in
milliseconds, prevents the slow erosion that kills layered codebases.

Phase 1 minimum set:

| Test | Asserts |
|---|---|
| `RegisterValidator` | email/password/phone rules |
| `RegisterHandler` | creates user as `PendingApproval`, queues confirmation email |
| `LoginHandler` | pending → `ACCOUNT_PENDING`; suspended → `ACCOUNT_SUSPENDED`; valid → 2FA challenge |
| `VerifyTwoFactorHandler` | wrong code → `TWO_FACTOR_INVALID`; 5 attempts → ticket dies |
| `ApproveUserHandler` | pending → active + role assigned + outbox row; non-pending → conflict |
| `JwtTokenService` | claims present, expiry correct, `sid` included |
| `LayeringTests` | Application does not reference EF or ASP.NET |
| `AuthFlowTests` (integration) | register → approve → login → 2FA → call protected endpoint |
| `SessionTests` (integration) | second login invalidates the first session's refresh token |

Target ~20 tests for phase 1. Enough to refactor with confidence, not enough to become a
second project.
