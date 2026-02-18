using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OrchardFramework.Api.Tests;

public class ApiFlowTests
{
    [Fact]
    public void Iteration0_Recipe_Should_Keep_Minimal_Mvc_Features()
    {
        var recipePath = TestWebApplicationFactory.GetRecipePath("SaaS.Iteration0.recipe.json");
        Assert.True(File.Exists(recipePath));

        using var stream = File.OpenRead(recipePath);
        using var document = JsonDocument.Parse(stream);

        var featureStep = document.RootElement
            .GetProperty("steps")
            .EnumerateArray()
            .First(step => step.GetProperty("name").GetString() == "feature");

        var enabled = featureStep.GetProperty("enable").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        var disabled = featureStep.GetProperty("disable").EnumerateArray().Select(x => x.GetString()).ToHashSet();

        Assert.Contains("OrchardCore.Settings", enabled);
        Assert.Contains("OrchardCore.Recipes", enabled);

        Assert.Contains("OrchardCore.Admin", disabled);
        Assert.Contains("OrchardCore.Tenants", disabled);
        Assert.Contains("OrchardCore.Features", disabled);
        Assert.Contains("OrchardCore.Users", disabled);
        Assert.Contains("OrchardCore.Roles", disabled);
    }

    [Fact]
    public void Iteration1_Recipe_Should_Enable_Tenant_And_Feature_Management()
    {
        var recipePath = TestWebApplicationFactory.GetRecipePath("SaaS.Base.recipe.json");
        Assert.True(File.Exists(recipePath));

        using var stream = File.OpenRead(recipePath);
        using var document = JsonDocument.Parse(stream);

        var featureStep = document.RootElement
            .GetProperty("steps")
            .EnumerateArray()
            .First(step => step.GetProperty("name").GetString() == "feature");

        var enabled = featureStep.GetProperty("enable").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        var disabled = featureStep.GetProperty("disable").EnumerateArray().Select(x => x.GetString()).ToHashSet();

        Assert.Contains("OrchardCore.Admin", enabled);
        Assert.Contains("OrchardCore.Tenants", enabled);
        Assert.Contains("OrchardCore.Tenants.FeatureProfiles", enabled);
        Assert.Contains("OrchardCore.Users", enabled);
        Assert.Contains("OrchardCore.Roles", enabled);
        Assert.Contains("OrchardCore.Features", enabled);
        Assert.Contains("OrchardCore.Localization", enabled);
        Assert.Contains("OrchardCore.Apis.GraphQL", enabled);
        Assert.Contains("OrchardCore.OpenId", enabled);
        Assert.Contains("OrchardCore.OpenId.Management", enabled);
        Assert.Contains("OrchardCore.OpenId.Server", enabled);
        Assert.Contains("OrchardCore.OpenId.Validation", enabled);
        Assert.Contains("OrchardCore.Navigation", enabled);
        Assert.Contains("TheAdmin", enabled);

        Assert.Contains("OrchardCore.Contents", disabled);
        Assert.Contains("OrchardCore.ContentTypes", disabled);
        Assert.Contains("OrchardCore.Widgets", disabled);
    }

