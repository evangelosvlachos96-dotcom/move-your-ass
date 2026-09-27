using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Security;
using Mya.Application.Common.Settings;
using Mya.Application.Features.Auth.AcceptInvitation;
using Mya.Application.Features.Auth.Register;
using Mya.Application.Features.Common;
using Mya.Application.Features.Users.ApproveUser;
using Mya.Application.Features.Users.DeclineUser;
using Mya.Application.Features.Users.DeleteUser;
using Mya.Domain.Constants;
using Mya.Domain.Enums;
using Shouldly;

namespace Mya.Api.IntegrationTests.Concurrency;

/// <summary>
/// Double-submit and two-admin races, against a real PostgreSQL (PART D). Each actor runs in its
/// own scope, so these are two DbContexts and two transactions under READ COMMITTED — the same
/// shape as two web requests hitting Neon at the same moment.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AdminRaceTests(PostgresFixture fixture)
{
    private const string Password = "Correct-Horse-9";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly FixedClock _clock = new();

    private static readonly IMapper Mapper = new MapperConfiguration(
        c => c.CreateMap<UserAccount, UserDto>(), NullLoggerFactory.Instance).CreateMapper();

    private Task<IsolatedDatabase> NewDatabaseAsync() => IsolatedDatabase.CreateAsync(fixture, _clock);

    [Fact]
    public async Task Two_registrations_of_the_same_email_at_once_create_exactly_one_account()
    {
        await using var database = await NewDatabaseAsync();

        async Task<bool> RegisterAsync()
        {
            using var scope = database.NewScope();
            var users = database.Users(scope).Build();
            var db = database.Context(scope);
            var handler = new RegisterHandler(users, db, _clock);
            var result = await handler.Handle(
                new RegisterCommand("race@example.test", "Μαρία", "Παπά", Password), Ct);
            return result.IsSuccess;
        }

        var outcomes = await Task.WhenAll(RegisterAsync(), RegisterAsync());

        using var check = database.NewScope();
        var context = database.Context(check);
        (await context.Users.CountAsync(u => u.Email == "race@example.test", Ct)).ShouldBe(1);
        // Exactly one wins; the loser fails cleanly rather than throwing a raw database error.
        outcomes.Count(x => x).ShouldBe(1);
    }

    [Fact]
    public async Task One_invitation_link_submitted_twice_at_once_activates_the_account_once()
    {
        await using var database = await NewDatabaseAsync();
        string token;
        string userId;

        using (var setup = database.NewScope())
        {
            var users = database.Users(setup).Build();
            var db = database.Context(setup);
            var created = await users.CreateAsync(
                new NewUserAccount("invited@example.test", "Α", "Β", Roles.Client, UserStatus.Invited, true), Password, Ct);
            userId = created.Value.Id;
            await new PasswordInvitations(db, _clock, Options.Create(new PlatformSettings { InvitationHours = 24 }))
                .QueueAsync(created.Value, Ct);
            var message = await db.OutboxMessages.AsNoTracking()
                .SingleAsync(m => m.Type == OutboxMessageTypes.PasswordInvitation, Ct);
            token = EmailOutbox.Deserialize<PasswordInvitationPayload>(message.PayloadJson).Token;
        }

        async Task<bool> AcceptAsync()
        {
            using var scope = database.NewScope();
            var handler = new AcceptInvitationHandler(
                database.Context(scope), database.Users(scope).Build(), _clock);
            return (await handler.Handle(new AcceptInvitationCommand(token, "Brand-New-Pass-1"), Ct)).IsSuccess;
        }

        var outcomes = await Task.WhenAll(AcceptAsync(), AcceptAsync());

        outcomes.Count(x => x).ShouldBe(1);
        using var check = database.NewScope();
        (await database.Users(check).Build().FindByIdAsync(userId, Ct))!.Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task Two_admins_approving_the_same_registration_produce_one_outcome_and_one_email()
    {
        await using var database = await NewDatabaseAsync();
        var userId = await PendingUserAsync(database, "approve@example.test");

        async Task<bool> ApproveAsync()
        {
            using var scope = database.NewScope();
            var db = database.Context(scope);
            var handler = new ApproveUserHandler(
                database.Users(scope).Build(), db, _clock, new Caller(), Mapper);
            return (await handler.Handle(new ApproveUserCommand(userId), Ct)).IsSuccess;
        }

        var outcomes = await Task.WhenAll(ApproveAsync(), ApproveAsync());

        outcomes.Count(x => x).ShouldBe(1);
        using var check = database.NewScope();
        var context = database.Context(check);
        // One approval email, not two: the client must not be told twice.
        (await context.OutboxMessages.CountAsync(m => m.Type == OutboxMessageTypes.AccountApproved, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Approving_and_declining_the_same_registration_at_once_settles_on_one_of_them()
    {
        await using var database = await NewDatabaseAsync();
        var userId = await PendingUserAsync(database, "both@example.test");

        async Task<bool> ApproveAsync()
        {
            using var scope = database.NewScope();
            var handler = new ApproveUserHandler(
                database.Users(scope).Build(), database.Context(scope), _clock, new Caller(), Mapper);
            return (await handler.Handle(new ApproveUserCommand(userId), Ct)).IsSuccess;
        }

        async Task<bool> DeclineAsync()
        {
            using var scope = database.NewScope();
            var handler = new DeclineUserHandler(
                database.Users(scope).Build(), database.Context(scope), _clock, new Caller(), Mapper);
            return (await handler.Handle(new DeclineUserCommand("duplicate") { UserId = userId }, Ct)).IsSuccess;
        }

        var outcomes = await Task.WhenAll(ApproveAsync(), DeclineAsync());

        outcomes.Count(x => x).ShouldBe(1);
        using var check = database.NewScope();
        var context = database.Context(check);
        var status = (await database.Users(check).Build().FindByIdAsync(userId, Ct))!.Status;
        status.ShouldBeOneOf(UserStatus.Active, UserStatus.Declined);

        // Whichever won, the client hears one story.
        var approved = await context.OutboxMessages.CountAsync(m => m.Type == OutboxMessageTypes.AccountApproved, Ct);
        var declined = await context.OutboxMessages.CountAsync(m => m.Type == OutboxMessageTypes.AccountDeclined, Ct);
        (approved + declined).ShouldBe(1);
    }

    [Fact]
    public async Task Approving_a_registration_while_it_is_being_deleted_does_not_resurrect_it()
    {
        await using var database = await NewDatabaseAsync();
        var userId = await PendingUserAsync(database, "gone@example.test");

        async Task<bool> ApproveAsync()
        {
            using var scope = database.NewScope();
            var handler = new ApproveUserHandler(
                database.Users(scope).Build(), database.Context(scope), _clock, new Caller(), Mapper);
            return (await handler.Handle(new ApproveUserCommand(userId), Ct)).IsSuccess;
        }

        async Task<bool> DeleteAsync()
        {
            using var scope = database.NewScope();
            var handler = new DeleteUserHandler(
                database.Users(scope).Build(), new Caller { UserId = "someone-else" }, database.Context(scope));
            return (await handler.Handle(new DeleteUserCommand(userId), Ct)).IsSuccess;
        }

        await Task.WhenAll(ApproveAsync(), DeleteAsync());

        using var check = database.NewScope();
        var user = await database.Users(check).Build().FindByIdAsync(userId, Ct);
        // Either it survived as Active or it is gone. It must never be gone *and* still have an
        // approval email queued to an account that no longer exists.
        if (user is null)
        {
            var context = database.Context(check);
            (await context.OutboxMessages.CountAsync(m => m.Type == OutboxMessageTypes.AccountApproved, Ct)).ShouldBe(0);
        }
        else
        {
            user.Status.ShouldBe(UserStatus.Active);
        }
    }

    private async Task<string> PendingUserAsync(IsolatedDatabase database, string email)
    {
        using var scope = database.NewScope();
        var created = await database.Users(scope).Build().CreateAsync(
            new NewUserAccount(email, "Α", "Β", Roles.Client, UserStatus.PendingApproval, false), Password, Ct);
        created.IsSuccess.ShouldBeTrue();
        return created.Value.Id;
    }

    private sealed class Caller : ICurrentUser
    {
        public string? UserId { get; set; } = "admin-actor";

        public string? UserAgent => "tests";

        public string? IpAddress => "127.0.0.1";
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    }
}
