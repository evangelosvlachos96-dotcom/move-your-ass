using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Results;
using Mya.Application.Common.Security;
using Mya.Application.Common.Settings;
using Mya.Application.Features.Auth.ForgotPassword;
using Mya.Application.Features.Auth.Login;
using Mya.Application.Features.Auth.Refresh;
using Mya.Application.Features.Users.UpdateUser;
using Mya.Domain.Constants;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
using Mya.Infrastructure;
using Mya.Infrastructure.Identity;
using Mya.Infrastructure.Persistence;
using Shouldly;

namespace Mya.Api.IntegrationTests.Auth;

/// <summary>
/// Session, refresh-rotation and role-change behaviour (PART D). These exercise the real Identity
/// stores and the real EF model on SQLite; the conditional-update semantics they depend on are
/// plain SQL that SQLite and PostgreSQL agree on.
/// </summary>
public sealed class AuthRobustnessTests : IAsyncLifetime
{
    private const string Password = "Correct-Horse-9";

    /// <summary>Mirrors RefreshHandler.ReuseGrace, which is internal to the Application layer.</summary>
    private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly MutableClock _clock = new();
    private readonly Caller _caller = new();
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private AppDbContext _db = null!;
    private IUserService _users = null!;
    private LoginHandler _login = null!;
    private RefreshHandler _refresh = null!;
    private UpdateUserHandler _updateUser = null!;
    private ForgotPasswordHandler _forgot = null!;
    private ResetPasswordHandler _reset = null!;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityStores();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();

        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole(Roles.Admin));
        await roles.CreateAsync(new IdentityRole(Roles.Client));

        var manager = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        _users = new UserService(manager, _db, _clock);

        var tokens = new JwtTokenService(
            Options.Create(new JwtSettings
            {
                SigningKey = new string('k', 64),
                Issuer = "mya-api",
                Audience = "mya-spa",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 14,
            }),
            _clock);

        var mapper = new MapperConfiguration(c => c.CreateMap<UserAccount, Mya.Application.Features.Common.UserDto>(), NullLoggerFactory).CreateMapper();
        var invitations = new PasswordInvitations(_db, _clock, Options.Create(new PlatformSettings { InvitationHours = 24 }));

        _login = new LoginHandler(_users, tokens, _db, _clock, _caller, mapper);
        _refresh = new RefreshHandler(_users, tokens, _db, _clock, _caller);
        _updateUser = new UpdateUserHandler(_users, _db, _caller, _clock);
        _forgot = new ForgotPasswordHandler(_users, _db, invitations);
        _reset = new ResetPasswordHandler(_db, _users, _clock);
    }

    private static readonly Microsoft.Extensions.Logging.ILoggerFactory NullLoggerFactory =
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

    public async Task DisposeAsync()
    {
        _scope.Dispose();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<UserAccount> CreateUserAsync(string email, string role = Roles.Client)
    {
        var created = await _users.CreateAsync(
            new NewUserAccount(email, "Test", "User", role, UserStatus.Active, false), Password, Ct);
        created.IsSuccess.ShouldBeTrue();
        return created.Value;
    }

    private async Task<string> SignInAsync(string email)
    {
        var result = await _login.Handle(new LoginCommand(email, Password), Ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Value.RefreshToken;
    }

    // --- one session per user -------------------------------------------------------------

    [Fact]
    public async Task A_new_login_ends_the_other_device_session_with_a_message_the_ui_can_explain()
    {
        var user = await CreateUserAsync("one@example.test");
        var deviceA = await SignInAsync(user.Email);

        // Device B signs in. Device A's next refresh must say why it stopped working, not just fail.
        await SignInAsync(user.Email);

        var result = await _refresh.Handle(new RefreshCommand(deviceA), Ct);
        result.Error!.Code.ShouldBe("SESSION_SUPERSEDED");
    }

    // --- two tabs on one device -----------------------------------------------------------

    [Fact]
    public async Task Two_tabs_refreshing_the_same_cookie_both_succeed_and_the_session_survives()
    {
        var user = await CreateUserAsync("tabs@example.test");
        var cookie = await SignInAsync(user.Email);

        // Tab A rotates.
        var first = await _refresh.Handle(new RefreshCommand(cookie), Ct);
        first.IsSuccess.ShouldBeTrue();
        first.Value.RefreshToken.ShouldNotBeNull();

        // Tab B presents the same cookie a moment later, inside the grace window.
        _clock.Advance(TimeSpan.FromSeconds(5));
        var second = await _refresh.Handle(new RefreshCommand(cookie), Ct);

        second.IsSuccess.ShouldBeTrue();
        second.Value.AccessToken.ShouldNotBeNullOrWhiteSpace();
        // Nothing rotated, so the cookie the browser already holds is left alone.
        second.Value.RefreshToken.ShouldBeNull();

        // The session is still usable afterwards: the family was not revoked.
        var third = await _refresh.Handle(new RefreshCommand(first.Value.RefreshToken!), Ct);
        third.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Replaying_a_rotated_token_after_the_grace_window_revokes_the_whole_family()
    {
        var user = await CreateUserAsync("reuse@example.test");
        var cookie = await SignInAsync(user.Email);

        var rotated = await _refresh.Handle(new RefreshCommand(cookie), Ct);
        rotated.IsSuccess.ShouldBeTrue();

        _clock.Advance(ReuseGrace + TimeSpan.FromSeconds(1));
        var replayed = await _refresh.Handle(new RefreshCommand(cookie), Ct);
        replayed.Error!.Code.ShouldBe("INVALID_CREDENTIALS");

        // The legitimate token is dead too: that is the point of revoking the family.
        var afterwards = await _refresh.Handle(new RefreshCommand(rotated.Value.RefreshToken!), Ct);
        afterwards.IsFailure.ShouldBeTrue();
        (await _db.RefreshTokens.CountAsync(t => t.RevokedAtUtc == null)).ShouldBe(0);
    }

    [Fact]
    public async Task A_grace_replay_cannot_resurrect_a_session_that_a_newer_login_replaced()
    {
        var user = await CreateUserAsync("grace-superseded@example.test");
        var cookie = await SignInAsync(user.Email);
        await _refresh.Handle(new RefreshCommand(cookie), Ct);

        // Another device signs in during the grace window.
        await SignInAsync(user.Email);

        var replayed = await _refresh.Handle(new RefreshCommand(cookie), Ct);
        replayed.Error!.Code.ShouldBe("SESSION_SUPERSEDED");
    }

    [Fact]
    public async Task A_grace_replay_cannot_outlive_a_suspension()
    {
        var user = await CreateUserAsync("grace-suspended@example.test");
        var cookie = await SignInAsync(user.Email);
        await _refresh.Handle(new RefreshCommand(cookie), Ct);

        await _users.SetStatusAsync(user.Id, UserStatus.Suspended, "test", null, Ct);

        var replayed = await _refresh.Handle(new RefreshCommand(cookie), Ct);
        replayed.IsFailure.ShouldBeTrue();
    }

    // --- role changes ---------------------------------------------------------------------

    [Fact]
    public async Task An_admin_can_never_change_their_own_role()
    {
        var admin = await CreateUserAsync("self@example.test", Roles.Admin);
        await CreateUserAsync("other-admin@example.test", Roles.Admin);
        _caller.UserId = admin.Id;

        var result = await _updateUser.Handle(
            new UpdateUserCommand("Test", "User", Roles.Client) { UserId = admin.Id }, Ct);

        result.Error!.Code.ShouldBe("CANNOT_MODIFY_SELF");
        (await _users.FindByIdAsync(admin.Id, Ct))!.Role.ShouldBe(Roles.Admin);
    }

    [Fact]
    public async Task An_admin_may_still_edit_their_own_name()
    {
        var admin = await CreateUserAsync("rename@example.test", Roles.Admin);
        _caller.UserId = admin.Id;

        var result = await _updateUser.Handle(
            new UpdateUserCommand("Νέο", "Όνομα", Roles.Admin) { UserId = admin.Id }, Ct);

        result.IsSuccess.ShouldBeTrue();
        (await _users.FindByIdAsync(admin.Id, Ct))!.FirstName.ShouldBe("Νέο");
    }

    [Fact]
    public async Task The_last_admin_cannot_be_demoted_by_anyone()
    {
        var admin = await CreateUserAsync("only@example.test", Roles.Admin);
        var other = await CreateUserAsync("actor@example.test", Roles.Client);
        _caller.UserId = other.Id;

        var result = await _updateUser.Handle(
            new UpdateUserCommand("Test", "User", Roles.Client) { UserId = admin.Id }, Ct);

        result.Error!.Code.ShouldBe("CANNOT_DELETE_LAST_ADMIN");
    }

    [Fact]
    public async Task Demotion_ends_the_demoted_session_so_the_new_role_applies_on_the_next_request()
    {
        var admin = await CreateUserAsync("demoted@example.test", Roles.Admin);
        await CreateUserAsync("keeper@example.test", Roles.Admin);
        var actor = await CreateUserAsync("actor2@example.test", Roles.Admin);

        var cookie = await SignInAsync(admin.Email);
        (await _users.FindByIdAsync(admin.Id, Ct))!.ActiveSessionId.ShouldNotBeNull();

        _caller.UserId = actor.Id;
        (await _updateUser.Handle(new UpdateUserCommand("Test", "User", Roles.Client) { UserId = admin.Id }, Ct))
            .IsSuccess.ShouldBeTrue();

        var after = await _users.FindByIdAsync(admin.Id, Ct);
        after!.Role.ShouldBe(Roles.Client);
        // No session and no live refresh token: the next refresh fails and they sign in again.
        after.ActiveSessionId.ShouldBeNull();
        (await _db.RefreshTokens.CountAsync(t => t.UserId == admin.Id && t.RevokedAtUtc == null)).ShouldBe(0);
        (await _refresh.Handle(new RefreshCommand(cookie), Ct)).IsFailure.ShouldBeTrue();
    }

    // --- forgotten password ---------------------------------------------------------------

    [Fact]
    public async Task Forgot_password_answers_identically_whether_or_not_the_address_exists()
    {
        await CreateUserAsync("known@example.test");

        var known = await _forgot.Handle(new ForgotPasswordCommand("known@example.test"), Ct);
        var unknown = await _forgot.Handle(new ForgotPasswordCommand("nobody@example.test"), Ct);

        known.IsSuccess.ShouldBeTrue();
        unknown.IsSuccess.ShouldBeTrue();
        known.Error.ShouldBeNull();
        unknown.Error.ShouldBeNull();

        // Only the real address produced a credential and an email.
        (await _db.PasswordInvitations.CountAsync()).ShouldBe(1);
        (await _db.OutboxMessages.CountAsync(m => m.Type == OutboxMessageTypes.PasswordReset)).ShouldBe(1);
    }

    [Fact]
    public async Task A_suspended_account_gets_no_reset_link_and_still_the_same_answer()
    {
        var user = await CreateUserAsync("suspended@example.test");
        await _users.SetStatusAsync(user.Id, UserStatus.Suspended, null, null, Ct);

        (await _forgot.Handle(new ForgotPasswordCommand(user.Email), Ct)).IsSuccess.ShouldBeTrue();
        (await _db.PasswordInvitations.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_reset_sets_the_password_once_and_signs_every_device_out()
    {
        var user = await CreateUserAsync("reset@example.test");
        var cookie = await SignInAsync(user.Email);

        await _forgot.Handle(new ForgotPasswordCommand(user.Email), Ct);
        var token = TokenFromOutbox();

        var result = await _reset.Handle(new ResetPasswordCommand(token, "Brand-New-Pass-1"), Ct);
        result.IsSuccess.ShouldBeTrue();

        (await _users.CheckPasswordAsync(user.Id, "Brand-New-Pass-1", Ct)).ShouldBe(PasswordCheckOutcome.Success);
        (await _users.FindByIdAsync(user.Id, Ct))!.ActiveSessionId.ShouldBeNull();
        (await _refresh.Handle(new RefreshCommand(cookie), Ct)).IsFailure.ShouldBeTrue();

        // Single use.
        (await _reset.Handle(new ResetPasswordCommand(token, "Another-Pass-2"), Ct))
            .Error!.Code.ShouldBe("INVALID_INVITATION");
    }

    [Fact]
    public async Task An_expired_reset_link_is_refused()
    {
        var user = await CreateUserAsync("expired@example.test");
        await _forgot.Handle(new ForgotPasswordCommand(user.Email), Ct);
        var token = TokenFromOutbox();

        _clock.Advance(TimeSpan.FromHours(2));

        (await _reset.Handle(new ResetPasswordCommand(token, "Brand-New-Pass-1"), Ct))
            .Error!.Code.ShouldBe("INVALID_INVITATION");
    }

    [Fact]
    public async Task An_invitation_token_cannot_be_spent_as_a_password_reset()
    {
        var user = await CreateUserAsync("invited@example.test");
        var invitations = new PasswordInvitations(_db, _clock, Options.Create(new PlatformSettings { InvitationHours = 24 }));
        await invitations.QueueAsync(user, Ct);

        var token = TokenFromOutbox(OutboxMessageTypes.PasswordInvitation);

        (await _reset.Handle(new ResetPasswordCommand(token, "Brand-New-Pass-1"), Ct))
            .Error!.Code.ShouldBe("INVALID_INVITATION");
        // Refused without consuming it: the invitation still works for its own flow.
        (await _db.PasswordInvitations.CountAsync(x => x.ConsumedAtUtc == null)).ShouldBe(1);
    }

    [Fact]
    public async Task Asking_twice_invalidates_the_first_link()
    {
        var user = await CreateUserAsync("twice@example.test");

        await _forgot.Handle(new ForgotPasswordCommand(user.Email), Ct);
        var first = TokenFromOutbox();
        await _forgot.Handle(new ForgotPasswordCommand(user.Email), Ct);

        (await _reset.Handle(new ResetPasswordCommand(first, "Brand-New-Pass-1"), Ct))
            .Error!.Code.ShouldBe("INVALID_INVITATION");
    }

    private string TokenFromOutbox(string type = OutboxMessageTypes.PasswordReset)
    {
        var message = _db.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == type)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToList()
            .Last();

        return type == OutboxMessageTypes.PasswordReset
            ? EmailOutbox.Deserialize<PasswordResetPayload>(message.PayloadJson).Token
            : EmailOutbox.Deserialize<PasswordInvitationPayload>(message.PayloadJson).Token;
    }

    private sealed class Caller : ICurrentUser
    {
        public string? UserId { get; set; }

        public string? UserAgent => "tests";

        public string? IpAddress => "127.0.0.1";
    }

    /// <summary>Lets a test step over the refresh grace window without sleeping.</summary>
    private sealed class MutableClock : IClock
    {
        private DateTime _now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
