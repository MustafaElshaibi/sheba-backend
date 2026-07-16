using MediatR;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.Queries.GetServiceById;

public sealed record GetServiceByIdQuery(Guid ServiceId) : IRequest<ServiceDetailDto?>;

public sealed record ServiceDetailDto(
    Guid Id, string Code, string NameAr, string NameEn,
    string? DescriptionAr, string? DescriptionEn,
    Guid CategoryId, Guid MinistryId,
    int RequiredLoa, bool RequiresAppointment, bool IsOnline, bool IsActive,
    int? AverageDays, int DisplayOrder,
    DateTime CreatedAt, DateTime UpdatedAt,
    FormSchemaDto? FormSchema,
    List<FeeDetailDto> Fees,
    List<RequiredDocumentDto> RequiredDocuments,
    List<WorkflowStepDto> WorkflowSteps);

public sealed record FormSchemaDto(
    Guid Id, string SchemaVersion, string FormSchemaJson, string? UiSchemaJson);

public sealed record FeeDetailDto(
    Guid Id, string FeeType, string NameAr, string NameEn,
    decimal Amount, string Currency, bool IsMandatory);

public sealed record RequiredDocumentDto(
    Guid Id, string DocumentType, string NameAr, string NameEn,
    bool IsMandatory, int MaxSizeMb);

public sealed record WorkflowStepDto(
    Guid Id, int StepOrder, string NameAr, string NameEn,
    WorkflowStepType StepType, WorkflowActor Actor,
    bool IsAutomated, int? TimeoutHours);
