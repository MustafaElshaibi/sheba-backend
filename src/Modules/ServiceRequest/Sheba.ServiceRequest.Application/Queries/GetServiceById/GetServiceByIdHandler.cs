using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Queries.GetServiceById;

public sealed class GetServiceByIdHandler(
    IServiceDefinitionRepository repository,
    ILogger<GetServiceByIdHandler> logger
) : IRequestHandler<GetServiceByIdQuery, ServiceDetailDto?>
{
    public async Task<ServiceDetailDto?> Handle(GetServiceByIdQuery request, CancellationToken ct)
    {
        var s = await repository.GetServiceByIdAsync(request.ServiceId, ct);
        if (s is null)
        {
            logger.LogWarning("[GetServiceById] Service {Id} not found", request.ServiceId);
            return null;
        }

        return new ServiceDetailDto(
            s.Id, s.Code, s.NameAr, s.NameEn,
            s.DescriptionAr, s.DescriptionEn,
            s.CategoryId, s.MinistryId,
            s.RequiredLoa, s.RequiresAppointment, s.IsOnline, s.IsActive,
            s.AverageDays, s.DisplayOrder,
            s.CreatedAt, s.UpdatedAt,
            FormSchema: s.FormSchema is not null
                ? new FormSchemaDto(s.FormSchema.Id, s.FormSchema.SchemaVersion,
                    s.FormSchema.FormSchemaJson, s.FormSchema.UiSchemaJson)
                : null,
            Fees: s.Fees.Select(f => new FeeDetailDto(
                f.Id, f.FeeType, f.NameAr, f.NameEn,
                f.Amount, f.Currency, f.IsMandatory
            )).ToList(),
            RequiredDocuments: s.RequiredDocuments.Select(d => new RequiredDocumentDto(
                d.Id, d.DocumentType, d.NameAr, d.NameEn,
                d.IsMandatory, d.MaxSizeMb
            )).ToList(),
            WorkflowSteps: s.WorkflowSteps.OrderBy(w => w.StepOrder).Select(w => new WorkflowStepDto(
                w.Id, w.StepOrder, w.NameAr, w.NameEn,
                w.StepType, w.Actor, w.IsAutomated, w.TimeoutHours
            )).ToList());
    }
}
