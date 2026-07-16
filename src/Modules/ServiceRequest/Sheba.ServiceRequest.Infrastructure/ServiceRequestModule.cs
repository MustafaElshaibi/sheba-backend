using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Application.Commands.CreateServiceCategory;
using Sheba.ServiceRequest.Application.Commands.CreateServiceDefinition;
using Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;
using Sheba.ServiceRequest.Application.Commands.HandleWebhookCallback;
using Sheba.ServiceRequest.Application.Commands.MarkPaymentComplete;
using Sheba.ServiceRequest.Application.Commands.SetServiceFee;
using Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest;
using Sheba.ServiceRequest.Application.Commands.UpdateServiceDefinition;
using Sheba.ServiceRequest.Application.Queries.GetAllRequests;
using Sheba.ServiceRequest.Application.Queries.GetMyRequests;
using Sheba.ServiceRequest.Application.Queries.GetRequestById;
using Sheba.ServiceRequest.Application.Queries.GetServiceById;
using Sheba.ServiceRequest.Application.Queries.GetServiceCatalog;
using Sheba.ServiceRequest.Application.StepHandlers;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.ServiceRequest.Infrastructure.Persistence;
using Sheba.ServiceRequest.Infrastructure.Persistence.Repositories;

namespace Sheba.ServiceRequest.Infrastructure;

