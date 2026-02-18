# Orchard Core Headless 模式实现机制

## 概述

Orchard Core 的 Headless 模式通过 **Shape 系统** 实现了内容和视图的完全解耦。模块包含视图页面是正常的，因为在传统 CMS 模式下需要这些视图，但在 Headless 模式下这些视图不会被使用。
Orchard Core最新源码位置：/home/yueyuan/OrchardCore

## 1. 核心设计理念：内容与表现分离

Orchard Core 采用三层架构实现 Headless：

```
┌─────────────────────────────────────────────────────────────────────┐
│                      内容请求                                    │
└─────────────────────────┬───────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────────────┐
│              ContentItemDisplayManager                              │
│         将 ContentItem 转换为 Shape 对象                          │
└─────────────────────────┬───────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────────────┐
│              DisplayDriver (内容部件驱动)                             │
│    ┌─────────────────────────────────────────────────────────┐      │
│    │  LiquidPartDisplayDriver.DisplayAsync()                │      │
│    │    - 创建 Shape: "LiquidPart"                        │      │
│    │    - 填充 ViewModel 数据                            │      │
│    │    - 指定 Location: Detail/Summary                   │      │
│    └─────────────────────────────────────────────────────────┘      │
└─────────────────────────┬───────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────────────┐
│          DefaultHtmlDisplay.ExecuteAsync()                           │
│              (渲染决策点)                                         │
│                                                                 │
│   ┌─────────────────────┬─────────────────────┬────────────┐   │
│   │                     │                     │            │   │
│   ▼                     ▼                     ▼            ▼   │
│ 传统 Web 模式         Headless/API 模式    其他输出格式   自定义绑定 │
│                     │                     │            │       │   │
│   ├─ 查找模板文件       ├─ 直接返回 Shape    ├─ 自定义    ├─ 动态│
│   │   .cshtml          │      数据           │  格式       │  模板 │
│   ├─ 渲染 HTML        │   (ContentItem)   │            │       │
│   │                    │                     │            │       │
│   └─ 输出 HTML         └─ 返回 JSON/GraphQL └────────────┴───────│
│                                                                 │
└─────────────────────────────────────────────────────────────────────┘
```

## 2. 关键实现机制

### 2.1 DisplayDriver 创建 Shape

文件位置：`src/OrchardCore.Modules/OrchardCore.Liquid/Drivers/LiquidPartDisplayDriver.cs:25`

```csharp
public override Task<IDisplayResult> DisplayAsync(LiquidPart liquidPart, BuildPartDisplayContext context)
{
    return CombineAsync(
        Initialize<LiquidPartViewModel>("LiquidPart", m => BuildViewModel(m, liquidPart))
            .Location(OrchardCoreConstants.DisplayType.Detail, "Content"),
        Initialize<LiquidPartViewModel>("LiquidPart_Summary", m => BuildViewModel(m, liquidPart))
            .Location(OrchardCoreConstants.DisplayType.Summary, "Content")
    );
}
```

**关键点**：这里只创建了 Shape 和填充数据，**并没有指定具体的视图文件**。

### 2.2 Shape 模板绑定策略

文件位置：`src/OrchardCore/OrchardCore.DisplayManagement/Descriptors/ShapeTemplateStrategy/ShapeTemplateBindingStrategy.cs:48`

```csharp
public override async ValueTask DiscoverAsync(ShapeTableBuilder builder)
{
    // 扫描所有模块的 Views 目录
    // 自动发现并绑定 .cshtml 文件到 Shape 类型
    // 例如: LiquidPart.cshtml → ShapeType "LiquidPart"

    var hits = activeExtensions.Select(extensionDescriptor =>
    {
        var filePaths = _fileProviderAccessor.FileProvider.GetViewFilePaths(
            PathExtensions.Combine(extensionDescriptor.SubPath, subPath),
            _viewEnginesFileExtensions,
            inViewsFolder: true, inDepth: false);

        // ... 收集文件信息
    });

    foreach (var hit in hits)
    {
        foreach (var feature in hit.extensionDescriptor.Features.Where(f => enabledFeatureIds.Contains(f.Id)))
        {
            builder.Describe(hit.shapeContext.harvestShapeHit.ShapeType)
                .From(feature)
                .BoundAs(relativePath, displayContext =>
                {
                    var viewEngine = displayContext.ServiceProvider
                        .GetServices<IShapeTemplateViewEngine>()
                        .Single(e => e.GetType() == viewEngineType);

                    return viewEngine.RenderAsync(relativePath, displayContext);
                });
        }
    }
}
```

