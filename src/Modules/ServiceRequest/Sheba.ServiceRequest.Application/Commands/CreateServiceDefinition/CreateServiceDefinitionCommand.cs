using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.CreateServiceDefinition;

public sealed record CreateServiceDefinitionCommand(
    Guid CategoryId,
    Guid MinistryId,
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr = null,
    string? DescriptionEn = null,
    int RequiredLoa = 1,
    int? AverageDays = null,
    string? FormSchemaJson = null,
    string? UiSchemaJson = null
) : IRequest<CreateServiceDefinitionResponse>;

public sealed record CreateServiceDefinitionResponse(Guid ServiceId, string Code, string NameEn);
