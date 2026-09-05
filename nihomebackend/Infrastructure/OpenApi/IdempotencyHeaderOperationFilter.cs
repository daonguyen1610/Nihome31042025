using Microsoft.OpenApi.Models;
using NihomeBackend.Services;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NihomeBackend.Infrastructure.OpenApi;

public sealed class IdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.MethodInfo.GetCustomAttributes(typeof(IdempotencyAttribute), inherit: true)
            .OfType<IdempotencyAttribute>()
            .SingleOrDefault();
        if (attribute is null)
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
            Required = attribute.RequireKey,
            Description = "Unique key used to safely replay this mutation.",
            Schema = new OpenApiSchema { Type = "string", MaxLength = IdempotencyService.MaxKeyLength },
        });
    }
}