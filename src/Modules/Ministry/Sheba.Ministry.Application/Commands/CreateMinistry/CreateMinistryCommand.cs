using MediatR;

namespace Sheba.Ministry.Application.Commands.CreateMinistry;

public sealed record CreateMinistryCommand(
    string Code,
    string NameAr,
    string NameEn,
    Guid? ParentMinistryId = null,
    string? DescriptionAr = null,
    string? DescriptionEn = null,
    string? ContactEmail = null,
    string? ContactPhone = null
) : IRequest<CreateMinistryResponse>;

public sealed record CreateMinistryResponse(Guid MinistryId, string Code, string NameEn);
