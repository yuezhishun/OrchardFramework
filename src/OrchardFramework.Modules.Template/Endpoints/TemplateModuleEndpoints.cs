using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OrchardFramework.Modules.Template.Endpoints;

public static class TemplateModuleEndpoints
{
    public static IEndpointRouteBuilder MapTemplateModuleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/module-template").WithTags("Module Template");

        group.MapGet("/ping", () => Results.Ok(new
        {
            ready = true,
            module = "OrchardFramework.ModuleTemplate",
            utcNow = DateTime.UtcNow
        }));

        return routes;
    }
}