    [Fact]
    public async Task Iteration0_Should_AutoSetup_And_Keep_Admin_Features_Disabled()
    {
        using var factory = new TestWebApplicationFactory("SaaS.Iteration0");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var summary = await WaitForReadySummaryAsync(client);
        var featureMap = ToFeatureMap(summary);

        Assert.True(summary.GetProperty("ready").GetBoolean());
        Assert.True(featureMap.TryGetValue("OrchardCore.Admin", out var adminEnabled) && !adminEnabled);
        Assert.True(featureMap.TryGetValue("OrchardCore.Tenants", out var tenantsEnabled) && !tenantsEnabled);
        Assert.True(featureMap.TryGetValue("OrchardCore.Features", out var featuresEnabled) && !featuresEnabled);

        var adminResponse = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.NotFound, adminResponse.StatusCode);
    }

    [Fact]
    public async Task Iteration1_Should_AutoSetup_And_Enable_Tenant_Feature_Management()
    {
        using var factory = new TestWebApplicationFactory("SaaS.Base");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var summary = await WaitForReadySummaryAsync(client);
        var featureMap = ToFeatureMap(summary);

        Assert.True(summary.GetProperty("ready").GetBoolean());
        Assert.True(summary.GetProperty("tenantCount").GetInt32() >= 1);
        Assert.True(featureMap.TryGetValue("OrchardCore.Admin", out var adminEnabled) && adminEnabled);
        Assert.True(featureMap.TryGetValue("OrchardCore.Tenants", out var tenantsEnabled) && tenantsEnabled);
        Assert.True(featureMap.TryGetValue("OrchardCore.Features", out var featuresEnabled) && featuresEnabled);
    }

    [Fact]
    public async Task Iteration1_Admin_Routes_Should_Be_Closed_When_Admin_Path_Is_Disabled()
    {
        using var factory = new TestWebApplicationFactory("SaaS.Base");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await WaitForReadySummaryAsync(client);

        var adminResponse = await client.GetAsync("/Admin");
        var tenantsResponse = await client.GetAsync("/Admin/Tenants");
        var featuresResponse = await client.GetAsync("/Admin/Features");
        var prefixedAdminResponse = await client.GetAsync("/saas-admin/Admin");

        Assert.Equal(HttpStatusCode.NotFound, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, tenantsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, featuresResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, prefixedAdminResponse.StatusCode);
    }

    [Fact]
    public async Task Iteration1_Links_Should_Mark_Admin_Entry_As_Disabled()
    {
        using var factory = new TestWebApplicationFactory("SaaS.Base");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await WaitForReadySummaryAsync(client);

        var response = await client.GetAsync("/api/saas/links");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var links = document.RootElement
            .EnumerateArray()
            .Select(x => new
            {
                Url = x.GetProperty("url").GetString() ?? string.Empty,
                Enabled = x.GetProperty("enabled").GetBoolean()
            })
            .ToList();

        Assert.Contains(links, x => x.Url == "/saas-admin/Admin" && !x.Enabled);
        Assert.Contains(links, x => x.Url == "/api/saas/capabilities" && x.Enabled);
    }

    [Fact]
    public async Task Iteration1_Capabilities_Should_Report_Admin_Path_Disabled()
    {
        using var factory = new TestWebApplicationFactory("SaaS.Base");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await WaitForReadySummaryAsync(client);

        var response = await client.GetAsync("/api/saas/capabilities");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.True(root.GetProperty("adminPathAccessDisabled").GetBoolean());
        Assert.Equal("/saas-admin/Admin", root.GetProperty("fallbackAdminUi").GetString());
        Assert.True(root.GetProperty("allowAnonymousManagementApi").GetBoolean());
        var availableAdapters = root.GetProperty("availableAdapters")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("/api/management/tenants", availableAdapters);
        Assert.Contains("/api/management/features", availableAdapters);
        Assert.Contains("/api/management/feature-profiles", availableAdapters);
        Assert.Contains("/api/management/users", availableAdapters);
        Assert.Contains("/api/management/roles", availableAdapters);
        Assert.Contains("/api/management/permissions", availableAdapters);
        Assert.Contains("/api/management/roles/{id}/permissions", availableAdapters);
        Assert.Contains("/api/management/site-settings", availableAdapters);
        Assert.Contains("/api/management/localization", availableAdapters);
        Assert.Contains("/api/management/openid/applications", availableAdapters);
        Assert.Contains("/api/management/openid/scopes", availableAdapters);
        Assert.Contains("/api/management/recipes", availableAdapters);
        Assert.Contains("/api/management/recipes/execute", availableAdapters);

        var plannedAdapters = root.GetProperty("plannedAdapters")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Empty(plannedAdapters);
    }

    [Fact]
    public async Task Iteration1_Management_Endpoints_Should_Return_Basic_Payloads()
    {
        using var factory = new TestWebApplicationFactory("SaaS.Base");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await WaitForReadySummaryAsync(client);

        var tenantsResponse = await client.GetAsync("/api/management/tenants");
        tenantsResponse.EnsureSuccessStatusCode();
        using var tenantsDocument = JsonDocument.Parse(await tenantsResponse.Content.ReadAsStringAsync());
        var tenants = tenantsDocument.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(tenants);
        Assert.Contains(tenants, x => x.GetProperty("name").GetString() == "Default");

        var featuresResponse = await client.GetAsync("/api/management/features");
        featuresResponse.EnsureSuccessStatusCode();
        using var featuresDocument = JsonDocument.Parse(await featuresResponse.Content.ReadAsStringAsync());
        Assert.Equal("Default", featuresDocument.RootElement.GetProperty("tenant").GetString());
        Assert.True(featuresDocument.RootElement.GetProperty("features").GetArrayLength() > 0);

        var featureProfilesResponse = await client.GetAsync("/api/management/feature-profiles");
        featureProfilesResponse.EnsureSuccessStatusCode();
        using var profilesDocument = JsonDocument.Parse(await featureProfilesResponse.Content.ReadAsStringAsync());
        Assert.True(profilesDocument.RootElement.ValueKind is JsonValueKind.Array);

        var usersResponse = await client.GetAsync("/api/management/users");
        usersResponse.EnsureSuccessStatusCode();
        using var usersDocument = JsonDocument.Parse(await usersResponse.Content.ReadAsStringAsync());
        Assert.True(usersDocument.RootElement.ValueKind is JsonValueKind.Array);

        var rolesResponse = await client.GetAsync("/api/management/roles");
        rolesResponse.EnsureSuccessStatusCode();
        using var rolesDocument = JsonDocument.Parse(await rolesResponse.Content.ReadAsStringAsync());
        Assert.True(rolesDocument.RootElement.ValueKind is JsonValueKind.Array);

        var permissionsResponse = await client.GetAsync("/api/management/permissions");
        permissionsResponse.EnsureSuccessStatusCode();
        using var permissionsDocument = JsonDocument.Parse(await permissionsResponse.Content.ReadAsStringAsync());
        Assert.True(permissionsDocument.RootElement.ValueKind is JsonValueKind.Array);

        var siteSettingsResponse = await client.GetAsync("/api/management/site-settings");
        siteSettingsResponse.EnsureSuccessStatusCode();
        using var siteSettingsDocument = JsonDocument.Parse(await siteSettingsResponse.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(siteSettingsDocument.RootElement.GetProperty("siteName").GetString()));

        var localizationResponse = await client.GetAsync("/api/management/localization");
        localizationResponse.EnsureSuccessStatusCode();
        using var localizationDocument = JsonDocument.Parse(await localizationResponse.Content.ReadAsStringAsync());
        Assert.True(localizationDocument.RootElement.GetProperty("supportedCultures").ValueKind is JsonValueKind.Array);

        var openIdApplicationsResponse = await client.GetAsync("/api/management/openid/applications");
        openIdApplicationsResponse.EnsureSuccessStatusCode();
        using var openIdApplicationsDocument = JsonDocument.Parse(await openIdApplicationsResponse.Content.ReadAsStringAsync());
        Assert.True(openIdApplicationsDocument.RootElement.ValueKind is JsonValueKind.Array);
        var applicationCount = openIdApplicationsDocument.RootElement.GetArrayLength();

        var openIdScopesResponse = await client.GetAsync("/api/management/openid/scopes");
        openIdScopesResponse.EnsureSuccessStatusCode();
        using var openIdScopesDocument = JsonDocument.Parse(await openIdScopesResponse.Content.ReadAsStringAsync());
        Assert.True(openIdScopesDocument.RootElement.ValueKind is JsonValueKind.Array);
        var scopeCount = openIdScopesDocument.RootElement.GetArrayLength();

        var recipesResponse = await client.GetAsync("/api/management/recipes");
        recipesResponse.EnsureSuccessStatusCode();
        using var recipesDocument = JsonDocument.Parse(await recipesResponse.Content.ReadAsStringAsync());
        Assert.True(recipesDocument.RootElement.ValueKind is JsonValueKind.Array);

        var summaryResponse = await client.GetAsync("/api/saas/summary");
        summaryResponse.EnsureSuccessStatusCode();
        using var summaryDocument = JsonDocument.Parse(await summaryResponse.Content.ReadAsStringAsync());
        var openId = summaryDocument.RootElement.GetProperty("openId");
        Assert.Equal(applicationCount, openId.GetProperty("applications").GetInt32());
        Assert.Equal(scopeCount, openId.GetProperty("scopes").GetInt32());
        Assert.True(openId.GetProperty("tokens").GetInt32() >= 0);
        Assert.True(openId.GetProperty("authorizations").GetInt32() >= 0);
    }

    private static Dictionary<string, bool> ToFeatureMap(JsonElement summary)
    {
        return summary.GetProperty("requiredSaasFeatures")
            .EnumerateArray()
            .ToDictionary(
                x => x.GetProperty("name").GetString() ?? string.Empty,
                x => x.GetProperty("enabled").GetBoolean(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<JsonElement> WaitForReadySummaryAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var summaryResponse = await client.GetAsync("/api/saas/summary");
            if (summaryResponse.StatusCode == HttpStatusCode.Found)
            {
                await client.GetAsync("/");
                await Task.Delay(300);
                continue;
            }

            summaryResponse.EnsureSuccessStatusCode();
            using var summaryDocument = JsonDocument.Parse(await summaryResponse.Content.ReadAsStringAsync());
            var summary = summaryDocument.RootElement.Clone();
            if (summary.GetProperty("ready").GetBoolean())
            {
                return summary;
            }

            await Task.Delay(300);
        }

        throw new TimeoutException("SaaS summary never became ready after auto-setup retries.");
    }
}
