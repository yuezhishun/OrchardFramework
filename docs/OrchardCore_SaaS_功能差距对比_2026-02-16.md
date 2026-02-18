# OrchardFramework vs OrchardCoreCMS 功能差距对比（2026-02-16）

## 1. 对比基线

- 当前项目：`/home/yueyuan/OrchardFramework`
- 参考项目：`/home/yueyuan/OrcharCoreCms`
- 线上参考地址：`https://pty.addai.vip/cms`（当前可访问到登录页）

### 1.1 参考项目关键证据

- OrchardCore 主机方式：`/home/yueyuan/OrcharCoreCms/Program.cs:17` 到 `/home/yueyuan/OrcharCoreCms/Program.cs:43`
  - `AddOrchardCms()` + `UseOrchardCore()`
- CMS Target 包：`/home/yueyuan/OrcharCoreCms/cms-test.csproj:26`
  - `OrchardCore.Application.Cms.Targets`

### 1.2 参考项目当前租户已启用能力（来自 `OrchardCore.db` 的 ShellDescriptor）

已启用/安装包含：

- SaaS 基础相关：`OrchardCore.Tenants`、`OrchardCore.Tenants.FeatureProfiles`、`OrchardCore.Users`、`OrchardCore.Roles`、`OrchardCore.Settings`、`OrchardCore.Localization`、`OrchardCore.Security`、`OrchardCore.Features`、`OrchardCore.Recipes`、`OrchardCore.Apis.GraphQL`、`OrchardCore.OpenId.*`
- CMS 相关（当前也启用）：`OrchardCore.Contents`、`OrchardCore.ContentTypes`、`OrchardCore.ContentFields`、`OrchardCore.Widgets`、`OrchardCore.Flows`、`OrchardCore.Media`、`OrchardCore.Menu`、`OrchardCore.Navigation`、`OrchardCore.Markdown` 等

## 2. 当前项目实现现状

### 2.1 后端

- 当前是自建 Minimal API + EF Core + JWT，不是 OrchardCore 运行时：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Program.cs:37` 到 `/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Program.cs:85`
- 虽然引用了部分 OrchardCore 包：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/OrchardFramework.Api.csproj:14` 到 `:24`
  - 但没有 `AddOrchardCms()` / `UseOrchardCore()`
- 数据模型是自定义关系模型：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Models/Entities.cs:3` 到 `:105`
- 已实现 API：`auth/tenants/users/roles/permissions/templates`

### 2.2 前端

- 当前菜单仅 6 个：`总览/租户/用户/角色/权限/终端模板`
  - 路由：`/home/yueyuan/OrchardFramework/frontend/src/router/index.ts:19` 到 `:43`
  - 菜单：`/home/yueyuan/OrchardFramework/frontend/src/components/AppShell.vue:11` 到 `:18`
- 没有 GraphiQL、功能开关、本地化、站点设置、OpenId 应用管理等页面

## 3. 目标能力对照（你提出的“去 CMS 保 SaaS”）

结论标记：

- `已实现`：基本可用
- `部分实现`：有雏形但与 OrchardCore 能力不等价
- `未实现`：缺少后端与前端

### 3.1 能力对照表

1. 租户管理
- 当前：`已实现`
- 证据：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Endpoints/TenantEndpoints.cs:12`
- 差距：未使用 Orchard `ShellSettings`/请求域名或路径映射机制，仍是业务表 CRUD。

2. 租户配置隔离
- 当前：`部分实现`
- 证据：通过 `tenant_id` Claim 与查询过滤（如 `/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Endpoints/UserEndpoints.cs`）
- 差距：缺少 Orchard Shell 级隔离（配置、中间件、特性集、每租户模块状态）。

3. 功能动态开启和关闭（Feature Toggle）
- 当前：`未实现`
- 证据：代码中无 Feature/Profile 管理接口与页面；仅有租户启停和模板启停。
- 参考：官方启用了 `OrchardCore.Features` 与 `OrchardCore.Tenants.FeatureProfiles`。

