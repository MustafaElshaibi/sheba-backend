using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;

namespace Sheba.Identity.Application.Queries.GetAccountById;

public sealed class GetAccountByIdHandler(
    IIdentityRepository repository,
    ILogger<GetAccountByIdHandler> logger
) : IRequestHandler<GetAccountByIdQuery, AccountDetailDto?>
{
    public async Task<AccountDetailDto?> Handle(
        GetAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            logger.LogWarning("[GetAccountById] Account not found: {AccountId}", request.AccountId);
            return null;
        }

        return new AccountDetailDto(
            Id:           account.Id,
            MaskedNid:    MaskNid(account.NationalId),
            Username:     account.Username ?? "—",
            Email:        account.Email ?? "—",
            MaskedPhone:  MaskPhone(account.PhoneNumber),
            FullNameAr:   account.FullNameAr,
            FullNameEn:   account.FullNameEn,
            Status:       account.Status,
            IdentityLevel: account.IdentityLevel,
            CreatedAt:    account.CreatedAt,
            LastLoginAt:  account.LastLoginAt);
    }

    private static string MaskNid(string nid) =>
        nid.Length > 4
            ? string.Concat(new string('*', nid.Length - 4), nid[^4..])
            : "****";

    private static string MaskPhone(string phone) =>
        phone.Length < 7 ? "***" : phone[..^6] + "***" + phone[^3..];
}