**工作流程**：
1. 扫描所有启用的模块/主题的 `Views` 目录
2. 根据文件名推断 Shape 类型（如 `LiquidPart.cshtml` → `LiquidPart`）
3. 将文件路径绑定到对应的 Shape 类型

### 2.3 条件渲染逻辑

文件位置：`src/OrchardCore/OrchardCore.DisplayManagement/Implementation/DefaultHtmlDisplay.cs:120`

```csharp
public async Task<IHtmlContent> ExecuteAsync(DisplayContext context)
{
    var shape = context.Value;

    // 1. 检查 shape 是否为空或预渲染
    if (shape.IsNullOrEmpty())
    {
        return HtmlString.Empty;
    }

    if (shape is IHtmlContent htmlContent)
    {
        return htmlContent;  // 已经渲染完成，直接返回
    }

    // 2. 获取 Shape 表
    var theme = await _themeManager.GetThemeAsync();
    var shapeTable = await _shapeTableManager.GetShapeTableAsync(theme?.Id);

    // 3. 触发 Displaying 事件
    await _shapeDisplayEvents.InvokeAsync((e, displayContext) => e.DisplayingAsync(displayContext), displayContext, _logger);

    // 4. 获取实际的 Shape 绑定（模板）
    var actualBinding = await GetShapeBindingAsync(shapeMetadata.Type, shapeMetadata.Alternates, shapeTable);

    if (actualBinding == null)
    {
        throw new InvalidOperationException($"The shape type '{shapeMetadata.Type}' is not found for theme '{theme?.Id}'");
    }

    // 5. 使用绑定的模板或默认处理
    shape.Metadata.ChildContent = await ProcessAsync(actualBinding, shape, localContext);

    // 6. 处理 Wrappers
    if (shape.Metadata.Wrappers.Count > 0)
    {
        foreach (var frameType in shape.Metadata.Wrappers)
        {
            var frameBinding = await GetShapeBindingAsync(frameType, AlternatesCollection.Empty, shapeTable);
            if (frameBinding != null)
            {
                shape.Metadata.ChildContent = await ProcessAsync(frameBinding, shape, localContext);
            }
        }
    }

    return shape.Metadata.ChildContent;
}
```

### 2.4 IShapeBindingResolver 扩展点

文件位置：`src/OrchardCore/OrchardCore.DisplayManagement/IShapeBindingResolver.cs`

```csharp
/// <summary>
/// An implementation of this interface is called whenever a shape template
/// is seeked. it can be used to provide custom dynamic templates, for instance to override
/// any view engine based ones.
/// </summary>
public interface IShapeBindingResolver
{
    Task<ShapeBinding> GetShapeBindingAsync(string shapeType);
}
```

这个接口允许在运行时动态提供 Shape 模板绑定，而不依赖物理视图文件。

## 3. Headless 模式的三种实现方式

### 方式 1：REST API (直接返回 ContentItem)

文件位置：`src/OrchardCore.Modules/OrchardCore.Demo/Controllers/ContentApiController.cs:24`

```csharp
[Route("api/demo")]
[Authorize(AuthenticationSchemes = "Api"), IgnoreAntiforgeryToken, AllowAnonymous]
[ApiController]
public sealed class ContentApiController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentManager _contentManager;

    public ContentApiController(
        IAuthorizationService authorizationService,
        IContentManager contentManager)
    {
        _authorizationService = authorizationService;
        _contentManager = contentManager;
    }

    public async Task<IActionResult> GetById(string id)
    {
        var contentItem = await _contentManager.GetAsync(id);

        if (contentItem == null)
        {
            return NotFound();
        }

        return new ObjectResult(contentItem);  // 直接返回对象，不经过 Shape 渲染
    }

    public async Task<IActionResult> GetAuthorizedById(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.DemoAPIAccess))
        {
            return this.ChallengeOrForbid("Api");
        }

        var contentItem = await _contentManager.GetAsync(id);

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, contentItem))
        {
            return this.ChallengeOrForbid("Api");
        }

        if (contentItem == null)
        {
            return NotFound();
        }

        return new ObjectResult(contentItem);
    }
}
```

