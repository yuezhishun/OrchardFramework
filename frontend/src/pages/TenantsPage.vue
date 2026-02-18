<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

import AppModal from '../components/AppModal.vue'
import { managementApi } from '../api/service'
import type { ManagementTenant } from '../api/types'

const loading = ref(false)
const error = ref('')
const message = ref('')
const tenants = ref<ManagementTenant[]>([])
const editingTenantName = ref<string | null>(null)
const isFormModalOpen = ref(false)

const form = reactive({
  name: '',
  requestUrlPrefix: '',
  requestUrlHost: '',
  category: '',
  description: '',
  recipeName: 'SaaS.Base',
  featureProfiles: '',
})

const resetForm = () => {
  editingTenantName.value = null
  form.name = ''
  form.requestUrlPrefix = ''
  form.requestUrlHost = ''
  form.category = ''
  form.description = ''
  form.recipeName = 'SaaS.Base'
  form.featureProfiles = ''
}

const handleFormModalToggle = (open: boolean) => {
  isFormModalOpen.value = open
  if (!open) {
    resetForm()
  }
}

const openCreateTenantForm = () => {
  resetForm()
  isFormModalOpen.value = true
}

const parseProfiles = () =>
  form.featureProfiles
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)

const loadTenants = async () => {
  loading.value = true
  error.value = ''

  try {
    tenants.value = await managementApi.listTenants()
  } catch {
    error.value = '租户列表加载失败。'
  } finally {
    loading.value = false
  }
}

const editTenant = (tenant: ManagementTenant) => {
  editingTenantName.value = tenant.name
  form.name = tenant.name
  form.requestUrlPrefix = tenant.requestUrlPrefix
  form.requestUrlHost = tenant.requestUrlHost
  form.category = tenant.category
  form.description = tenant.description
  form.recipeName = tenant.recipeName || 'SaaS.Base'
  form.featureProfiles = tenant.featureProfiles.join(', ')
  isFormModalOpen.value = true
}

const saveTenant = async () => {
  error.value = ''
  message.value = ''

  const payload = {
    requestUrlPrefix: form.requestUrlPrefix,
    requestUrlHost: form.requestUrlHost,
    category: form.category,
    description: form.description,
    recipeName: form.recipeName,
    featureProfiles: parseProfiles(),
  }

  try {
    if (editingTenantName.value) {
      await managementApi.patchTenant(editingTenantName.value, payload)
      message.value = `租户 ${editingTenantName.value} 已更新。`
    } else {
      await managementApi.createTenant({
        name: form.name.trim(),
        ...payload,
      })
      message.value = `租户 ${form.name.trim()} 已创建。`
    }

    handleFormModalToggle(false)
    await loadTenants()
  } catch {
    error.value = '租户保存失败。请检查名称是否重复、参数是否有效。'
  }
}

const toggleTenant = async (tenant: ManagementTenant) => {
  error.value = ''
  message.value = ''

  try {
    const enabled = tenant.state !== 'Running'
    await managementApi.patchTenant(tenant.name, { enabled })
    message.value = enabled ? `租户 ${tenant.name} 已启用。` : `租户 ${tenant.name} 已禁用。`
    await loadTenants()
  } catch {
    error.value = '租户状态更新失败。'
  }
}

const removeTenant = async (tenant: ManagementTenant) => {
  if (!window.confirm(`确认删除租户 ${tenant.name} ?`)) {
    return
  }

  error.value = ''
  message.value = ''

  try {
    await managementApi.patchTenant(tenant.name, { operation: 'remove' })
    message.value = `租户 ${tenant.name} 已删除。`
    if (editingTenantName.value === tenant.name) {
      handleFormModalToggle(false)
    }
    await loadTenants()
  } catch {
    error.value = '租户删除失败。请先禁用租户并确认其状态允许删除。'
  }
}

onMounted(loadTenants)
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>租户管理（H2）</h3>
      <div class="action-row">
        <button class="ghost" @click="loadTenants" :disabled="loading">刷新</button>
        <button @click="openCreateTenantForm">新建租户</button>
      </div>
    </div>

    <AppModal
      :model-value="isFormModalOpen"
      :title="editingTenantName ? `编辑租户：${editingTenantName}` : '新建租户'"
      max-width="980px"
      @update:model-value="handleFormModalToggle"
    >
      <form class="panel modal-form" @submit.prevent="saveTenant">
        <div class="form-grid four">
          <label>
            名称
            <input v-model="form.name" :disabled="!!editingTenantName" required />
          </label>
          <label>
            URL Prefix
            <input v-model="form.requestUrlPrefix" placeholder="例如：tenant-a" />
          </label>
          <label>
            URL Host
            <input v-model="form.requestUrlHost" placeholder="例如：tenant-a.example.com" />
          </label>
          <label>
            Recipe
            <input v-model="form.recipeName" />
          </label>
        </div>

        <div class="form-grid three">
          <label>
            分类
            <input v-model="form.category" />
          </label>
          <label>
            描述
            <input v-model="form.description" />
          </label>
          <label>
            Feature Profiles（逗号分隔）
            <input v-model="form.featureProfiles" placeholder="default, enterprise" />
          </label>
        </div>

        <div class="action-row">
          <button type="submit">{{ editingTenantName ? '保存更新' : '创建租户' }}</button>
          <button type="button" class="ghost" @click="handleFormModalToggle(false)">取消</button>
        </div>
      </form>
    </AppModal>

    <p v-if="message" class="success">{{ message }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>名称</th>
            <th>状态</th>
            <th>URL Prefix</th>
            <th>URL Host</th>
            <th>Feature Profiles</th>
            <th>描述</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="tenant in tenants" :key="tenant.name">
            <td>
              {{ tenant.name }}
              <span v-if="tenant.isDefault" class="badge muted">Default</span>
            </td>
            <td>
              <span :class="tenant.state === 'Running' ? 'badge success' : 'badge muted'">
                {{ tenant.state }}
              </span>
            </td>
            <td>{{ tenant.requestUrlPrefix || '-' }}</td>
            <td>{{ tenant.requestUrlHost || '-' }}</td>
            <td>{{ tenant.featureProfiles.length ? tenant.featureProfiles.join(', ') : '-' }}</td>
            <td>{{ tenant.description || '-' }}</td>
            <td class="action-row">
              <button class="ghost" @click="editTenant(tenant)">编辑</button>
              <button
                class="ghost"
                :disabled="tenant.state === 'Uninitialized' || tenant.isDefault"
                @click="toggleTenant(tenant)"
              >
                {{ tenant.state === 'Running' ? '禁用' : '启用' }}
              </button>
              <button class="danger" :disabled="tenant.isDefault" @click="removeTenant(tenant)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
