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
    bool? Publish = null,      // true=publish, false=depublish, null=no change
    // T-AUTH-1: null for SuperAdmin (unrestricted); a MinistryManager's own ministry_id
    // otherwise — the handler rejects updating a service owned by a different ministry.
    Guid? ActorMinistryId = null
) : IRequest<UpdateServiceDefinitionResponse>;

public sealed record UpdateServiceDefinitionResponse(Guid ServiceId, string Message);
