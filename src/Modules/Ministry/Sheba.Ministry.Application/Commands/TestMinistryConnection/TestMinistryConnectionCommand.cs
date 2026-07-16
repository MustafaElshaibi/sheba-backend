using MediatR;

namespace Sheba.Ministry.Application.Commands.TestMinistryConnection;

public sealed record TestMinistryConnectionCommand(Guid AuthConfigId)
    : IRequest<TestMinistryConnectionResponse>;

public sealed record TestMinistryConnectionResponse(
    bool Success,
    int? StatusCode,
    long LatencyMs,
    string? Error);
