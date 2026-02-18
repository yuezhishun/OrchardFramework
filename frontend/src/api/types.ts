export interface LoginResponse {
  token: string
  expiresAt: string
  userId: number
  tenantId: number
  username: string
  roles: string[]
  permissions: string[]
}

export interface Tenant {
  id: number
  name: string
  status: number
  createdOn: string
  url: string
  isEnabled: boolean
}

export interface Role {
  id: number
  tenantId: number
  name: string
  description: string
  permissionNames: string[]
}

export interface Permission {
  id: number
  name: string
  description: string
}

export interface User {
  id: number
  tenantId: number
  username: string
  email: string
  isEnabled: boolean
  roleNames: string[]
}

export interface TemplateItem {
  id?: number
  appName?: string
  command?: string
  projectName?: string
  directoryPath?: string
  variable?: string
  value?: string
}

export interface TerminalTemplate {
  id: number
  tenantId: number
  name: string
  description: string
  isEnabled: boolean
  applicationConfigs: TemplateItem[]
  startupDirectories: TemplateItem[]
  environmentVariables: TemplateItem[]
}

export interface FeatureStatus {
  name: string
  enabled: boolean
}

export interface SaasSummary {
  ready: boolean
  lastUpdatedUtc: string
  message?: string
  databasePath: string
  tenantCount: number
  defaultTenantState: string
  site: {
    siteName: string
    timeZoneId: string
  }
  openId: {
    applications: number
    scopes: number
    tokens: number
    authorizations: number
  }
  requiredSaasFeatures: FeatureStatus[]
  cmsFeatures: FeatureStatus[]
}

export interface SaasLinksItem {
  name: string
  url: string
  description: string
  enabled?: boolean
}

export interface SaasCapabilities {
  ready: boolean
  lastUpdatedUtc: string
  mode: string
  managementUi: string
  fallbackAdminUi: string
  adminPathAccessDisabled: boolean
  allowAnonymousManagementApi?: boolean
  builtInApis: string[]
  availableAdapters?: string[]
  plannedAdapters: string[]
  headlessFeatures: FeatureStatus[]
  missingHeadlessFeatures: string[]
}

export interface ManagementTenant {
  name: string
  state: string
  isDefault: boolean
  requestUrlHost: string
  requestUrlPrefix: string
  category: string
  description: string
  recipeName: string
  databaseProvider: string
  featureProfiles: string[]
}

export interface ManagementFeature {
  id: string
  name: string
  category: string
  description: string
  enabled: boolean
  isAlwaysEnabled: boolean
  enabledByDependencyOnly: boolean
  defaultTenantOnly: boolean
  dependencies: string[]
}

export interface ManagementFeaturePayload {
  tenant: string
  updatedAtUtc: string
  features: ManagementFeature[]
}

export interface FeatureProfileRule {
  rule: string
  expression: string
}

export interface FeatureProfileItem {
  id: string
  name: string
  featureRules: FeatureProfileRule[]
  assignedTenants: string[]
}

export interface ManagementUser {
  id: string
  userName: string
  email: string
  isEnabled: boolean
  emailConfirmed: boolean
  roleNames: string[]
}

export interface ManagementRole {
  id: string
  name: string
  permissionNames: string[]
}

export interface ManagementPermission {
  name: string
  description: string
  category: string
}

export interface ManagementSiteSettings {
  siteName: string
  timeZoneId: string
  calendar: string
  baseUrl: string
  pageSize: number
  maxPageSize: number
  maxPagedCount: number
  useCdn: boolean
  cdnBaseUrl: string
  appendVersion: boolean
  resourceDebugMode: string
  cacheMode: string
}

export interface ManagementLocalization {
  defaultCulture: string
  supportedCultures: string[]
}

export interface ManagementOpenIdApplication {
  id: string
  clientId: string
  displayName: string
  clientType: string
  consentType: string
  redirectUris: string[]
  postLogoutRedirectUris: string[]
  scopeNames: string[]
  permissionNames: string[]
  roleNames: string[]
  requirements: string[]
}

export interface ManagementOpenIdScope {
  id: string
  name: string
  displayName: string
  description: string
  resources: string[]
}

export interface ManagementRecipe {
  id: string
  name: string
  displayName: string
  description: string
  basePath: string
  fileName: string
  author: string
  website: string
  version: string
  categories: string[]
  tags: string[]
}

export interface ManagementRecipeExecutionResult {
  executionId: string
  result: string
  tenant: string
  recipeId: string
  recipeName: string
  displayName: string
  basePath: string
  fileName: string
  releasedShellContext: boolean
}
