# OrchardCore 开发规范（租户 / 模块 / 功能 / GraphQL）

更新时间：2026-02-18  
适用仓库：`/home/yueyuan/OrchardFramework`

## 1. 基线与适用范围

本规范用于当前 `OrchardFramework` 项目，优先遵循 OrchardCore 官方约定，再叠加本项目 Headless 管理约定。

- 后端：`.NET 10` + `OrchardCore 2.2.1`
- 默认配方：`SaaS.Base`
- 管理接口分组：`/api/management/*`
- 能力探测接口：`/api/saas/*`
- GraphQL 端点：`/api/graphql`
- 多租户管理：`OrchardCore.Tenants` + 适配层接口
- 当前默认配置：`SaaS:DisableAdminPathAccess=true`、`SaaS:AllowAnonymousManagementApi=true`

说明：
- 若配置了 `ASPNETCORE_PATHBASE`，实际路径为 `<PathBase>/api/...`。
- 开发环境允许匿名管理接口仅用于联调，生产必须关闭匿名访问。

## 2. 总体开发流程

所有需求按以下流程执行，避免“只改代码不改配置/权限/测试”。

1. 明确需求归类  
- 租户管理变更  
- 模块能力变更  
- 管理 API 变更  
- GraphQL 查询能力变更

2. 设计变更边界  
- 是否需要新增模块（推荐小模块）  
- 是否仅需更新 Recipe / Feature Profile  
- 是否涉及跨租户访问（必须经过租户上下文切换）

3. 实施代码与配置  
- `src/` 代码  
- `Recipes/*.recipe.json` 配方  
- `appsettings*.json` 配置键  
- `docs/` 同步文档

4. 验证  
- `dotnet build`  
- `dotnet test`  
- 关键 API 回归（租户、功能、权限、GraphQL）

## 3. 租户开发规范

### 3.1 命名与路由规则

- 租户名：小写英文、数字、短横线，示例 `tenant-a`
- `requestUrlPrefix`：与租户名保持一致，避免混淆
- 默认租户 `Default` 不可删除

### 3.2 租户生命周期

状态与可执行操作：
- `Uninitialized`：可编辑、可删除、可 setup，不可直接启用
- `Running`：可编辑、可禁用，不可删除
- `Disabled`：可编辑、可启用、可删除

### 3.3 标准接口（本项目）

推荐优先用适配层：
- `GET /api/management/tenants`
- `POST /api/management/tenants`
- `PATCH /api/management/tenants/{tenantName}`

创建租户示例：

```bash
curl -X POST http://localhost:5019/api/management/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "name": "tenant-a",
    "requestUrlPrefix": "tenant-a",
    "category": "SaaS",
    "description": "租户A",
    "databaseProvider": "Sqlite",
    "recipeName": "SaaS.Base",
    "featureProfiles": ["SaaSNoCms"]
  }'
```

启用/禁用/删除示例：

```bash
# 禁用
curl -X PATCH http://localhost:5019/api/management/tenants/tenant-a \
  -H "Content-Type: application/json" \
  -d '{"enabled": false}'

# 启用（仅 Disabled 可启用）
curl -X PATCH http://localhost:5019/api/management/tenants/tenant-a \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'

# 删除（仅 Disabled 或 Uninitialized）
curl -X PATCH http://localhost:5019/api/management/tenants/tenant-a \
  -H "Content-Type: application/json" \
  -d '{"operation": "remove"}'
```

### 3.4 租户初始化（Setup）

`Uninitialized` 租户必须先 setup。可使用 Orchard 内置接口：
- `POST /api/tenants/setup`

最小请求体示例：

```json
{
  "name": "tenant-a",
  "siteName": "Tenant A",
  "databaseProvider": "Sqlite",
  "connectionString": "",
  "tablePrefix": "",
  "schema": "",
  "userName": "admin",
  "email": "admin@tenant-a.local",
  "password": "Admin123!",
  "recipeName": "SaaS.Base",
  "siteTimeZone": "Asia/Shanghai"
}
```

### 3.5 安全要求

- 生产环境设置 `SaaS:AllowAnonymousManagementApi=false`
- 管理接口必须要求权限（至少租户管理权限）
- 不允许跨租户直接访问数据，必须经过租户作用域执行

## 4. 新模块开发规范

### 4.1 何时新建模块

满足任一条件即新建模块：
- 有独立业务边界（如 Billing、Audit、WorkflowAdapter）
- 需要独立 Feature 开关
- 需要独立权限集合
- 需要独立迁移或定时任务

### 4.2 项目结构建议

- 模块项目：`src/OrchardFramework.Modules.<ModuleName>/`
- 测试项目：`tests/OrchardFramework.Modules.<ModuleName>.Tests/`

创建与引用（示例）：

```bash
dotnet new ocmodulemvc -n OrchardFramework.Modules.Billing -o src/OrchardFramework.Modules.Billing
dotnet add src/OrchardFramework.Api/OrchardFramework.Api.csproj reference src/OrchardFramework.Modules.Billing/OrchardFramework.Modules.Billing.csproj
```

### 4.3 模块最小骨架

