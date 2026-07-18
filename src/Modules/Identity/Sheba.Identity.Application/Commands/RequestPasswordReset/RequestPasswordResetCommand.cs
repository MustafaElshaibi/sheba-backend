using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RequestPasswordReset;

/// <summary>
/// Step 1 of password reset: citizen supplies their login identifier (username or national ID).
/// The response is always the same generic message regardless of whether the identifier matches
/// an account (BR-ON-3 anti-enumeration) — only an approved account with the identifier actually
/// gets a code sent, and only to its registry-registered phone number (BR-ON-5), never one the
/// caller supplies.
///
/// API: POST /api/identity/password-reset/request
/// </summary>
public sealed record RequestPasswordResetCommand(string UsernameOrNid) : IRequest<Result<RequestPasswordResetResponse>>;

public sealed record RequestPasswordResetResponse(string Message);
