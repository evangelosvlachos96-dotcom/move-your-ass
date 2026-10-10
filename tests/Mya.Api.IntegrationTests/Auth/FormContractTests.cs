using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mya.Api.Extensions;
using Mya.Application.Common.Results;
using Mya.Application.Features.Auth.ChangePassword;
using Mya.Application.Features.Auth.ForgotPassword;
using Mya.Application.Features.Auth.Login;
using Shouldly;

namespace Mya.Api.IntegrationTests.Auth;

public sealed class FormContractTests
{
    [Theory]
    [InlineData("person@localhost")]
    [InlineData("person@domain.")]
    [InlineData("person@domain..com")]
    public void Authentication_requires_a_complete_email_domain(string email)
    {
        new LoginValidator().Validate(new LoginCommand(email, "ExamplePassword42")).IsValid.ShouldBeFalse();
        new ForgotPasswordValidator().Validate(new ForgotPasswordCommand(email)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Reused_password_has_a_machine_readable_validation_code()
    {
        var result = new ChangePasswordValidator().Validate(new ChangePasswordCommand("ExamplePassword42", "ExamplePassword42"));
        result.Errors.ShouldContain(e => e.ErrorCode == "PASSWORD_UNCHANGED");
    }

    [Fact]
    public void Empty_success_returns_a_receipt_and_trace_identifier()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "test-trace" };
        var response = Result.Success().ToActionResult(context).ShouldBeOfType<OkObjectResult>();
        response.StatusCode.ShouldBe(200);
        var body = JsonSerializer.SerializeToElement(response.Value);
        body.GetProperty("success").GetBoolean().ShouldBeTrue();
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
