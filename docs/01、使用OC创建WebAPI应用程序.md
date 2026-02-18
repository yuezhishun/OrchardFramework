## 1、使用OrchardCore创建Web项目

### 1.1、创建WebAPI项目

使用visual studio创建api项目WebHost

### 1.2、添加nuget包

`OrchardCore.Application.Mvc.Targets`

### 1.3、在Program中添加OC启动

~~~cs

~~~

### 1.4、添加配置：

注意：这只是为了演示，通过配置的方式设置租户与模块，实际使用时配置到数据库中，参考租户管理功能：Admin/Tenants

~~~json
  "OrchardCore": {
    "Default": {
      "State": "Running",
      "RequestUrlHost": null,
      "RequestUrlPrefix": null,
      "Features": [ "Ruoyi.Net", "LiveClient.Douyin" ],
      "CustomTitle": "Default Tenant",
      "CustomSetting": "Custom setting for Default tenant"
    },
    "CustomerA": {
      "State": "Running",
      "RequestUrlHost": null,
      "RequestUrlPrefix": "aaa",
      "Features": [ "Ruoyi.Net", "LiveClient.Douyin" ],
      "CustomTitle": "Customer A",
      "CustomSetting": "Custom setting for Customer A"
    },
    "CustomerB": {
      "State": "Running",
      "RequestUrlHost": null,
      "RequestUrlPrefix": "bbb",
      "Features": [ "Ruoyi.Net" ],
      "CustomTitle": "Customer B",
      "CustomSetting": "Custom setting for Customer B"
    }
  }
~~~

### 1.5、添加Controller

几个提取配置的方法

~~~cs
[Route("api/[controller]/[action]")]
[ApiController]
[AllowAnonymous]
public class HomeController : ControllerBase
{
    private readonly ShellSettings settings;
    private readonly IApplicationContext app;
    private readonly IShellSettingsManager settingsManager;

    public HomeController(ShellSettings settings, IApplicationContext app, IShellSettingsManager settingsManager) {
        this.settings = settings;
        this.app = app;
        this.settingsManager = settingsManager;
    }
    [HttpGet]
    public IActionResult Index()
    {
        return Ok(new {
            name= settings.Name, 
            RequestUrlHost= settings.RequestUrlHost,
            RequestUrlPrefix=settings.RequestUrlPrefix,
            State = settings.State,
            ShellConfiguration =settings.ShellConfiguration
        });
    }
    [HttpGet]
    public IActionResult Modules()
    {
        return Ok(app.Application.Modules.Select(module => module.Name));
    }
    [HttpGet]
    public async Task<IActionResult> ShellSettings()
    {
        var shellSettings = await settingsManager.LoadSettingsAsync();
        return Ok(shellSettings);
    }
}

~~~

## 2、Swagger配置

根据assembly生成不同的文档

~~~cs
public static IServiceCollection AddAssemblySwaggerDocs(this IServiceCollection services)
{
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
        foreach (var assemblyName in AssemblyNames)
        {
            options.SwaggerDoc(assemblyName, new OpenApiInfo
            {
                Title = $"{assemblyName} API",
                Version = "v1"
            });
        }
        options.DocInclusionPredicate((docName, apiDesc) =>
        {
            var descriptor = apiDesc.ActionDescriptor as ControllerActionDescriptor;
            if (descriptor != null)
            {
                var assemblyName = descriptor.ControllerTypeInfo.Assembly.GetName().Name;
                return docName == assemblyName;
            }
            return false;
        });

        options.TagActionsBy(apiDesc =>
        {
            var descriptor = apiDesc.ActionDescriptor as ControllerActionDescriptor;
            if (descriptor != null)
            {
                return new[] { descriptor.ControllerName };
            }
            return new[] { "Default" };
        });
    });
    return services;
}
public static IApplicationBuilder UseAssemblySwaggerDocs(this IApplicationBuilder app)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        foreach (var assemblyName in AssemblyNames)
        {
            c.SwaggerEndpoint($"/swagger/{assemblyName}/swagger.json", $"{assemblyName} API V1");
        }
    });
    return app;
}
~~~

