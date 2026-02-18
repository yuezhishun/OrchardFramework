using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OrchardFramework Module Template",
    Author = "OrchardFramework",
    Website = "https://pty.addai.vip/saas",
    Version = "1.0.0",
    Description = "Reusable scaffold module template for OrchardFramework."
)]

[assembly: Feature(
    Id = "OrchardFramework.ModuleTemplate",
    Name = "OrchardFramework Module Template",
    Description = "Scaffold feature with permissions, migration, and sample endpoints.",
    Category = "OrchardFramework"
)]

[assembly: Feature(
    Id = "OrchardFramework.ModuleTemplate.GraphQL",
    Name = "OrchardFramework Module Template GraphQL",
    Description = "GraphQL extension points for the scaffold module.",
    Dependencies =
    [
        "OrchardFramework.ModuleTemplate",
        "OrchardCore.Apis.GraphQL"
    ],
    Category = "OrchardFramework"
)]
