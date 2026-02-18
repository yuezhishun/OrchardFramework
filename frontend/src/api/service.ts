import { http } from './http'
import type {
  FeatureProfileItem,
  ManagementPermission,
  ManagementLocalization,
  LoginResponse,
  ManagementFeaturePayload,
  ManagementOpenIdApplication,
  ManagementOpenIdScope,
  ManagementRecipe,
  ManagementRecipeExecutionResult,
  ManagementRole,
  ManagementSiteSettings,
  ManagementTenant,
  ManagementUser,
  Permission,
  Role,
  SaasCapabilities,
  SaasLinksItem,
  SaasSummary,
  Tenant,
  TerminalTemplate,
  User,
} from './types'

export const authApi = {
  async login(username: string, password: string) {
    const { data } = await http.post<LoginResponse>('/auth/login', { username, password })
    return data
  },
}

export const tenantApi = {
  async list() {
    const { data } = await http.get<Tenant[]>('/tenants')
    return data
  },
  async create(payload: { name: string; url: string; isEnabled: boolean }) {
    const { data } = await http.post<{ id: number; name: string }>('/tenants', payload)
    return data
  },
  async update(id: number, payload: { name: string; url: string; isEnabled: boolean }) {
    const { data } = await http.put<{ id: number; name: string }>(`/tenants/${id}`, payload)
    return data
  },
  async setStatus(id: number, isEnabled: boolean) {
    const { data } = await http.patch<{ id: number; isEnabled: boolean }>(`/tenants/${id}/status`, {
      isEnabled,
    })
    return data
  },
  async remove(id: number) {
    await http.delete(`/tenants/${id}`)
  },
}

export const permissionApi = {
  async list() {
    const { data } = await http.get<Permission[]>('/permissions')
    return data
  },
  async create(payload: { name: string; description: string }) {
    const { data } = await http.post<Permission>('/permissions', payload)
    return data
  },
  async update(id: number, payload: { name: string; description: string }) {
    const { data } = await http.put<Permission>(`/permissions/${id}`, payload)
    return data
  },
  async remove(id: number) {
    await http.delete(`/permissions/${id}`)
  },
}

export const roleApi = {
  async list(tenantId?: number) {
    const { data } = await http.get<Role[]>('/roles', { params: tenantId ? { tenantId } : undefined })
    return data
  },
  async create(payload: { tenantId?: number; name: string; description: string }) {
    const { data } = await http.post<{ id: number; name: string; tenantId: number }>('/roles', payload)
    return data
  },
  async update(id: number, payload: { name: string; description: string; tenantId?: number }) {
    const { data } = await http.put<{ id: number; name: string; description: string }>(`/roles/${id}`, payload)
    return data
  },
  async assignPermissions(id: number, permissionIds: number[]) {
    const { data } = await http.put<{ id: number; permissionCount: number }>(`/roles/${id}/permissions`, {
      permissionIds,
    })
    return data
  },
  async remove(id: number) {
    await http.delete(`/roles/${id}`)
  },
}

export const userApi = {
  async list(tenantId?: number) {
    const { data } = await http.get<User[]>('/users', { params: tenantId ? { tenantId } : undefined })
    return data
  },
  async create(payload: {
    tenantId?: number
    username: string
    email: string
    password: string
    isEnabled: boolean
    roleIds: number[]
  }) {
    const { data } = await http.post<{ id: number; username: string; tenantId: number }>('/users', payload)
    return data
  },
  async update(
    id: number,
    payload: {
      email: string
      isEnabled: boolean
      roleIds: number[]
    },
  ) {
    const { data } = await http.put<{ id: number; email: string; isEnabled: boolean }>(`/users/${id}`, payload)
    return data
  },
  async assignRoles(id: number, roleIds: number[]) {
    const { data } = await http.post<{ id: number; roleCount: number }>(`/users/${id}/roles`, {
      roleIds,
    })
    return data
  },
  async remove(id: number) {
    await http.delete(`/users/${id}`)
  },
}

