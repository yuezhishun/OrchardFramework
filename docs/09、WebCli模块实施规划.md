# WebCli 模块实施规划（基于 `/home/yueyuan/WebCLI` MVP）

更新时间：2026-02-18  
适用仓库：`/home/yueyuan/OrchardFramework`

## 1. 目标与边界

### 1.1 目标
- 在 OrchardFramework 中新增模块：`OrchardFramework.Modules.WebCli`（Feature Id：`OrchardFramework.WebCli`）。
- MVP 先复用 `/home/yueyuan/WebCLI` 已验证能力：
  - 终端实例创建/连接/终止
  - WebSocket 实时交互
  - snapshot + patch + history
  - 文件浏览与文本预览
  - 移动端软键盘操作
- 接入当前 SaaS 管理台（`frontend`）并保持现有 `/api/management/*` 风格。

### 1.2 非目标（本期不做）
- 不在第一期实现 Master-Slave 分布式调度。
- 不在第一期重写 node-pty/xterm-headless 为 .NET 原生实现。
- 不做跨节点会话迁移和长周期审计检索。

## 2. 实施策略（推荐）

采用“两阶段架构”，先上线可用版本，再逐步收敛：

### 阶段 A（推荐先落地）
- Orchard `WebCli` 模块负责：权限、租户策略、BFF 接口、配置、审计、UI 入口。
- 终端执行引擎继续使用 `/home/yueyuan/WebCLI` 的 Node 服务（Sidecar）。
- 模块通过内部 HTTP/WS 与 Sidecar 通信，向前端统一暴露 `/api/management/webcli/*`。

### 阶段 B（演进）
- 将 Sidecar 能力逐步模块内化（或替换为独立 WebCli 服务集群）。
- 引入 control 协议和 Master-Slave 架构，对接 SaaS/OpenId 统一鉴权。

原因：当前 MVP 已有完整链路和测试，先复用可显著降低上线时间与风险。

## 3. 模块设计

### 3.1 后端项目与 Feature
- 新建：`src/OrchardFramework.Modules.WebCli/`
- 最小文件：
  - `Manifest.cs`
  - `Startup.cs`
  - `Permissions/WebCliPermissions.cs`
  - `Migrations/WebCliMigrations.cs`
  - `Endpoints/WebCliManagementEndpoints.cs`
  - `Services/*`（策略、代理、审计）
- `src/OrchardFramework.Api/OrchardFramework.Api.csproj` 添加 `ProjectReference`。
- `OrchardFramework.slnx` 添加项目。
- 在 `SaaS.Base.recipe.json` 中启用 `OrchardFramework.WebCli`。

### 3.2 配置键（appsettings）
建议新增：
- `WebCli:Enabled`
- `WebCli:SidecarBaseUrl`（如 `http://127.0.0.1:8080`）
- `WebCli:AllowAnonymous`（开发期可继承 `SaaS:AllowAnonymousManagementApi`）
- `WebCli:TenantWorkspaceRoot`（默认 `/home/yueyuan`）
- `WebCli:HistoryLimit`
- `WebCli:DefaultShell`
- `WebCli:MaxInstancesPerTenant`
- `WebCli:MaxInstancesPerUser`
- `WebCli:AllowedCommands` / `WebCli:BlockedCommands`

### 3.3 权限模型
建议至少三个权限：
- `UseWebCliSession`：创建/连接/输入/resize/拉历史
- `ManageWebCliPolicies`：模板、白名单、限额、路径策略
- `ViewWebCliAudit`：查看操作日志

默认将上述权限授予 `Administrator`。

## 4. API 规划（统一管理面）

统一前缀：`/api/management/webcli`

- `GET /health`：模块与 sidecar 健康
- `GET /instances?tenant=`：列实例
- `POST /instances`：创建实例（command/args/cwd/env/cols/rows）
- `DELETE /instances/{id}`：终止实例
- `GET /projects`：项目目录列表（受租户根目录约束）
- `GET /files/list`：文件列表（防越权）
- `GET /files/read`：文本预览（大小/行数限制）
- `GET /ws-url?instanceId=`：返回前端连接地址（由模块签发短期 token）
- `GET /policies`、`PUT /policies`：命令模板、限制、配额

