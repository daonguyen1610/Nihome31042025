using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NihomeBackend.Authorization;

namespace NihomeBackend.IntegrationTests.Infrastructure;

/// <summary>
/// Reflects over every controller in the backend assembly to enumerate routes
/// guarded by <c>[RequirePermission]</c> at class or action level. Used by the
/// lockdown probe so newly added endpoints get unauth/wrong-role coverage
/// without any manual registration.
/// </summary>
public static class ProtectedEndpointInventory
{
    public sealed record Endpoint(
        string HttpMethod,
        string Url,
        IReadOnlyList<string> RequiredCodes,
        bool ExpectsMultipart);

    public static IReadOnlyList<Endpoint> Discover()
    {
        var backendAssembly = typeof(RequirePermissionAttribute).Assembly;
        var endpoints = new List<Endpoint>();

        var controllerTypes = backendAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controller in controllerTypes)
        {
            var classPerms = controller.GetCustomAttributes<RequirePermissionAttribute>(inherit: true).ToArray();
            var routeTemplates = controller.GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(r => r.Template ?? string.Empty)
                .Where(t => !t.Contains("v1", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var baseRoute = routeTemplates.FirstOrDefault() ?? string.Empty;
            var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? controller.Name[..^"Controller".Length]
                : controller.Name;

            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<AllowAnonymousAttribute>() != null) continue;

                var methodPerms = method.GetCustomAttributes<RequirePermissionAttribute>(inherit: true).ToArray();
                var effective = classPerms.Concat(methodPerms)
                    .Select(p => p.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (effective.Length == 0) continue;

                var expectsMultipart = method.GetCustomAttributes<ConsumesAttribute>(inherit: true)
                    .Any(c => c.ContentTypes.Any(ct => ct.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase)));

                foreach (var httpAttr in method.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
                {
                    var verb = httpAttr.HttpMethods.First();
                    var template = httpAttr.Template ?? string.Empty;
                    var url = Combine(baseRoute, template, controllerName);
                    endpoints.Add(new Endpoint(verb, url, effective, expectsMultipart));
                }
            }
        }

        return endpoints
            .OrderBy(e => e.Url, StringComparer.Ordinal)
            .ThenBy(e => e.HttpMethod, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Combine(string baseRoute, string template, string controllerName)
    {
        var combined = string.IsNullOrEmpty(template)
            ? baseRoute
            : $"{baseRoute.TrimEnd('/')}/{template.TrimStart('/')}";

        var withRoot = "/" + combined.TrimStart('/');
        var withController = withRoot.Replace("[controller]", controllerName, StringComparison.Ordinal);
        return MaterializePlaceholders(withController);
    }

    private static string MaterializePlaceholders(string url)
    {
        // {id:int} / {id} / {slug} / {lang} -> sentinel '1'. The permission
        // filter runs before model binding so '1' resolves both int and string
        // route constraints; the filter rejects with 401/403 before validation.
        return System.Text.RegularExpressions.Regex.Replace(
            url,
            @"\{[^{}]+\}",
            "1");
    }
}
