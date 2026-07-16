using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Application.Queries.GetIdentityRequests;

public sealed class GetIdentityRequestsHandler(
    IIdentityRepository repository,
    ILogger<GetIdentityRequestsHandler> logger
) : IRequestHandler<GetIdentityRequestsQuery, GetIdentityRequestsResponse>
{
    public async Task<GetIdentityRequestsResponse> Handle(
        GetIdentityRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var allRequests = await repository.GetPendingRequestsAsync(
            request.PageSize * request.Page, // over-fetch for server-side filtering
            1,
            cancellationToken);

        // Apply status filter in-memory (small dataset for graduation)
        var filtered = request.Status.HasValue
            ? allRequests.Where(r => r.Status == request.Status.Value).ToList()
            : allRequests;

        var total      = filtered.Count;
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        var paged      = filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var items = paged.Select(r =>
        {
            // Snapshot is stored as JSONB; read the fields from the JsonElement.
            var (fullNameAr, fullNameEn, nid) = ReadSnapshot(r.CitizenSnapshot);

            return new IdentityRequestSummary(
                RequestId:   r.Id,
                AccountId:   r.AccountId,
                FullNameAr:  fullNameAr,
                FullNameEn:  fullNameEn,
                MaskedNid:   MaskNid(nid),
                Status:      r.Status,
                RequestType: r.RequestType,
                SubmittedAt: r.SubmittedAt,
                ReviewedAt:  r.ReviewedAt);
        }).ToList();

        logger.LogDebug(
            "[GetIdentityRequests] Returning {Count}/{Total} requests (Page={Page})",
            items.Count, total, request.Page);

        return new GetIdentityRequestsResponse(items, total, request.Page, request.PageSize, totalPages);
    }

    /// <summary>
    /// Reads the citizen snapshot (stored as JSONB) into display fields.
    /// The snapshot is written with PascalCase keys by RegisterCitizenHandler.
    /// </summary>
    private static (string FullNameAr, string FullNameEn, string Nid) ReadSnapshot(object snapshot)
    {
        if (snapshot is not System.Text.Json.JsonElement el ||
            el.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return ("—", "—", "****");
        }

        return (
            GetString(el, "FullNameAr") ?? "—",
            GetString(el, "FullNameEn") ?? "—",
            GetString(el, "NationalId") ?? "****");
    }

    private static string? GetString(System.Text.Json.JsonElement el, string propertyName) =>
        el.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static string MaskNid(string nid) =>
        nid.Length > 4
            ? string.Concat(new string('*', nid.Length - 4), nid[^4..])
            : "****";
}
