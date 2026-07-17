using MediatR;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Queries.GetAccountById;

/// <summary>
/// Get a single account by its ID.
///
/// API: GET /api/admin/accounts/{id}
/// Auth: SUPER_ADMIN | IDENTITY_REVIEWER
/// </summary>
public sealed record GetAccountByIdQuery(Guid AccountId)
    : IRequest<Result<AccountDetailDto>>;

public sealed record AccountDetailDto(
    Guid          Id,
    string        MaskedNid,
    string        Username,
    string        Email,
    string        MaskedPhone,
    string        FullNameAr,
    string        FullNameEn,
    AccountStatus Status,
    int           IdentityLevel,
    DateTime      CreatedAt,
    DateTime?     LastLoginAt
);