public static class ServiceRequestModule
{
    public static IServiceCollection AddServiceRequestModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ServiceRequestDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "service_req");
                    npgsql.MigrationsAssembly(typeof(ServiceRequestModule).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ServiceRequestDbContext>());
        services.AddScoped<IServiceDefinitionRepository, ServiceDefinitionRepository>();
        services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();

        // Workflow step handlers
        services.AddScoped<IWorkflowStepHandler, PaymentStepHandler>();
        services.AddScoped<IWorkflowStepHandler, MinistryCallStepHandler>();
        services.AddScoped<IWorkflowStepHandler, WebhookWaitStepHandler>();
        services.AddScoped<IWorkflowStepHandler, AdminReviewStepHandler>();
        services.AddScoped<IWorkflowStepHandler, AutoCompleteStepHandler>();

        return services;
    }

    public static WebApplication MapServiceRequestEndpoints(this WebApplication app)
    {
        // ── Service Catalog (public) ─────────────────────────────────────────
        var catalog = app.MapGroup("/api/services").WithTags("Service Catalog");

        catalog.MapGet("/", async (IMediator mediator, bool? includeInactive, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetServiceCatalogQuery(includeInactive ?? false), ct);
            return Results.Ok(result);
        })
        .WithName("GetServiceCatalog")
        .WithSummary("Get the full service catalog grouped by category.");

        catalog.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetServiceByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetServiceById")
        .WithSummary("Get full service detail with form schema, fees, docs, workflow steps.");

        // ── Admin Service Management ─────────────────────────────────────────
        var admin = app.MapGroup("/api/admin/services").WithTags("Admin — Service Catalog");

        admin.MapPost("/categories", async (
            CreateServiceCategoryCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/services/categories/{result.CategoryId}", result);
        })
        .WithName("CreateServiceCategory")
        .WithSummary("Create a new service category.");

        admin.MapPost("/", async (
            CreateServiceDefinitionCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/services/{result.ServiceId}", result);
        })
        .WithName("CreateServiceDefinition")
        .WithSummary("Create a new service definition (starts unpublished).");

        admin.MapPut("/{id:guid}", async (
            Guid id, UpdateServiceDefinitionCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = command with { ServiceId = id };
            var result = await mediator.Send(cmd, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateServiceDefinition")
        .WithSummary("Update service details. Set Publish=true/false to publish/depublish.");

        admin.MapPost("/{id:guid}/fees", async (
            Guid id, SetServiceFeeCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = command with { ServiceId = id };
            var result = await mediator.Send(cmd, ct);
            return Results.Created($"/api/services/{id}/fees/{result.FeeId}", result);
        })
        .WithName("SetServiceFee")
        .WithSummary("Add a fee to a service.");

        // ── Service Requests (citizen) ───────────────────────────────────────
        var requests = app.MapGroup("/api/requests").WithTags("Service Requests");

        requests.MapPost("/", async (
            SubmitServiceRequestCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            // Auto-execute the first workflow step
            await mediator.Send(new ExecuteNextStepCommand(result.RequestId), ct);
            return Results.Created($"/api/requests/{result.RequestId}", result);
        })
        .WithName("SubmitServiceRequest")
        .WithSummary("Submit a new service request and start workflow.");

        requests.MapGet("/mine/{citizenId:guid}", async (
            Guid citizenId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyRequestsQuery(citizenId), ct);
            return Results.Ok(result);
        })
        .WithName("GetMyRequests")
        .WithSummary("Get all service requests for a citizen.");

        requests.MapGet("/{id:guid}", async (
            Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRequestByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetRequestById")
        .WithSummary("Get full request detail with step executions.");

        requests.MapPost("/{id:guid}/execute-next", async (
            Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ExecuteNextStepCommand(id), ct);
            return Results.Ok(result);
        })
        .WithName("ExecuteNextStep")
        .WithSummary("Execute the next workflow step for a request.");

        // ── Payment ──────────────────────────────────────────────────────────
        requests.MapPost("/payments/{paymentOrderId:guid}/complete", async (
            Guid paymentOrderId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new MarkPaymentCompleteCommand(paymentOrderId), ct);
            return Results.Ok(result);
        })
        .WithName("MarkPaymentComplete")
        .WithSummary("Mark a payment as completed (mock gateway callback).");

        // ── Webhook Receiver ─────────────────────────────────────────────────
        app.MapPost("/api/webhooks/ministry/{ministryId:guid}", async (
            Guid ministryId, HttpRequest httpRequest, IMediator mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(httpRequest.Body);
            var payload = await reader.ReadToEndAsync(ct);
            var eventType = httpRequest.Headers["X-Webhook-Event"].FirstOrDefault() ?? "unknown";
            var signature = httpRequest.Headers["X-Webhook-Signature"].FirstOrDefault();

            var result = await mediator.Send(new HandleWebhookCallbackCommand(
                ministryId, eventType, payload, signature), ct);

            return result.Accepted ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithTags("Webhooks")
        .WithName("MinistryWebhookReceiver")
        .WithSummary("Receive webhook callbacks from ministry systems.");

        // ── Admin Requests ───────────────────────────────────────────────────
        var adminReq = app.MapGroup("/api/admin/requests").WithTags("Admin — Service Requests");

        adminReq.MapGet("/", async (
            IMediator mediator,
            RequestLifecycleStatus? status, Guid? serviceId, Guid? ministryId,
            DateTime? fromDate, DateTime? toDate,
            int? page, int? pageSize, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllRequestsQuery(
                status, serviceId, ministryId, fromDate, toDate,
                page ?? 1, pageSize ?? 20), ct);
            return Results.Ok(result);
        })
        .WithName("GetAllRequests")
        .WithSummary("Admin query: list all requests with filtering.");

        return app;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SEED DATA — 5 demo services for graduation
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Seeds 5 demo government services with JSON Schema forms.
    /// Called from Program.cs on startup. Idempotent — skips if data exists.
    /// </summary>
    public static async Task SeedServiceCatalogAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceRequestDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ServiceRequestDbContext>>();

        // Skip if already seeded
        if (await db.Categories.AnyAsync())
        {
            logger.LogDebug("[ServiceRequestModule] Catalog already seeded — skipping.");
            return;
        }

        logger.LogInformation("[ServiceRequestModule] Seeding demo service catalog...");

        // A deterministic ministry ID for seeded services (matches a ministry that would be seeded)
        var moiId = Guid.Parse("00000000-0000-0000-0001-000000000001");   // Ministry of Interior
        var mojId = Guid.Parse("00000000-0000-0000-0001-000000000002");   // Ministry of Justice
        var mohId = Guid.Parse("00000000-0000-0000-0001-000000000003");   // Ministry of Health
        var motId = Guid.Parse("00000000-0000-0000-0001-000000000004");   // Ministry of Transport
        var mociId = Guid.Parse("00000000-0000-0000-0001-000000000005");  // Ministry of Commerce

        // ── Categories ───────────────────────────────────────────────────────
        var catIdentity = ServiceCategory.Create("وثائق هوية", "Identity Documents", displayOrder: 1);
        var catCivil = ServiceCategory.Create("سجل مدني", "Civil Registry", displayOrder: 2);
        var catTransport = ServiceCategory.Create("نقل ومرور", "Transport & Traffic", displayOrder: 3);
        var catCommerce = ServiceCategory.Create("تجارة وأعمال", "Commerce & Business", displayOrder: 4);

        db.Categories.AddRange(catIdentity, catCivil, catTransport, catCommerce);

        // ── Service 1: Passport Application ──────────────────────────────────
        var passport = ServiceDefinition.Create(catIdentity.Id, moiId, "PASSPORT_NEW",
            "طلب جواز سفر جديد", "New Passport Application", requiredLoa: 2,
            descriptionAr: "تقديم طلب للحصول على جواز سفر يمني جديد",
            descriptionEn: "Apply for a new Yemeni passport", averageDays: 14);
        db.Services.Add(passport);
        db.FormSchemas.Add(ServiceFormSchema.Create(passport.Id, """
        {
            "type": "object",
            "required": ["fullNameEn", "fullNameAr", "dateOfBirth", "gender", "placeOfBirth", "address", "travelReason"],
            "properties": {
                "fullNameEn": { "type": "string", "title": "Full Name (English)", "minLength": 3, "maxLength": 200 },
                "fullNameAr": { "type": "string", "title": "الاسم الكامل (عربي)", "minLength": 3, "maxLength": 200 },
                "dateOfBirth": { "type": "string", "format": "date", "title": "Date of Birth" },
                "gender": { "type": "string", "enum": ["Male", "Female"], "title": "Gender" },
                "placeOfBirth": { "type": "string", "title": "Place of Birth" },
                "address": { "type": "string", "title": "Current Address", "maxLength": 500 },
                "travelReason": { "type": "string", "title": "Reason for Travel", "maxLength": 300 },
                "emergencyContact": { "type": "string", "title": "Emergency Contact Phone" }
            }
        }
        """));
        db.Fees.Add(ServiceFee.Create(passport.Id, "BASE", "رسوم إصدار", "Issuance Fee", 25000m));
        db.Fees.Add(ServiceFee.Create(passport.Id, "EXPEDITE", "رسوم استعجال", "Express Processing", 15000m, isMandatory: false));

        // ── Service 2: Civil ID Renewal ──────────────────────────────────────
        var civilId = ServiceDefinition.Create(catIdentity.Id, moiId, "CIVIL_ID_RENEW",
            "تجديد البطاقة الشخصية", "Civil ID Card Renewal", requiredLoa: 1,
            descriptionAr: "تجديد بطاقة الهوية الشخصية المنتهية",
            descriptionEn: "Renew an expired national ID card", averageDays: 7);
        db.Services.Add(civilId);
        db.FormSchemas.Add(ServiceFormSchema.Create(civilId.Id, """
        {
            "type": "object",
            "required": ["currentIdNumber", "renewalReason"],
            "properties": {
                "currentIdNumber": { "type": "string", "title": "Current ID Number", "pattern": "^\\d{10}$" },
                "renewalReason": { "type": "string", "enum": ["Expired", "Damaged", "Lost", "Data Change"], "title": "Reason for Renewal" },
                "newAddress": { "type": "string", "title": "New Address (if changed)", "maxLength": 500 },
                "newPhone": { "type": "string", "title": "New Phone Number (if changed)" }
            }
        }
        """));
        db.Fees.Add(ServiceFee.Create(civilId.Id, "BASE", "رسوم تجديد", "Renewal Fee", 5000m));

        // ── Service 3: Birth Certificate ─────────────────────────────────────
        var birthCert = ServiceDefinition.Create(catCivil.Id, mojId, "BIRTH_CERT",
            "شهادة ميلاد", "Birth Certificate Issuance", requiredLoa: 1,
            descriptionAr: "طلب إصدار شهادة ميلاد",
            descriptionEn: "Request issuance of a birth certificate", averageDays: 5);
        db.Services.Add(birthCert);
        db.FormSchemas.Add(ServiceFormSchema.Create(birthCert.Id, """
        {
            "type": "object",
            "required": ["childFullNameAr", "childFullNameEn", "dateOfBirth", "placeOfBirth", "gender", "fatherNationalId", "motherName"],
            "properties": {
                "childFullNameAr": { "type": "string", "title": "اسم المولود (عربي)", "minLength": 3 },
                "childFullNameEn": { "type": "string", "title": "Child Full Name (English)", "minLength": 3 },
                "dateOfBirth": { "type": "string", "format": "date", "title": "Date of Birth" },
                "placeOfBirth": { "type": "string", "title": "Place of Birth" },
                "gender": { "type": "string", "enum": ["Male", "Female"], "title": "Gender" },
                "fatherNationalId": { "type": "string", "title": "Father's National ID", "pattern": "^\\d{10}$" },
                "motherName": { "type": "string", "title": "Mother's Full Name" },
                "hospitalName": { "type": "string", "title": "Hospital / Birth Location" }
            }
        }
        """));
        db.Fees.Add(ServiceFee.Create(birthCert.Id, "BASE", "رسوم إصدار", "Issuance Fee", 2000m));

        // ── Service 4: Driving License ───────────────────────────────────────
        var drivingLicense = ServiceDefinition.Create(catTransport.Id, motId, "DRIVING_LICENSE_NEW",
            "رخصة قيادة جديدة", "New Driving License", requiredLoa: 2,
            descriptionAr: "طلب إصدار رخصة قيادة جديدة",
            descriptionEn: "Apply for a new driving license", averageDays: 10);
        db.Services.Add(drivingLicense);
        db.FormSchemas.Add(ServiceFormSchema.Create(drivingLicense.Id, """
        {
            "type": "object",
            "required": ["licenseClass", "hasCompletedTraining", "medicalCertificateUploaded"],
            "properties": {
                "licenseClass": { "type": "string", "enum": ["A-Motorcycle", "B-Private", "C-Commercial", "D-Bus"], "title": "License Class" },
                "hasCompletedTraining": { "type": "boolean", "title": "Completed Driving Training?" },
                "trainingCenterName": { "type": "string", "title": "Training Center Name" },
                "medicalCertificateUploaded": { "type": "boolean", "title": "Medical Certificate Uploaded?" },
                "visionTestResult": { "type": "string", "enum": ["Pass", "Corrected"], "title": "Vision Test Result" }
            }
        }
        """));
        db.Fees.Add(ServiceFee.Create(drivingLicense.Id, "BASE", "رسوم إصدار", "Issuance Fee", 10000m));
        db.Fees.Add(ServiceFee.Create(drivingLicense.Id, "EXPEDITE", "رسوم استعجال", "Express Processing", 8000m, isMandatory: false));

        // ── Service 5: Business Registration ─────────────────────────────────
        var bizReg = ServiceDefinition.Create(catCommerce.Id, mociId, "BUSINESS_REG",
            "تسجيل منشأة تجارية", "Business Registration", requiredLoa: 2,
            descriptionAr: "تسجيل نشاط تجاري أو شركة جديدة",
            descriptionEn: "Register a new business or commercial entity", averageDays: 21);
        db.Services.Add(bizReg);
        db.FormSchemas.Add(ServiceFormSchema.Create(bizReg.Id, """
        {
            "type": "object",
            "required": ["businessNameAr", "businessNameEn", "businessType", "capitalAmount", "ownerNationalId", "businessAddress"],
            "properties": {
                "businessNameAr": { "type": "string", "title": "اسم المنشأة (عربي)", "minLength": 3, "maxLength": 200 },
                "businessNameEn": { "type": "string", "title": "Business Name (English)", "minLength": 3, "maxLength": 200 },
                "businessType": { "type": "string", "enum": ["SoleProprietorship", "LLC", "Partnership", "Corporation"], "title": "Business Type" },
                "capitalAmount": { "type": "number", "minimum": 100000, "title": "Capital Amount (YER)" },
                "ownerNationalId": { "type": "string", "title": "Owner's National ID", "pattern": "^\\d{10}$" },
                "businessAddress": { "type": "string", "title": "Business Address", "maxLength": 500 },
                "businessActivity": { "type": "string", "title": "Primary Business Activity", "maxLength": 300 },
                "numberOfEmployees": { "type": "integer", "minimum": 0, "title": "Expected Number of Employees" }
            }
        }
        """));
        db.Fees.Add(ServiceFee.Create(bizReg.Id, "BASE", "رسوم تسجيل", "Registration Fee", 50000m));

        // ── Publish all seeded services ───────────────────────────────────────
        // Use direct IsActive set since Publish() requires FormSchema navigation loaded
        // and we're using direct DbContext adds
        await db.SaveChangesAsync();

        // Now load and publish each service
        var services = await db.Services.Include(s => s.FormSchema).ToListAsync();
        foreach (var svc in services)
        {
            svc.Publish();
        }
        await db.SaveChangesAsync();

        logger.LogInformation("[ServiceRequestModule] Seeded {Count} demo services.", services.Count);
    }
}
