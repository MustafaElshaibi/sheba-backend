using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Sheba.Api.Swagger;

/// <summary>
/// Documents the JSend envelope in Swagger (T-API-1): every JSON response schema on non-exempt
/// routes is wrapped as <c>{ status, data }</c> (plus <c>message</c>/<c>code</c> for 5xx) so RP
/// developers see the real wire shapes. OIDC protocol routes keep their spec-mandated schemas.
/// </summary>
public sealed class JSendOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + (context.ApiDescription.RelativePath ?? string.Empty);
        if (path.StartsWith("/connect") || path.StartsWith("/.well-known"))
            return;

        foreach (var (statusKey, response) in operation.Responses)
        {
            foreach (var (mediaType, media) in response.Content)
            {
                if (!mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
                    continue;
                media.Schema = Wrap(media.Schema, statusKey);
            }
        }
    }

    private static OpenApiSchema Wrap(OpenApiSchema? inner, string statusKey)
    {
        var jsendStatus = statusKey.Length > 0 ? statusKey[0] : '2';
        var schema = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "status" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["status"] = new()
                {
                    Type = "string",
                    Enum = [new OpenApiString(jsendStatus switch
                    {
                        '2' => "success",
                        '5' => "error",
                        _   => "fail",
                    })],
                },
            },
        };

        if (jsendStatus == '5')
        {
            schema.Required.Add("message");
            schema.Properties["message"] = new() { Type = "string" };
            schema.Properties["code"] = new() { Type = "integer", Nullable = true };
            schema.Properties["data"] = new()
            {
                Type = "object",
                Nullable = true,
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["correlation_id"] = new() { Type = "string" },
                },
            };
            return schema;
        }

        if (jsendStatus != '2')
        {
            // fail — data keys mirror the offending input fields; document as a free-form map.
            schema.Properties["data"] = new()
            {
                Type = "object",
                AdditionalProperties = new OpenApiSchema { Type = "string" },
            };
            return schema;
        }

        schema.Properties["data"] = inner is null
            ? new OpenApiSchema { Nullable = true }
            : inner;
        return schema;
    }
}