4. 本地化多语言
- 当前：`未实现`
- 证据：无 Localization API/UI、无文化切换。
- 参考：官方启用了 `OrchardCore.Localization` 和 `OrchardCore.Users.Localization`。

5. 站点配置（Site Settings）
- 当前：`未实现`
- 证据：无站点配置接口与页面。
- 参考：官方启用 `OrchardCore.Settings`，并在站点文档里有 `SiteName/TimeZone/...`。

6. GraphiQL / GraphQL
- 当前：`未实现`
- 证据：路由与 API 中无 GraphQL/GraphiQL。
- 参考：官方启用了 `OrchardCore.Apis.GraphQL`。

7. SaaS 配方（Recipes）
- 当前：`未实现`
- 证据：无 Recipe 执行/导入导出的 API 与 UI。
- 参考：官方启用 `OrchardCore.Recipes`。

8. 安全（Security 模块化策略）
- 当前：`部分实现`
- 证据：JWT + 自定义权限检查（`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Endpoints/EndpointSecurityExtensions.cs:18`）
- 差距：缺少 Orchard 安全策略生态（例如安全策略设置、统一授权策略、模块权限体系）。

9. OpenId 连接（第三方登录 + 对外授权）
- 当前：`未实现`
- 证据：仅在 csproj 引用 `OrchardCore.OpenId`，无对应 endpoint/service/UI。
  - 引用位置：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/OrchardFramework.Api.csproj:19`
- 参考：官方启用了 `OrchardCore.OpenId.Management/Server/Validation`，数据库存在 OpenId Scope 索引表。

10. 用户、角色、权限
- 当前：`已实现（基础版）`
- 证据：
  - 用户：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Endpoints/UserEndpoints.cs:13`
  - 角色：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Endpoints/RoleEndpoints.cs:12`
  - 权限：`/home/yueyuan/OrchardFramework/src/OrchardFramework.Api/Endpoints/PermissionEndpoints.cs:12`
- 差距：与 Orchard 文档模型（Document + Claims + LoginInfos）不一致，且缺少外部登录关联流程。

## 4. 结构性偏差（实体层）

1. 实体存储模型偏差
- 当前项目使用关系实体（`Tenant/User/Role/Permission/...`）
- 官方 Orchard 在已安装站点中核心对象大量使用 Document（JSON）和索引投影
- 影响：后续无法平滑复用 Orchard 模块行为，升级与兼容风险高

2. 运行时偏差
- 当前项目不是 Orchard Shell 运行时，不具备“每租户特性开关 + 每租户配置管线”能力
- 影响：你要求的“去 CMS 保 SaaS 核心能力”无法靠现有架构小改达成

3. 前端能力面偏差
- 当前前端仅对接自定义 REST
- 未接 Orchard Admin 的 SaaS 能力（Features/Profile/Recipes/OpenId/Localization/Settings）

## 5. 审查结论

- 你的判断是准确的：当前项目与官方实体和能力模型差异很大。
- 当前实现更像“自研 SaaS 管理后台 MVP”，不是“OrchardCore CMS 去 CMS 后的 SaaS 核心发行形态”。
- 若目标是你描述的默认能力集合，建议以 OrchardCore 主机为核心重构，而不是继续在现有自定义模型上补功能。

## 6. 建议的收敛方向（简版）

1. 先把后端切回 OrchardCore Host（`AddOrchardCms`/`UseOrchardCore`），再做“减法”去 CMS 特性。
2. 通过默认 Recipe 仅启用 SaaS 必需模块：Tenants、FeatureProfiles、Users、Roles、Settings、Localization、Security、OpenId、GraphQL、Recipes。
3. 明确禁用 CMS 内容模块（Contents/ContentTypes/Widgets/Flows/Media/Menu/Navigation/Markdown 等）。
4. 前端改为“SaaS 控制台”对接 Orchard 的管理 API（而非自定义实体 CRUD）。
