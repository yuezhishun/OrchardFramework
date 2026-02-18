<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

import AppModal from '../components/AppModal.vue'
import { managementApi } from '../api/service'
import type {
  ManagementOpenIdApplication,
  ManagementOpenIdScope,
  ManagementTenant,
} from '../api/types'

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')
const tenantFilter = ref('')

const tenants = ref<ManagementTenant[]>([])
const applications = ref<ManagementOpenIdApplication[]>([])
const scopes = ref<ManagementOpenIdScope[]>([])

const editingApplicationId = ref<string | null>(null)
const editingScopeId = ref<string | null>(null)
const isApplicationFormModalOpen = ref(false)
const isScopeFormModalOpen = ref(false)

const applicationForm = reactive({
  clientId: '',
  displayName: '',
  clientType: 'confidential',
  consentType: 'explicit',
  clientSecret: '',
  redirectUrisText: '',
  postLogoutRedirectUrisText: '',
  scopeNames: [] as string[],
  permissionNamesText: '',
  roleNamesText: '',
  requirementsText: '',
})

const scopeForm = reactive({
  name: '',
  displayName: '',
  description: '',
  resourcesText: '',
})

const effectiveTenant = computed(() => tenantFilter.value || undefined)

const parseList = (text: string) =>
  text
    .split(/[\n,]/g)
    .map((item) => item.trim())
    .filter(Boolean)

const resetApplicationForm = () => {
  editingApplicationId.value = null
  applicationForm.clientId = ''
  applicationForm.displayName = ''
  applicationForm.clientType = 'confidential'
  applicationForm.consentType = 'explicit'
  applicationForm.clientSecret = ''
  applicationForm.redirectUrisText = ''
  applicationForm.postLogoutRedirectUrisText = ''
  applicationForm.scopeNames = []
  applicationForm.permissionNamesText = ''
  applicationForm.roleNamesText = ''
  applicationForm.requirementsText = ''
}

const handleApplicationFormModalToggle = (open: boolean) => {
  isApplicationFormModalOpen.value = open
  if (!open) {
    resetApplicationForm()
  }
}

const openCreateApplicationForm = () => {
  resetApplicationForm()
  isApplicationFormModalOpen.value = true
}

const resetScopeForm = () => {
  editingScopeId.value = null
  scopeForm.name = ''
  scopeForm.displayName = ''
  scopeForm.description = ''
  scopeForm.resourcesText = ''
}

const handleScopeFormModalToggle = (open: boolean) => {
  isScopeFormModalOpen.value = open
  if (!open) {
    resetScopeForm()
  }
}

const openCreateScopeForm = () => {
  resetScopeForm()
  isScopeFormModalOpen.value = true
}

const loadTenants = async () => {
  const all = await managementApi.listTenants()
  tenants.value = all.filter((item) => item.isDefault || item.state === 'Running')
}

const loadData = async () => {
  loading.value = true
  error.value = ''

  try {
    const [applicationList, scopeList] = await Promise.all([
      managementApi.listOpenIdApplications(effectiveTenant.value),
      managementApi.listOpenIdScopes(effectiveTenant.value),
    ])

    applications.value = applicationList
    scopes.value = scopeList

    if (!tenants.value.length) {
      await loadTenants()
    }
  } catch {
    error.value = 'OpenId 数据加载失败。请确认已启用 OpenId 管理模块。'
  } finally {
    loading.value = false
  }
}

const saveApplication = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const payload: {
      tenant?: string
      clientId: string
      displayName: string
      clientType: string
      consentType: string
      clientSecret?: string
      redirectUris: string[]
      postLogoutRedirectUris: string[]
      scopeNames: string[]
      permissionNames: string[]
      roleNames: string[]
      requirements: string[]
    } = {
      tenant: effectiveTenant.value,
      clientId: applicationForm.clientId.trim(),
      displayName: applicationForm.displayName.trim(),
      clientType: applicationForm.clientType.trim(),
      consentType: applicationForm.consentType.trim(),
      redirectUris: parseList(applicationForm.redirectUrisText),
      postLogoutRedirectUris: parseList(applicationForm.postLogoutRedirectUrisText),
      scopeNames: [...new Set(applicationForm.scopeNames.map((item) => item.trim()).filter(Boolean))],
      permissionNames: parseList(applicationForm.permissionNamesText),
      roleNames: parseList(applicationForm.roleNamesText),
      requirements: parseList(applicationForm.requirementsText),
    }

    if (applicationForm.clientSecret.trim()) {
      payload.clientSecret = applicationForm.clientSecret.trim()
    }

    if (editingApplicationId.value) {
      await managementApi.patchOpenIdApplication(editingApplicationId.value, payload)
      message.value = 'OpenId 应用已更新。'
    } else {
      await managementApi.createOpenIdApplication(payload)
      message.value = 'OpenId 应用已创建。'
    }

    handleApplicationFormModalToggle(false)
    await loadData()
  } catch {
    error.value = 'OpenId 应用保存失败。请检查 ClientId、ClientType、Secret 和 URI。'
  } finally {
    saving.value = false
  }
}

