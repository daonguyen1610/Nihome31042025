using Microsoft.OpenApi.Models;
using NihomeBackend.Services;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NihomeBackend.Infrastructure.OpenApi;

public sealed class IdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.MethodInfo.IsDefined(typeof(IdempotencyAttribute), inherit: true))
        {
            return;
        }

        operation.Parameters ??= [];
        if (operation.Parameters.Any(parameter =>
                parameter.In == ParameterLocation.Header &&
                string.Equals(parameter.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Unique key used to safely replay this mutation.",
            Schema = new OpenApiSchema { Type = "string", MaxLength = IdempotencyService.MaxKeyLength },
        });
    }
}