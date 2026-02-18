using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardFramework.Modules.Template.Endpoints;
using OrchardFramework.Modules.Template.Migrations;
using OrchardFramework.Modules.Template.Permissions;

namespace OrchardFramework.Modules.Template;

[Feature("OrchardFramework.ModuleTemplate")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPermissionProvider, TemplatePermissions>();
        services.AddDataMigration<TemplateMigrations>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapTemplateModuleEndpoints();
    }
}