const editApplication = (item: ManagementOpenIdApplication) => {
  editingApplicationId.value = item.id
  applicationForm.clientId = item.clientId
  applicationForm.displayName = item.displayName
  applicationForm.clientType = item.clientType || 'confidential'
  applicationForm.consentType = item.consentType || 'explicit'
  applicationForm.clientSecret = ''
  applicationForm.redirectUrisText = item.redirectUris.join(', ')
  applicationForm.postLogoutRedirectUrisText = item.postLogoutRedirectUris.join(', ')
  applicationForm.scopeNames = [...item.scopeNames]
  applicationForm.permissionNamesText = item.permissionNames.join(', ')
  applicationForm.roleNamesText = item.roleNames.join(', ')
  applicationForm.requirementsText = item.requirements.join(', ')
  isApplicationFormModalOpen.value = true
}

const removeApplication = async (item: ManagementOpenIdApplication) => {
  if (!window.confirm(`确认删除 OpenId 应用 ${item.clientId} ?`)) {
    return
  }

  error.value = ''
  message.value = ''

  try {
    await managementApi.patchOpenIdApplication(item.id, {
      tenant: effectiveTenant.value,
      operation: 'remove',
    })
    message.value = `OpenId 应用 ${item.clientId} 已删除。`
    if (editingApplicationId.value === item.id) {
      handleApplicationFormModalToggle(false)
    }
    await loadData()
  } catch {
    error.value = 'OpenId 应用删除失败。'
  }
}

const saveScope = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const payload = {
      tenant: effectiveTenant.value,
      name: scopeForm.name.trim(),
      displayName: scopeForm.displayName.trim(),
      description: scopeForm.description.trim(),
      resources: parseList(scopeForm.resourcesText),
    }

    if (editingScopeId.value) {
      await managementApi.patchOpenIdScope(editingScopeId.value, payload)
      message.value = 'OpenId Scope 已更新。'
    } else {
      await managementApi.createOpenIdScope(payload)
      message.value = 'OpenId Scope 已创建。'
    }

    handleScopeFormModalToggle(false)
    await loadData()
  } catch {
    error.value = 'OpenId Scope 保存失败。请检查 Name、DisplayName 和 Resources。'
  } finally {
    saving.value = false
  }
}

const editScope = (item: ManagementOpenIdScope) => {
  editingScopeId.value = item.id
  scopeForm.name = item.name
  scopeForm.displayName = item.displayName
  scopeForm.description = item.description
  scopeForm.resourcesText = item.resources.join(', ')
  isScopeFormModalOpen.value = true
}

const removeScope = async (item: ManagementOpenIdScope) => {
  if (!window.confirm(`确认删除 OpenId Scope ${item.name} ?`)) {
    return
  }

  error.value = ''
  message.value = ''

  try {
    await managementApi.patchOpenIdScope(item.id, {
      tenant: effectiveTenant.value,
      operation: 'remove',
    })
    message.value = `OpenId Scope ${item.name} 已删除。`
    if (editingScopeId.value === item.id) {
      handleScopeFormModalToggle(false)
    }
    await loadData()
  } catch {
    error.value = 'OpenId Scope 删除失败。'
  }
}

