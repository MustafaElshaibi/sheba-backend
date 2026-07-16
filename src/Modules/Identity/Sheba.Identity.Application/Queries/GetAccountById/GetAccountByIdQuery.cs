using MediatR;
using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Application.Queries.GetAccountById;

/// <summary>
/// Get a single account by its ID.
/// Used by admin portal and OpenIddict userinfo endpoint.
///
/// API: GET /api/admin/accounts/{id}
///      GET /connect/userinfo (via OpenIddict)
/// Auth: SUPER_ADMIN | IDENTITY_REVIEWER | self (for userinfo)
/// </summary>
public sealed record GetAccountByIdQuery(Guid AccountId)
    : IRequest<AccountDetailDto?>;

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
