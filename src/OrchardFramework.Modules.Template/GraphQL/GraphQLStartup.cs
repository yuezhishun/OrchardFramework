using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Apis.GraphQL;
using OrchardCore.Modules;

namespace OrchardFramework.Modules.Template.GraphQL;

[Feature("OrchardFramework.ModuleTemplate.GraphQL")]
[RequireFeatures("OrchardCore.Apis.GraphQL")]
public sealed class GraphQLStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, TemplateHealthQuery>();
    }
}
