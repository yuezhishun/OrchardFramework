# SaaS Headless + Vue 管理台实施方案（复用 OrchardCore 模块）

更新时间：2026-02-17
适用仓库：`/home/yueyuan/OrchardFramework`

## 1. 决策结论（对应 `05、Headless模式实现机制.md`）

目标是“管理功能全部使用 Vue 页面”，且“不重写所有后端逻辑”。

采用 **混合实现**：
- 主路线：**方式 2（GraphQL）** 作为读取能力主通道（站点配置、可查询实体、仪表盘统计）。
- 管理动作：**方式 1（REST）但仅做薄适配 BFF**，直接调用 OrchardCore 现有服务/文档模型完成写操作（租户、功能开关、用户、角色、OpenId）。
- 不采用方式 3（`IShapeBindingResolver`）作为本项目核心路径：它解决的是“模板渲染绑定”，不是“管理 API 面向 Vue”的核心矛盾。

## 2. 为什么这样选

1. `GraphQL` 适合“查询聚合”，能减少 Vue 页面请求数。
2. 已有可复用 API/服务能力并不对称：
   - `OrchardCore.Tenants` 已提供 `TenantApiController`（`/api/tenants/*`）可直接复用。
   - `Features/Users/Roles` 主要是 Admin Controller + 内部服务（如 `IShellFeaturesManager`、`IUserService`、`IRoleService`），需要薄适配给 Vue。
   - 因此单靠 GraphQL 不够，需要少量 BFF 补齐管理动作。
3. 薄适配 BFF 可以复用 OrchardCore 模块能力，避免重新设计实体与权限模型。
4. 后端只负责“授权 + 参数校验 + 调用 Orchard 服务”，不重复发明租户/权限/特性逻辑。

## 2.1 源码证据（`/home/yueyuan/OrchardCore`）

- 多租户 API：
  - `src/OrchardCore.Modules/OrchardCore.Tenants/Controllers/TenantApiController.cs`
  - 路由：`[Route("api/tenants")]`
- 功能管理服务接口：
  - `src/OrchardCore/OrchardCore.Abstractions/Shell/IShellFeaturesManager.cs`
- 用户/角色服务接口：
  - `src/OrchardCore/OrchardCore.Users.Abstractions/Services/IUserService.cs`
  - `src/OrchardCore/OrchardCore.Roles.Abstractions/IRoleService.cs`
- GraphQL 与 OpenId 文档：
  - `src/docs/reference/modules/Apis.GraphQL/README.md`
  - `src/docs/reference/modules/OpenId/README.md`

## 3. 目标架构

- 前端：`/saas`（Vue 管理台，唯一业务入口）
- 后端：
  - `GraphQL` 查询：`/saas/api/graphql`（或等效路径）
  - BFF 管理 API：`/saas/api/management/*`
- 后台：`/saas-admin` 仅作为运维兜底入口，不再作为业务验收主路径。

## 4. 模块复用策略

优先复用并保持启用：
- `OrchardCore.Tenants`
- `OrchardCore.Tenants.FeatureProfiles`
- `OrchardCore.Features`
- `OrchardCore.Users`
- `OrchardCore.Roles`
- `OrchardCore.Settings`
- `OrchardCore.Recipes`
- `OrchardCore.OpenId.*`
- `OrchardCore.Apis.GraphQL`

保持禁用（SaaS 非必要 CMS 能力）：
- `OrchardCore.Contents`、`OrchardCore.ContentTypes`、`OrchardCore.Widgets` 等内容模块

## 5. API 分层约束（避免“重写后端”）

BFF 层只允许做三件事：
1. 参数/权限校验（复用 Orchard 权限）
2. 调用 Orchard 服务并返回 DTO
3. 聚合少量跨模块查询

禁止：
- 新建自定义业务实体替代 Orchard 实体
- 重做账号体系/JWT 体系绕开 Orchard
- 在 BFF 写一套独立的租户/角色/权限规则

## 6. 迭代落地

### Iteration H1：Headless 基线
- 启用 GraphQL + OpenId 必需 Feature。
- 增加 `/api/saas/capabilities`，输出当前可用管理能力矩阵。
- Vue 增加“能力探测 + API 健康”页面。

### Iteration H2：租户与功能管理（Vue 首批替换）
- 租户优先复用官方接口：`/api/tenants/create|edit|enable|disable|remove|setup`
- 对前端统一提供适配层：`/api/management/tenants/*`（可转发/封装官方接口）
- `GET/PUT /api/management/features`
- `GET/PUT /api/management/feature-profiles`
- Vue 用这些接口替换 `/saas-admin/Admin/Tenants|Features` 入口。

### Iteration H3：用户与角色管理
- `GET/POST/PATCH /api/management/users`
- `GET/POST/PATCH /api/management/roles`
- `PUT /api/management/roles/{id}/permissions`

### Iteration H4：站点设置、本地化、OpenId 应用管理
- 设置与本地化查询/更新接口
- OpenId 应用与 Scope 管理接口

## 7. 验收标准更新

后续迭代以以下标准替代“后台 URL 可访问”：
1. Vue 页面可完成增删改查与状态切换。
2. `/saas-admin/*` 不作为主验收项（仅兜底）。
3. 所有管理动作均通过 Orchard 权限校验。
4. 集成测试覆盖“租户隔离 + 权限拒绝 + 关键边界条件”。

## 8. 初始执行建议（H1，部分完成）

H1 执行状态：
1. 配方增加 `OrchardCore.Apis.GraphQL` 与 `OrchardCore.OpenId.*`：`待完成`
2. `SaasInspectionEndpoints` 增加 capabilities 与 headless 链接：`已完成`
3. 前端路由新增 headless 能力页：`已完成`
4. 调整 Playwright 与测试，不再以 `/saas-admin` 页面作为通过条件：`已完成`

## 9. 本次会话落地记录（2026-02-17）

### 9.1 已完成改造

1. 后端巡检接口：
   - 新增 `GET /api/saas/capabilities`
   - `GET /api/saas/links` 增加 headless 能力相关链接
2. 前端页面：
   - 新增 `/saas/capabilities` 页面
   - 菜单和路由已接入
3. 自动化验收：
   - `scripts/playwright_saas_admin_check.mjs` 从“后台管理页巡检”切换为“Vue 页面 + Headless API 验收”
   - 验收项包含：
     - Vue 路由：`/`、`/features`、`/capabilities`、`/admin-links`、`/graphiql`
     - API：`/api/saas/summary|features|capabilities|links`
     - 内置租户 API 可达性：`POST /api/tenants/create`（接受 400/401/403）

### 9.2 验证与发布

- 本地验证：
  - `env -u version dotnet test OrchardFramework.slnx -c Debug --nologo` 通过
  - `npm --prefix frontend run build` 通过
- 线上部署：
  - 命令：`./scripts/deploy_saas.sh`
  - 结果：成功（03:29:18）
  - 备份目录：
    - `/www/backup/saas-api-20260217032843`
    - `/www/backup/saas-frontend-20260217032843`
- 部署后验收：
  - 命令：`node scripts/playwright_saas_admin_check.mjs`
  - 结果：`passed=10, failed=0`
  - 报告：`artifacts/saas-headless-check-2026-02-16T19-29-27-962Z/summary.json`