**特点**：
- 完全绕过 Shape 系统
- 直接返回序列化的 ContentItem 对象
- 适合简单的 CRUD 操作

### 方式 2：GraphQL API (通过 Schema 暴露)

GraphQL Schema 服务会自动从 ContentItem 和 ContentPart 生成类型。

**查询示例**：
```graphql
{
  contentItem(id: "xxx") {
    contentType
    displayed(displayType: "Detail") {
      ...on LiquidPart {
        liquid
      }
    }
  }
}
```

**特点**：
- GraphQL Schema 直接映射到数据模型
- 不需要视图模板
- 支持灵活的数据查询
- 类型安全

### 方式 3：自定义 ShapeBindingResolver

文件位置：`src/OrchardCore.Modules/OrchardCore.Templates/Services/TemplatesShapeBindingResolver.cs:36`

```csharp
public class TemplatesShapeBindingResolver : IShapeBindingResolver
{
    private TemplatesDocument _templatesDocument;
    private readonly TemplatesDocument _localTemplates;

    private readonly TemplatesManager _templatesManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HtmlEncoder _htmlEncoder;
    private bool? _isAdmin;

    public TemplatesShapeBindingResolver(
        TemplatesManager templatesManager,
        ILiquidTemplateManager liquidTemplateManager,
        PreviewTemplatesProvider previewTemplatesProvider,
        IHttpContextAccessor httpContextAccessor,
        HtmlEncoder htmlEncoder)
    {
        _templatesManager = templatesManager;
        _liquidTemplateManager = liquidTemplateManager;
        _httpContextAccessor = httpContextAccessor;
        _htmlEncoder = htmlEncoder;
        _localTemplates = previewTemplatesProvider.GetTemplates();
    }

    public async Task<ShapeBinding> GetShapeBindingAsync(string shapeType)
    {
        // 缓存管理状态
        _isAdmin ??= AdminAttribute.IsApplied(_httpContextAccessor.HttpContext);

        if (_isAdmin.Value)
        {
            return null;  // 管理员模式下不使用动态模板
        }

        // 检查本地预览模板
        if (_localTemplates?.Templates?.TryGetValue(shapeType, out var localTemplate) == true)
        {
            return BuildShapeBinding(shapeType, localTemplate);
        }

        // 获取数据库中的模板
        _templatesDocument ??= await _templatesManager.GetTemplatesDocumentAsync();

        if (_templatesDocument.Templates.TryGetValue(shapeType, out var template))
        {
            return BuildShapeBinding(shapeType, template);
        }

        return null;  // 返回 null 表示使用默认行为
    }

    private ShapeBinding BuildShapeBinding(string shapeType, Template template)
    {
        return new ShapeBinding()
        {
            BindingName = shapeType,
            BindingSource = shapeType,
            BindingAsync = displayContext =>
                _liquidTemplateManager.RenderHtmlContentAsync(template.Content, _htmlEncoder, displayContext.Value),
        };
    }
}
```

**特点**：
- 允许在数据库中存储模板
- 无需物理视图文件
- 支持动态模板更新
- 通过 `IShapeBindingResolver` 扩展点实现

## 4. 为什么模块包含视图文件