`Manifest.cs`：

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SaaS Billing",
    Author = "OrchardFramework",
    Version = "1.0.0",
    Description = "Billing module for SaaS tenants"
)]

[assembly: Feature(
    Id = "OrchardFramework.SaaS.Billing",
    Name = "SaaS Billing",
    Category = "SaaS",
    Description = "Billing capability for tenant management"
)]
```

`Startup.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace OrchardFramework.Modules.Billing;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册服务、权限、迁移、后台任务等
    }
}
```

### 4.4 强制约束

- 模块必须提供独立 Feature Id
- 涉及数据表变更必须使用 `DataMigration`
- 涉及权限必须实现 `IPermissionProvider`
- 路由不与现有 `/api/management/*` 冲突

## 5. 新功能开发规范

### 5.1 功能类型划分

- 配置型：只改配方或配置（Feature 开关、默认值）
- 接口型：新增或扩展 `/api/management/*`
- 领域型：新增模块 + 接口 + 权限 + 数据迁移

### 5.2 实施清单

1. 定义 Feature 与权限  
2. 补齐配置项与默认值  
3. 开发 API 或 GraphQL  
4. 补齐集成测试（租户隔离、权限、异常分支）  
5. 更新文档与回归脚本

### 5.3 接口规范（Management API）

- 路由前缀：`/api/management`
- 租户参数：统一使用 `tenant`（query/body）
- 鉴权：先做管理访问检查，再执行业务
- 状态码：
  - `200` 查询/更新成功
  - `201` 创建成功
  - `400` 参数或状态非法
  - `404` 资源不存在
  - `409` 资源冲突

返回体建议：

```json
{
  "message": "错误说明",
  "unknown": [],
  "details": {}
}
```

## 6. GraphQL 开发规范

### 6.1 使用场景

优先 GraphQL 的场景：
- 前端一次请求需要多实体聚合
- 字段裁剪需求强（减少 over-fetching）
- 查询模型稳定、变更频率低

优先 REST（Management API）的场景：
- 命令式操作（创建、更新、启停、删除）
- 强事务流程
- 审计要求强的后台操作

### 6.2 启用要求

- 必选 Feature：`OrchardCore.Apis.GraphQL`
- 若要查询内容项：还需启用 `OrchardCore.Contents`（其 GraphQL Startup 会注册内容查询能力）
- 权限要求：
  - 查询：`ExecuteGraphQL`
  - 变更：`ExecuteGraphQLMutations`

### 6.3 请求方式

GET 示例：

```bash
curl "http://localhost:5019/api/graphql?query={__schema{queryType{name}}}"
```

POST 示例：

```bash
curl -X POST http://localhost:5019/api/graphql \
  -H "Content-Type: application/json" \
  -d '{
    "query":"query($n:Int){ blogPost(first:$n){ displayText contentItemId } }",
    "variables":{"n":5}
  }'
```

### 6.4 在模块中扩展 GraphQL Schema

`GraphQLStartup.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Apis.GraphQL;
using OrchardCore.Modules;

namespace OrchardFramework.Modules.Billing.GraphQL;

[RequireFeatures("OrchardCore.Apis.GraphQL")]
public sealed class GraphQLStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, BillingQuery>();
    }
}
```

`BillingQuery.cs`：

```csharp
using GraphQL.Resolvers;
using GraphQL.Types;
using OrchardCore.Apis.GraphQL;

namespace OrchardFramework.Modules.Billing.GraphQL;

public sealed class BillingQuery : ISchemaBuilder
{
    public Task BuildAsync(ISchema schema)
    {
        schema.Query.AddField(new FieldType
        {
            Name = "billingHealth",
            Type = typeof(StringGraphType),
            Resolver = new FuncFieldResolver<string>(_ => "ok")
        });

        return Task.CompletedTask;
    }

    public Task<string> GetIdentifierAsync() => Task.FromResult("billing-query-v1");
}
```

### 6.5 GraphQL 安全与性能

建议在配置中限制深度与复杂度：

```json
{
  "OrchardCore": {
    "OrchardCore_Apis_GraphQL": {
      "MaxDepth": 20,
      "MaxComplexity": 100,
      "FieldImpact": 2.0,
      "DefaultNumberOfResults": 100,
      "MaxNumberOfResults": 1000
    }
  }
}
```

生产要求：
- 关闭异常堆栈外露
- 仅开放必要角色的 GraphQL 权限
- 对高频查询增加缓存或分页限制

## 7. 验收与发布规范

每次变更至少通过：

1. 构建与测试  
- `dotnet restore`  
- `dotnet build`  
- `dotnet test`

2. 接口回归  
- 租户创建/启停/删除  
- Feature 启停  
- 新增管理接口  
- GraphQL 查询（权限正确、分页生效）

3. 文档回归  
- 变更点同步到 `docs/`  
- 新配置键与迁移步骤写入同一 PR

## 8. 本项目强制约束（落地版）

- 新管理能力默认放在 `/api/management/*`
- 所有跨租户操作必须经过租户作用域执行
- 生产环境禁止匿名管理 API
- 优先小模块 + 独立 Feature，而非在单文件堆叠逻辑
- 若本规范与 OrchardCore 官方约定冲突，以官方约定为准