export const templateApi = {
  async list(tenantId?: number) {
    const { data } = await http.get<TerminalTemplate[]>('/templates', {
      params: tenantId ? { tenantId } : undefined,
    })
    return data
  },
  async create(payload: {
    tenantId?: number
    name: string
    description: string
    isEnabled: boolean
    applicationConfigs: { appName: string; command: string }[]
    startupDirectories: { projectName: string; directoryPath: string }[]
    environmentVariables: { variable: string; value: string }[]
  }) {
    const { data } = await http.post<{ id: number; name: string; tenantId: number }>('/templates', payload)
    return data
  },
  async update(
    id: number,
    payload: {
      tenantId?: number
      name: string
      description: string
      isEnabled: boolean
      applicationConfigs: { appName: string; command: string }[]
      startupDirectories: { projectName: string; directoryPath: string }[]
      environmentVariables: { variable: string; value: string }[]
    },
  ) {
    const { data } = await http.put<{ id: number; name: string; isEnabled: boolean }>(`/templates/${id}`, payload)
    return data
  },
  async setStatus(id: number, isEnabled: boolean) {
    const { data } = await http.patch<{ id: number; isEnabled: boolean }>(`/templates/${id}/status`, {
      isEnabled,
    })
    return data
  },
  async remove(id: number) {
    await http.delete(`/templates/${id}`)
  },
}