onMounted(async () => {
  try {
    await loadTenants()
  } catch {
    // tenant list is optional for current scope
  }

  await loadData()
})
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>OpenId 应用与 Scope（H4）</h3>
      <div class="action-row">
        <select v-model="tenantFilter" @change="loadData">
          <option value="">当前租户（Default）</option>
          <option
            v-for="tenant in tenants.filter((item) => !item.isDefault && item.state === 'Running')"
            :key="tenant.name"
            :value="tenant.name"
          >
            {{ tenant.name }}
          </option>
        </select>
        <button class="ghost" @click="loadData" :disabled="loading">刷新</button>
        <button @click="openCreateApplicationForm">新建应用</button>
        <button @click="openCreateScopeForm">新建 Scope</button>
      </div>
    </div>

    <AppModal
      :model-value="isApplicationFormModalOpen"
      :title="editingApplicationId ? `编辑应用：${editingApplicationId}` : '新建 OpenId 应用'"
      max-width="1100px"
      @update:model-value="handleApplicationFormModalToggle"
    >
      <form class="panel modal-form" @submit.prevent="saveApplication">
        <div class="form-grid four">
          <label>
            ClientId
            <input v-model="applicationForm.clientId" required />
          </label>
          <label>
            DisplayName
            <input v-model="applicationForm.displayName" required />
          </label>
          <label>
            ClientType
            <select v-model="applicationForm.clientType">
              <option value="confidential">confidential</option>
              <option value="public">public</option>
            </select>
          </label>
          <label>
            ConsentType
            <select v-model="applicationForm.consentType">
              <option value="explicit">explicit</option>
              <option value="implicit">implicit</option>
              <option value="external">external</option>
              <option value="systematic">systematic</option>
            </select>
          </label>
        </div>

        <div class="form-grid two">
          <label>
            ClientSecret{{ editingApplicationId ? '（留空不修改）' : '' }}
            <input
              v-model="applicationForm.clientSecret"
              type="password"
              :required="!editingApplicationId && applicationForm.clientType === 'confidential'"
            />
          </label>
          <label>
            Scope 选择
            <select v-model="applicationForm.scopeNames" multiple>
              <option v-for="scope in scopes" :key="scope.id" :value="scope.name">{{ scope.name }}</option>
            </select>
          </label>
        </div>

        <div class="form-grid two">
          <label>
            Redirect URIs（逗号/换行分隔）
            <textarea v-model="applicationForm.redirectUrisText" rows="3" />
          </label>
          <label>
            PostLogout Redirect URIs（逗号/换行分隔）
            <textarea v-model="applicationForm.postLogoutRedirectUrisText" rows="3" />
          </label>
        </div>

        <div class="form-grid three">
          <label>
            PermissionNames（逗号/换行分隔）
            <textarea v-model="applicationForm.permissionNamesText" rows="3" />
          </label>
          <label>
            RoleNames（逗号/换行分隔）
            <textarea v-model="applicationForm.roleNamesText" rows="3" />
          </label>
          <label>
            Requirements（逗号/换行分隔）
            <textarea v-model="applicationForm.requirementsText" rows="3" />
          </label>
        </div>

        <div class="action-row">
          <button type="submit" :disabled="saving">{{ editingApplicationId ? '保存应用' : '创建应用' }}</button>
          <button type="button" class="ghost" @click="handleApplicationFormModalToggle(false)">取消</button>
        </div>
      </form>
    </AppModal>

    <AppModal
      :model-value="isScopeFormModalOpen"
      :title="editingScopeId ? `编辑 Scope：${editingScopeId}` : '新建 OpenId Scope'"
      max-width="820px"
      @update:model-value="handleScopeFormModalToggle"
    >
      <form class="panel modal-form" @submit.prevent="saveScope">
        <div class="form-grid three">
          <label>
            Name
            <input v-model="scopeForm.name" required />
          </label>
          <label>
            DisplayName
            <input v-model="scopeForm.displayName" required />
          </label>
          <label>
            Description
            <input v-model="scopeForm.description" />
          </label>
        </div>

        <label>
          Resources（逗号/换行分隔）
          <textarea v-model="scopeForm.resourcesText" rows="3" />
        </label>

        <div class="action-row">
          <button type="submit" :disabled="saving">{{ editingScopeId ? '保存 Scope' : '创建 Scope' }}</button>
          <button type="button" class="ghost" @click="handleScopeFormModalToggle(false)">取消</button>
        </div>
      </form>
    </AppModal>

    <p v-if="message" class="success">{{ message }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div class="split-grid">
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>应用</th>
              <th>类型</th>
              <th>Scope</th>
              <th>权限数</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in applications" :key="item.id">
              <td>
                <strong>{{ item.clientId }}</strong>
                <p class="subtle">{{ item.displayName || '-' }}</p>
              </td>
              <td>{{ item.clientType || '-' }}</td>
              <td>{{ item.scopeNames.length ? item.scopeNames.join(', ') : '-' }}</td>
              <td>{{ item.permissionNames.length }}</td>
              <td class="action-row">
                <button class="ghost" @click="editApplication(item)">编辑</button>
                <button class="danger" @click="removeApplication(item)">删除</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Scope</th>
              <th>描述</th>
              <th>资源</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in scopes" :key="item.id">
              <td>
                <strong>{{ item.name }}</strong>
                <p class="subtle">{{ item.displayName || '-' }}</p>
              </td>
              <td>{{ item.description || '-' }}</td>
              <td>{{ item.resources.length ? item.resources.join(', ') : '-' }}</td>
              <td class="action-row">
                <button class="ghost" @click="editScope(item)">编辑</button>
                <button class="danger" @click="removeScope(item)">删除</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