```
┌─────────────────────────────────────────────────────────────┐
│              模块视图文件的作用范围                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Views/                                                    │
│    ├── LiquidPart.cshtml         ← 传统 Web 显示用             │
│    ├── LiquidPart.Summary.cshtml  ← 列表显示用               │
│    └── LiquidPart.Edit.cshtml     ← 后台编辑界面用 (必需)    │
│                                                             │
│  Headless 模式：                                           │
│    - LiquidPart.cshtml:         不会被调用                    │
│    - LiquidPart.Summary.cshtml:  不会被调用                    │
│    - LiquidPart.Edit.cshtml:     仍然需要（后台编辑）          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

视图文件在 Headless 模式下**不会被使用**，但存在的原因：

1. **向后兼容**：同一套代码可以同时支持传统 Web 模式和 Headless 模式
2. **管理界面**：后台管理界面仍然需要视图来渲染编辑器
3. **灵活性**：用户可以选择是否使用视图模板
4. **渐进式迁移**：可以逐步从传统模式迁移到 Headless

## 5. DisplayType 系统

Orchard Core 定义了多种显示类型，允许同一内容在不同场景下有不同的表示：

文件位置：`src/OrchardCore/OrchardCore.ContentManagement/DisplayManagement/DisplayType.cs`

```csharp
public static class DisplayType
{
    public const string Detail = "Detail";           // 完整详情
    public const string Summary = "Summary";         // 摘要显示
    public const string DetailAdmin = "DetailAdmin";   // 管理员详情
    public const string SummaryAdmin = "SummaryAdmin"; // 管理员摘要
}
```

在 Headless 模式下，可以通过指定 DisplayType 获取不同粒度的数据：

```graphql
{
  contentItem(id: "xxx") {
    contentType
    displayed(displayType: "Summary") {  # 获取摘要数据
      ...on LiquidPart {
        liquid
      }
    }
  }
}
```

## 6. 架构组件总结

### 核心接口和类

| 组件 | 位置 | 作用 |
|------|------|------|
| `IHtmlDisplay` | `src/OrchardCore/OrchardCore.DisplayManagement/Implementation/IHtmlDisplay.cs` | Shape 渲染核心接口 |
| `DefaultHtmlDisplay` | `src/OrchardCore/OrchardCore.DisplayManagement/Implementation/DefaultHtmlDisplay.cs` | 默认 HTML 渲染器 |
| `IShapeBindingResolver` | `src/OrchardCore/OrchardCore.DisplayManagement/IShapeBindingResolver.cs` | 动态 Shape 绑定解析器 |
| `ShapeTemplateBindingStrategy` | `src/OrchardCore/OrchardCore.DisplayManagement/Descriptors/ShapeTemplateStrategy/ShapeTemplateBindingStrategy.cs` | 模板发现和绑定策略 |
| `IShapeTemplateViewEngine` | `src/OrchardCore/OrchardCore.DisplayManagement/Descriptors/ShapeTemplateStrategy/IShapeTemplateViewEngine.cs` | 模板视图引擎接口 |

### API 模块

| 模块 | 位置 | API 类型 |
|------|------|---------|
| `OrchardCore.Apis.GraphQL` | `src/OrchardCore.Modules/OrchardCore.Apis.GraphQL/` | GraphQL API |
| `OrchardCore.Apis.Liquid` | `src/OrchardCore.Modules/OrchardCore.Apis.Liquid/` | Liquid 模板 API |
| `Templates` | `src/OrchardCore.Modules/OrchardCore.Templates/` | 动态模板服务 |

## 7. 传统模式 vs Headless 模式对比

| 特性 | 传统 Web 模式 | Headless 模式 |
|------|--------------|---------------|
| 数据获取 | Shape → 模板 → HTML | Shape → 数据 |
| 视图文件 | 必需 | 可选（仅编辑器需要） |
| 输出格式 | HTML | JSON/GraphQL |
| 前端技术 | Razor/Vue | 任意框架 (React, Vue, Angular, Flutter 等) |
| 灵活性 | 受限于模板 | 完全自定义 |
| 缓存策略 | 输出缓存 | 数据/响应缓存 |
| SEO | 服务端渲染，天然支持 | 需要额外处理 (SSG/ISR) |
| 开发复杂度 | 较低 | 较高 |

## 8. 架构优势

1. **一套代码，多种用途**
   - 无需维护两套代码（传统 CMS + Headless CMS）
   - 可以根据需求灵活切换模式

2. **渐进式采用**
   - 可以逐步从传统模式迁移到 Headless
   - 支持混合模式（部分页面用传统，部分用 Headless）

3. **视图可选**
   - API 和管理界面可以共享同样的数据模型
   - 减少重复代码

4. **类型安全**
   - GraphQL Schema 自动生成
   - 保持与数据模型同步

5. **扩展性强**
   - 通过 `IShapeBindingResolver` 可以自定义渲染逻辑
   - 支持多种输出格式

## 9. 最佳实践

### 9.1 创建 Headless 友好的模块

```csharp
public sealed class YourPartDisplayDriver : ContentPartDisplayDriver<YourPart>
{
    public override Task<IDisplayResult> DisplayAsync(YourPart part, BuildPartDisplayContext context)
    {
        // 为 Headless 模式提供结构化数据
        return CombineAsync(
            Initialize<YourPartViewModel>("YourPart", m => {
                m.Data = part.YourData;  // 确保 ViewModel 包含所需的数据
                m.YourPart = part;
            })
            .Location(OrchardCoreConstants.DisplayType.Detail, "Content")
        );
    }
}
```

### 9.2 使用 GraphQL 暴露数据

```csharp
public class YourPartGraphType : ObjectGraphType<YourPart>
{
    public YourPartGraphType()
    {
        Name = "YourPart";

        Field(x => x.YourData).Description("Your data field");
        Field(x => x.CreatedUtc).Description("Creation date");
        // 添加需要的字段
    }
}
```

### 9.3 动态模板实现

```csharp
public class CustomShapeBindingResolver : IShapeBindingResolver
{
    private readonly IYourTemplateProvider _templateProvider;

