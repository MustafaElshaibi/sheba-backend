using MediatR;

namespace Sheba.Identity.Application.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(
    Guid AccountId,
    string Token
) : IRequest<VerifyEmailResponse>;

public sealed record VerifyEmailResponse(
    bool Succeeded,
    string Message
);