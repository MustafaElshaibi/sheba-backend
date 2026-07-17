using FluentAssertions;
using NSubstitute;
using Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Tests.Application.Commands;

/// <summary>
/// Unit tests for SubmitServiceRequestValidator's JSON Schema form-data validation (T-SRV-2).
/// Uses NSubstitute to mock IServiceDefinitionRepository — no EF Core/DB dependency.
/// </summary>
public sealed class SubmitServiceRequestValidatorTests
{
    private const string Schema = """
    {
        "type": "object",
        "required": ["fullName"],
        "properties": {
            "fullName": { "type": "string", "minLength": 3 },
            "age": { "type": "integer", "minimum": 0 }
        }
    }
    """;

    private readonly IServiceDefinitionRepository _repo = Substitute.For<IServiceDefinitionRepository>();
    private readonly SubmitServiceRequestValidator _sut;
    private readonly Guid _serviceId = Guid.NewGuid();
    private readonly Guid _citizenId = Guid.NewGuid();

    public SubmitServiceRequestValidatorTests()
        => _sut = new SubmitServiceRequestValidator(_repo);

    private void WithSchema(string? schemaJson)
    {
        var schema = schemaJson is null ? null : ServiceFormSchema.Create(_serviceId, schemaJson);
        _repo.GetFormSchemaByServiceIdAsync(_serviceId, Arg.Any<CancellationToken>()).Returns(schema);
    }

    [Fact]
    public async Task Validate_FormDataSatisfiesSchema_Passes()
    {
        WithSchema(Schema);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, """{"fullName":"Ali Mohammed","age":30}""");

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_MissingRequiredField_FailsWithFormDataFieldKey()
    {
        WithSchema(Schema);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, """{"age":30}""");

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "formData");
    }

    [Fact]
    public async Task Validate_WrongFieldType_FailsWithPerFieldKey()
    {
        WithSchema(Schema);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, """{"fullName":"Ali","age":"not-a-number"}""");

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "formData.age");
    }

    [Fact]
    public async Task Validate_MalformedJson_FailsWithFormDataKey()
    {
        WithSchema(Schema);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, "{not valid json");

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "formData"
            && e.ErrorMessage == "Form data must be valid JSON.");
    }

    [Fact]
    public async Task Validate_ServiceHasNoFormSchema_PassesThrough()
    {
        WithSchema(null);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, """{"anything":"goes"}""");

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyFormDataJson_FailsNotEmptyRule()
    {
        WithSchema(Schema);
        var command = new SubmitServiceRequestCommand(_serviceId, _citizenId, "");

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FormDataJson");
    }
}