WebSocket：
- `GET /api/webcli/ws/term`（模块做 WS 鉴权与转发）

## 5. 多租户与安全策略

### 5.1 强制约束
- 每个实例必须绑定 `{tenant, user}`。
- `cwd` 只能在租户允许目录下（禁止越界、禁止符号链接逃逸）。
- 命令执行遵循租户命令策略（白名单优先）。
- 默认禁止危险命令模板（如直接 rm/rsync 全盘）。

### 5.2 鉴权演进
- 开发阶段：可允许匿名（与现有项目保持一致）。
- 生产阶段：必须启用 Orchard/OpenId 鉴权，并关闭匿名入口。
- WS 连接使用短时 token（30-120 秒），避免复用长期凭证。

### 5.3 审计
记录：
- 创建/连接/终止实例
- 执行命令模板 ID（不记录敏感 env 值）
- 文件浏览路径
- 失败原因与来源用户

## 6. 前端规划（`frontend`）

### 6.1 路由与页面
- 新增路由：
  - `/webcli`（桌面工作台）
  - `/webcli/mobile`（移动优化版，可后置）
- 在 `AppShell.vue` 增加菜单：`WebCli`。

### 6.2 复用策略
从 `/home/yueyuan/WebCLI/frontend-vue` 迁移：
- 终端核心交互（xterm、snapshot/patch/history/resync）
- 实例列表与连接流程
- 移动端软键盘（优先 mobile-v2）
- 文件浏览弹窗

只做必要改造：
- 接口基址改为 `/api/management/webcli/*`
- 统一错误提示、加载态、权限态
- 去除调试暴露对象（`__WEBCLI_DEBUG__`）

## 7. 测试与验收规划

### 7.1 后端测试
新增：`tests/OrchardFramework.Modules.WebCli.Tests/`

至少覆盖：
- 权限：未授权/有权访问差异
- 租户隔离：tenant-a 无法访问 tenant-b 实例
- 路径安全：`..`、符号链接、二进制文件预览拦截
- 实例生命周期：create/list/terminate
- WS 基础：连接、resync、exit

### 7.2 前端测试
- 单测：协议处理（snapshot/patch/history）与状态机。
- E2E：创建实例 -> 执行命令 -> 重连 -> resize -> 终止。
- 移动端：软键盘关键组合键（Ctrl+C、方向键、Tab）。

### 7.3 质量门禁
- `dotnet build` / `dotnet test`
- `npm --prefix frontend run build`
- 至少 1 条 WebCli 全链路 Playwright 通过
- 新增接口与配置完成文档更新

## 8. 分期里程碑

### M1（模块骨架 + 代理可用）
- 新模块、权限、配置、基础 API、sidecar health 联通
- 前端新增 WebCli 页面壳与菜单

### M2（核心终端闭环）
- create/list/terminate + WS 交互 + snapshot/patch/resync
- 基础权限和租户隔离生效

### M3（文件能力 + 策略中心）
- projects/files/list/read
- 命令模板与白名单、配额策略
- 审计日志落库

### M4（稳定性与发布）
- 压测与背压策略验证
- 生产配置切换（关闭匿名、启用 token）
- 回归测试与部署脚本联调

## 9. 风险与缓解

- 风险：Node sidecar 与 .NET 模块双栈维护成本高  
  缓解：明确协议边界与迁移计划，优先稳定接口不稳定实现。

- 风险：WebSocket 代理与反向代理配置复杂  
  缓解：先在开发环境跑通 `/api/webcli/ws/term`，再固化 Nginx 配置模板。

- 风险：命令执行安全边界不足  
  缓解：一期即引入路径约束、命令白名单、实例限额与审计。

- 风险：移动端体验分叉  
  缓解：桌面优先，移动端按 `mobile-v2` 收敛一套实现。

## 10. 立即执行清单（下一步）

1. 创建 `OrchardFramework.Modules.WebCli` 模块骨架并接入解决方案。  
2. 增加 `WebCli` 配置节与权限定义。  
3. 实现 `/api/management/webcli/health` + `/instances`（先代理 sidecar）。  
4. 在 `frontend` 增加 `/webcli` 路由与基础终端页面。  
5. 新增后端集成测试与最小 E2E，建立回归门禁。
