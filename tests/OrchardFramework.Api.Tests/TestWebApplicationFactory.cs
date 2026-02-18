using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OrchardFramework.Api.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _contentRoot;
    private readonly string _recipeName;
    private readonly string _siteName;
    private readonly string _dbPath;
    private readonly bool _ownsContentRoot;
    private readonly Dictionary<string, string?> _additionalSettings;
    public string ContentRootPath => _contentRoot;
    public string DefaultTenantDatabasePath => _dbPath;

    public TestWebApplicationFactory(
        string recipeName = "SaaS.Base",
        string? contentRootPath = null,
        bool ownsContentRoot = true,
        IDictionary<string, string?>? additionalSettings = null)
    {
        var tempRoot = string.IsNullOrWhiteSpace(contentRootPath)
            ? Path.Combine(Path.GetTempPath(), $"orchardframework-tests-{Guid.NewGuid():N}")
            : contentRootPath;
        _contentRoot = tempRoot;
        _recipeName = recipeName;
        _siteName = recipeName.Equals("SaaS.Iteration0", StringComparison.OrdinalIgnoreCase)
            ? "OrchardFramework Iteration0 Test"
            : "OrchardFramework Iteration1 Test";
        _dbPath = Path.Combine(_contentRoot, "test-default-tenant.db");
        _ownsContentRoot = string.IsNullOrWhiteSpace(contentRootPath) || ownsContentRoot;
        _additionalSettings = additionalSettings is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?>(additionalSettings);

        Directory.CreateDirectory(_contentRoot);
        Directory.CreateDirectory(Path.Combine(_contentRoot, "Recipes"));

        File.Copy(GetRecipePath("SaaS.Base.recipe.json"), Path.Combine(_contentRoot, "Recipes", "SaaS.Base.recipe.json"), overwrite: true);
        File.Copy(GetRecipePath("SaaS.Iteration0.recipe.json"), Path.Combine(_contentRoot, "Recipes", "SaaS.Iteration0.recipe.json"), overwrite: true);
    }

    public static string GetRecipePath(string recipeFileName = "SaaS.Base.recipe.json")
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "OrchardFramework.Api", "Recipes", recipeFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not find SaaS.Base.recipe.json from test context.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(_contentRoot);
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["OrchardCore:OrchardCore_AutoSetup:AutoSetupPath"] = "/api/saas/summary",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:ShellName"] = "Default",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:SiteName"] = _siteName,
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:SiteTimeZone"] = "UTC",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:AdminUsername"] = "admin",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:AdminEmail"] = "admin@test.local",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:AdminPassword"] = "Admin123!",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:DatabaseProvider"] = "Sqlite",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:DatabaseConnectionString"] = $"Data Source={_dbPath}",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:DatabaseTablePrefix"] = "",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:DatabaseSchema"] = "",
                ["OrchardCore:OrchardCore_AutoSetup:Tenants:0:RecipeName"] = _recipeName,
                ["SaaS:AdminBasePath"] = "/saas-admin",
                ["SaaS:DisableAdminPathAccess"] = "true",
                ["SaaS:AllowAnonymousManagementApi"] = "true"
            };

            foreach (var pair in _additionalSettings)
            {
                settings[pair.Key] = pair.Value;
            }

            configBuilder.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        if (_ownsContentRoot && Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }
}
