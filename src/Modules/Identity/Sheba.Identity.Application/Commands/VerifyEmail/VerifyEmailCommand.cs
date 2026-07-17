using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(
    Guid AccountId,
    string Token
) : IRequest<Result<VerifyEmailResponse>>;

/// <summary>Response for a successful VerifyEmailCommand — failures are carried by Result&lt;T&gt;.Error.</summary>
public sealed record VerifyEmailResponse(string Message);