    public CustomShapeBindingResolver(IYourTemplateProvider templateProvider)
    {
        _templateProvider = templateProvider;
    }

    public async Task<ShapeBinding> GetShapeBindingAsync(string shapeType)
    {
        var template = await _templateProvider.GetTemplateAsync(shapeType);

        if (template == null)
        {
            return null;  // 回退到默认行为
        }

        return new ShapeBinding()
        {
            BindingName = shapeType,
            BindingAsync = context =>
            {
                // 自定义渲染逻辑
                var viewModel = context.Value as YourViewModel;
                return RenderTemplate(template, viewModel);
            }
        };
    }
}
```

## 10. 参考资料

- **核心文档**: https://docs.orchardcore.net/
- **Display Management**: `src/OrchardCore/OrchardCore.DisplayManagement/`
- **Content Management**: `src/OrchardCore/OrchardCore.ContentManagement/`
- **API 模块**: `src/OrchardCore.Modules/OrchardCore.Apis.GraphQL/`
- **Templates 模块**: `src/OrchardCore.Modules/OrchardCore.Templates/`

## 11. 相关代码位置

| 文件 | 路径 |
|------|------|
| DefaultHtmlDisplay | `src/OrchardCore/OrchardCore.DisplayManagement/Implementation/DefaultHtmlDisplay.cs` |
| ShapeTemplateBindingStrategy | `src/OrchardCore/OrchardCore.DisplayManagement/Descriptors/ShapeTemplateStrategy/ShapeTemplateBindingStrategy.cs` |
| IShapeBindingResolver | `src/OrchardCore/OrchardCore.DisplayManagement/IShapeBindingResolver.cs` |
| ContentApiController (示例) | `src/OrchardCore.Modules/OrchardCore.Demo/Controllers/ContentApiController.cs` |
| TemplatesShapeBindingResolver | `src/OrchardCore.Modules/OrchardCore.Templates/Services/TemplatesShapeBindingResolver.cs` |
| LiquidPartDisplayDriver | `src/OrchardCore.Modules/OrchardCore.Liquid/Drivers/LiquidPartDisplayDriver.cs` |
| ContentsDriver | `src/OrchardCore.Modules/OrchardCore.Contents/Drivers/ContentsDriver.cs` |
| SchemaService (GraphQL) | `src/OrchardCore.Modules/OrchardCore.Apis.GraphQL/Services/SchemaService.cs` |
