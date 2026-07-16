using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.UpdateServiceDefinition;

public sealed record UpdateServiceDefinitionCommand(
    Guid ServiceId,
    string NameAr,
    string NameEn,
    string? DescriptionAr = null,
    string? DescriptionEn = null,
    int RequiredLoa = 1,
    bool RequiresAppointment = false,
    bool IsOnline = true,
    int? AverageDays = null,
    int DisplayOrder = 0,
    bool? Publish = null       // true=publish, false=depublish, null=no change
) : IRequest<UpdateServiceDefinitionResponse>;

public sealed record UpdateServiceDefinitionResponse(Guid ServiceId, string Message);