export const managementApi = {
  async listTenants() {
    const { data } = await http.get<ManagementTenant[]>('/management/tenants')
    return data
  },
  async createTenant(payload: {
    name: string
    requestUrlHost?: string
    requestUrlPrefix?: string
    category?: string
    description?: string
    recipeName?: string
    databaseProvider?: string
    featureProfiles?: string[]
  }) {
    const { data } = await http.post<ManagementTenant>('/management/tenants', payload)
    return data
  },
  async patchTenant(
    tenantName: string,
    payload: {
      requestUrlHost?: string
      requestUrlPrefix?: string
      category?: string
      description?: string
      featureProfiles?: string[]
      enabled?: boolean
      operation?: 'remove'
    },
  ) {
    const { data } = await http.patch<ManagementTenant | { removed: string }>(
      `/management/tenants/${encodeURIComponent(tenantName)}`,
      payload,
    )
    return data
  },
  async features(tenant?: string) {
    const { data } = await http.get<ManagementFeaturePayload>('/management/features', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async updateFeatures(payload: {
    tenant?: string
    enable: string[]
    disable: string[]
    force?: boolean
  }) {
    const { data } = await http.put<{
      tenant: string
      updatedAtUtc: string
      changed: { enabled: string[]; disabled: string[] }
      features: ManagementFeaturePayload['features']
      appliedTenant: string
    }>('/management/features', payload)
    return data
  },
  async listFeatureProfiles() {
    const { data } = await http.get<FeatureProfileItem[]>('/management/feature-profiles')
    return data
  },
  async upsertFeatureProfile(payload: {
    id: string
    name?: string
    featureRules: { rule: string; expression: string }[]
    delete?: boolean
  }) {
    const { data } = await http.put<FeatureProfileItem | { removed: string }>(
      '/management/feature-profiles',
      payload,
    )
    return data
  },
  async listUsers(tenant?: string) {
    const { data } = await http.get<ManagementUser[]>('/management/users', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async createUser(payload: {
    tenant?: string
    userName: string
    email: string
    password: string
    isEnabled: boolean
    roleNames: string[]
  }) {
    const { data } = await http.post<ManagementUser>('/management/users', payload)
    return data
  },
  async patchUser(
    id: string,
    payload: {
      tenant?: string
      email?: string
      password?: string
      isEnabled?: boolean
      roleNames?: string[]
      operation?: 'remove'
    },
  ) {
    const { data } = await http.patch<ManagementUser | { removed: string }>(
      `/management/users/${encodeURIComponent(id)}`,
      payload,
    )
    return data
  },
  async listRoles(tenant?: string) {
    const { data } = await http.get<ManagementRole[]>('/management/roles', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async createRole(payload: {
    tenant?: string
    name: string
    permissionNames: string[]
  }) {
    const { data } = await http.post<ManagementRole>('/management/roles', payload)
    return data
  },
  async patchRole(
    id: string,
    payload: {
      tenant?: string
      name?: string
      operation?: 'remove'
    },
  ) {
    const { data } = await http.patch<ManagementRole | { removed: string }>(
      `/management/roles/${encodeURIComponent(id)}`,
      payload,
    )
    return data
  },
  async updateRolePermissions(
    id: string,
    payload: {
      tenant?: string
      permissionNames: string[]
    },
  ) {
    const { data } = await http.put<ManagementRole>(`/management/roles/${encodeURIComponent(id)}/permissions`, payload)
    return data
  },
  async listPermissions(tenant?: string) {
    const { data } = await http.get<ManagementPermission[]>('/management/permissions', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async siteSettings(tenant?: string) {
    const { data } = await http.get<ManagementSiteSettings>('/management/site-settings', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async updateSiteSettings(payload: {
    tenant?: string
    siteName?: string
    timeZoneId?: string
    calendar?: string
    baseUrl?: string
    pageSize?: number
    maxPageSize?: number
    maxPagedCount?: number
    useCdn?: boolean
    cdnBaseUrl?: string
    appendVersion?: boolean
    resourceDebugMode?: string
    cacheMode?: string
  }) {
    const { data } = await http.put<ManagementSiteSettings>('/management/site-settings', payload)
    return data
  },
  async localization(tenant?: string) {
    const { data } = await http.get<ManagementLocalization>('/management/localization', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async updateLocalization(payload: {
    tenant?: string
    defaultCulture?: string
    supportedCultures?: string[]
  }) {
    const { data } = await http.put<ManagementLocalization>('/management/localization', payload)
    return data
  },
  async listOpenIdApplications(tenant?: string) {
    const { data } = await http.get<ManagementOpenIdApplication[]>('/management/openid/applications', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async createOpenIdApplication(payload: {
    tenant?: string
    clientId: string
    displayName: string
    clientType?: string
    consentType?: string
    clientSecret?: string
    redirectUris?: string[]
    postLogoutRedirectUris?: string[]
    scopeNames?: string[]
    permissionNames?: string[]
    roleNames?: string[]
    requirements?: string[]
  }) {
    const { data } = await http.post<ManagementOpenIdApplication>('/management/openid/applications', payload)
    return data
  },
  async patchOpenIdApplication(
    id: string,
    payload: {
      tenant?: string
      clientId?: string
      displayName?: string
      clientType?: string
      consentType?: string
      clientSecret?: string
      redirectUris?: string[]
      postLogoutRedirectUris?: string[]
      scopeNames?: string[]
      permissionNames?: string[]
      roleNames?: string[]
      requirements?: string[]
      operation?: 'remove'
    },
  ) {
    const { data } = await http.patch<ManagementOpenIdApplication | { removed: string }>(
      `/management/openid/applications/${encodeURIComponent(id)}`,
      payload,
    )
    return data
  },
  async listOpenIdScopes(tenant?: string) {
    const { data } = await http.get<ManagementOpenIdScope[]>('/management/openid/scopes', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async createOpenIdScope(payload: {
    tenant?: string
    name: string
    displayName: string
    description?: string
    resources?: string[]
  }) {
    const { data } = await http.post<ManagementOpenIdScope>('/management/openid/scopes', payload)
    return data
  },
  async patchOpenIdScope(
    id: string,
    payload: {
      tenant?: string
      name?: string
      displayName?: string
      description?: string
      resources?: string[]
      operation?: 'remove'
    },
  ) {
    const { data } = await http.patch<ManagementOpenIdScope | { removed: string }>(
      `/management/openid/scopes/${encodeURIComponent(id)}`,
      payload,
    )
    return data
  },
  async listRecipes(tenant?: string) {
    const { data } = await http.get<ManagementRecipe[]>('/management/recipes', {
      params: tenant ? { tenant } : undefined,
    })
    return data
  },
  async executeRecipe(payload: {
    tenant?: string
    recipeId?: string
    recipeName?: string
    fileName?: string
    environment?: Record<string, string>
    releaseShellContext?: boolean
  }) {
    const { data } = await http.post<ManagementRecipeExecutionResult>('/management/recipes/execute', payload)
    return data
  },
}

export const saasApi = {
  async summary() {
    const { data } = await http.get<SaasSummary>('/saas/summary')
    return data
  },
  async features() {
    const { data } = await http.get<Pick<SaasSummary, 'ready' | 'lastUpdatedUtc' | 'requiredSaasFeatures' | 'cmsFeatures'>>('/saas/features')
    return data
  },
  async links() {
    const { data } = await http.get<SaasLinksItem[]>('/saas/links')
    return data
  },
  async capabilities() {
    const { data } = await http.get<SaasCapabilities>('/saas/capabilities')
    return data
  },
}
