using MediatR;

namespace Sheba.Ministry.Application.Commands.UpdateMinistry;

public sealed record UpdateMinistryCommand(
    Guid MinistryId,
    string NameAr,
    string NameEn,
    string? DescriptionAr = null,
    string? DescriptionEn = null,
    string? LogoUrl = null,
    string? WebsiteUrl = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? AddressAr = null,
    string? AddressEn = null,
    int DisplayOrder = 0,
    bool IsActive = true
) : IRequest<UpdateMinistryResponse>;

public sealed record UpdateMinistryResponse(Guid MinistryId, string Message);
