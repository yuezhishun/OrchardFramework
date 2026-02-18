# OrchardFramework SaaS MVP

Backend + Frontend MVP for the SaaS platform described in `SaaS平台功能需求文档（SQLite 版本）.md`, following PDCA with tests as Check.

## Tech Stack
- Backend: OrchardCore host (.NET 10), SQLite
- Frontend: Vue 3 + Vite + TypeScript + Pinia + Vue Router
- Orchard dependencies: OrchardCore package set (2.2.1) prioritized for module evolution

## Current Status
- Iteration 0 completed:
  - Baseline recipe `SaaS.Iteration0` from Orchard MVC target stack (minimal setup + settings).
- Iteration 1 completed and set as default deploy recipe (`SaaS.Base`):
  - Tenant management (`/Admin/Tenants`)
  - Tenant feature profile management (`/Admin/TenantFeatureProfiles/Index`)
  - Feature/module management (`/Admin/Features`)
  - CMS features disabled by recipe
- Headless strategy activated (2026-02-17):
  - Primary management UI is Vue (`/saas`).
  - Orchard admin path (`/saas-admin`) is temporarily disabled in this stage.
  - Added capabilities inspection API: `/saas/api/saas/capabilities`.
  - Added Vue page: `/saas/capabilities`.
  - Iteration H2 delivered:
    - Management BFF APIs:
      - `/saas/api/management/tenants`
      - `/saas/api/management/features`
      - `/saas/api/management/feature-profiles`
    - Vue pages:
      - `/saas/tenants`
      - `/saas/features`
      - `/saas/feature-profiles`
    - `SaaS:AllowAnonymousManagementApi=true` by default for dev-stage validation.
  - Iteration H4 delivered:
    - Management BFF APIs:
      - `/saas/api/management/site-settings`
      - `/saas/api/management/localization`
      - `/saas/api/management/openid/applications`
      - `/saas/api/management/openid/scopes`
    - Vue pages:
      - `/saas/site-settings`
      - `/saas/localization`
      - `/saas/openid`
  - Iteration H5 delivered:
    - Management BFF APIs:
      - `/saas/api/management/recipes`
      - `/saas/api/management/recipes/execute`
    - Summary enhancement:
      - `/saas/api/saas/summary` now returns real OpenId counts
    - Vue pages:
      - `/saas/recipes`
  - Updated Playwright script `scripts/playwright_saas_admin_check.mjs` to validate Vue routes + Headless/H2 APIs.
- Latest deployment and acceptance (2026-02-17):
  - Deploy command: `./scripts/deploy_saas.sh`
  - Backups:
    - `/www/backup/saas-api-20260217032843`
    - `/www/backup/saas-frontend-20260217032843`
  - Headless E2E result: `passed=10, failed=0`
- Detailed execution plan:
  - `docs/04、SaaS迭代开发执行计划.md`
  - `docs/06、SaaS_Headless_Vue实施方案.md`

## Run Backend
```bash
env -u version dotnet restore OrchardFramework.slnx
env -u version dotnet run --project src/OrchardFramework.Api
```

Default admin account:
- Username: `admin`
- Password: `Admin123!`

## Run Frontend
```bash
cd frontend
npm install
npm run dev
```

Frontend dev server proxies `/api` to `http://localhost:5019`.

## Check (Tests)
```bash
env -u version dotnet test OrchardFramework.slnx -c Debug
```

Current test scope:
- Recipe-level validation for `SaaS.Iteration0` and `SaaS.Base`
- Admin-path closure checks for `/Admin*` and `/saas-admin*`
- AutoSetup + summary inspection (`/api/saas/summary`)
- H2 management endpoint payload checks (`/api/management/tenants|features|feature-profiles`)
- H5 recipe adapter payload checks (`/api/management/recipes`)
- Headless acceptance script:
  - `node scripts/playwright_saas_admin_check.mjs`
  - Validates Vue routes + `/api/saas/summary|features|links|capabilities` + `/api/management/*` + built-in tenant API reachability + admin path closure

## Deploy (Dev Stage)
For current dev-stage deployment (no 7x24 requirement), use:
```bash
./scripts/deploy_saas.sh
```

Default flow is:
1. Build and test while service is still running.
2. Stop `orchardframework-saas.service`.
3. Backup current backend/frontend deployment folders.
4. Sync new files (backend excludes `data/` and `App_Data/`).
5. Start service and run health check.
6. Auto rollback if health check fails.

Common options:
```bash
# Skip tests
./scripts/deploy_saas.sh --skip-tests

# Backend only
./scripts/deploy_saas.sh --skip-frontend

# Frontend only
./scripts/deploy_saas.sh --skip-backend
```

Common environment overrides:
```bash
SERVICE_NAME=orchardframework-saas.service \
APP_ROOT=/www/wwwroot/pty.addai.vip \
HEALTH_URL=https://pty.addai.vip/saas/api/saas/summary \
./scripts/deploy_saas.sh
```

## Project Layout
- `src/OrchardFramework.Api`: Orchard host + recipes + inspection endpoints
- `tests/OrchardFramework.Api.Tests`: backend integration tests
- `frontend`: Vue frontend
- `docs/PDCA.md`: PDCA cycle record
