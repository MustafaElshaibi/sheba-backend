using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Queries.GetAccountById;

public sealed class GetAccountByIdHandler(
    IIdentityRepository repository,
    ILogger<GetAccountByIdHandler> logger
) : IRequestHandler<GetAccountByIdQuery, Result<AccountDetailDto>>
{
    public async Task<Result<AccountDetailDto>> Handle(
        GetAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            logger.LogWarning("[GetAccountById] Account not found: {AccountId}", request.AccountId);
            return Result.Failure<AccountDetailDto>(Error.NotFound("resource", "Account not found."));
        }

        return Result.Success(new AccountDetailDto(
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
            LastLoginAt:  account.LastLoginAt));
    }

    private static string MaskNid(string nid) =>
        nid.Length > 4
            ? string.Concat(new string('*', nid.Length - 4), nid[^4..])
            : "****";

    private static string MaskPhone(string phone) =>
        phone.Length < 7 ? "***" : phone[..^6] + "***" + phone[^3..];
}
