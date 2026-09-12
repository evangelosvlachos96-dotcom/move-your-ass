using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mya.Api.Authorization;
using Mya.Api.Extensions;
using Mya.Application.Features.Users.ApproveUser;
using Mya.Application.Features.Users.CreateUser;
using Mya.Application.Features.Users.DeclineUser;
using Mya.Application.Features.Users.DeleteUser;
using Mya.Application.Features.Users.ListUsers;
using Mya.Application.Features.Users.ReactivateUser;
using Mya.Application.Features.Users.ResetPassword;
using Mya.Application.Features.Users.SuspendUser;
using Mya.Application.Features.Users.UpdateUser;

namespace Mya.Api.Features.Users;

/// <summary>
/// The target user is always identified by the route. Request bodies carry only the fields the
/// route cannot: the command is assembled here from both.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController(
    ListUsersHandler list,
    CreateUserHandler create,
    UpdateUserHandler update,
    ApproveUserHandler approve,
    DeclineUserHandler decline,
    SuspendUserHandler suspend,
    ReactivateUserHandler reactivate,
    ResetPasswordHandler resetPassword,
    DeleteUserHandler delete) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListUsersQuery query, CancellationToken ct) =>
        (await list.Handle(query, ct)).ToActionResult(HttpContext, page => Ok(page));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserCommand command, CancellationToken ct) =>
        (await create.Handle(command, ct)).ToActionResult(HttpContext, created =>
            Created($"/api/admin/users/{created.Id}", created));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new UpdateUserCommand(request.FirstName, request.LastName, request.Role) { UserId = id };
        return (await update.Handle(command, ct)).ToActionResult(HttpContext, NoContent);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(string id, CancellationToken ct) =>
        (await approve.Handle(new ApproveUserCommand(id), ct)).ToActionResult(HttpContext, NoContent);

    [HttpPost("{id}/decline")]
    public async Task<IActionResult> Decline(string id, DeclineUserRequest? request, CancellationToken ct) =>
        (await decline.Handle(new DeclineUserCommand(request?.Reason) { UserId = id }, ct))
            .ToActionResult(HttpContext, NoContent);

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(string id, SuspendUserRequest? request, CancellationToken ct) =>
        (await suspend.Handle(new SuspendUserCommand(request?.Reason) { UserId = id }, ct))
            .ToActionResult(HttpContext, NoContent);

    [HttpPost("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(string id, CancellationToken ct) =>
        (await reactivate.Handle(new ReactivateUserCommand(id), ct)).ToActionResult(HttpContext, NoContent);

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken ct) =>
        (await resetPassword.Handle(new ResetPasswordCommand(id), ct)).ToActionResult(HttpContext, response => Ok(response));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct) =>
        (await delete.Handle(new DeleteUserCommand(id), ct)).ToActionResult(HttpContext, NoContent);
}
