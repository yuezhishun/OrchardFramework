using GraphQL.Resolvers;
using GraphQL.Types;
using OrchardCore.Apis.GraphQL;

namespace OrchardFramework.Modules.Template.GraphQL;

public sealed class TemplateHealthQuery : ISchemaBuilder
{
    public Task BuildAsync(ISchema schema)
    {
        schema.Query.AddField(new FieldType
        {
            Name = "moduleTemplateHealth",
            Description = "Returns health status for the OrchardFramework module template.",
            Type = typeof(StringGraphType),
            Resolver = new FuncFieldResolver<string>(_ => "ok")
        });

        return Task.CompletedTask;
    }

    public Task<string> GetIdentifierAsync()
    {
        return Task.FromResult("orchardframework-module-template-graphql-v1");
    }
}
