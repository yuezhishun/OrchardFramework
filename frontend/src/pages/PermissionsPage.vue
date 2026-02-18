<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { managementApi } from '../api/service'
import type { ManagementPermission, ManagementTenant } from '../api/types'

const loading = ref(false)
const error = ref('')

const tenantFilter = ref('')
const keyword = ref('')

const tenants = ref<ManagementTenant[]>([])
const permissions = ref<ManagementPermission[]>([])

const effectiveTenant = computed(() => tenantFilter.value || undefined)

const visiblePermissions = computed(() => {
  const key = keyword.value.trim().toLowerCase()
  if (!key) {
    return permissions.value
  }

  return permissions.value.filter((item) => {
    const target = `${item.name} ${item.category} ${item.description}`.toLowerCase()
    return target.includes(key)
  })
})

const loadTenants = async () => {
  const all = await managementApi.listTenants()
  tenants.value = all.filter((item) => item.isDefault || item.state === 'Running')
}

const loadPermissions = async () => {
  loading.value = true
  error.value = ''

  try {
    permissions.value = await managementApi.listPermissions(effectiveTenant.value)

    if (!tenants.value.length) {
      await loadTenants()
    }
  } catch {
    error.value = '权限列表加载失败。'
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  try {
    await loadTenants()
  } catch {
    // keep tenant filter on current scope
  }
  await loadPermissions()
})
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>权限目录（H3）</h3>
      <div class="action-row">
        <select v-model="tenantFilter" @change="loadPermissions">
          <option value="">当前租户（Default）</option>
          <option
            v-for="tenant in tenants.filter((item) => !item.isDefault && item.state === 'Running')"
            :key="tenant.name"
            :value="tenant.name"
          >
            {{ tenant.name }}
          </option>
        </select>
        <button class="ghost" @click="loadPermissions" :disabled="loading">刷新</button>
      </div>
    </div>

    <div class="panel">
      <div class="form-grid two">
        <label>
          关键词筛选
          <input v-model="keyword" placeholder="输入权限名 / 分类 / 描述" />
        </label>
        <p class="subtle">共 {{ permissions.length }} 项，当前显示 {{ visiblePermissions.length }} 项。</p>
      </div>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>权限名称</th>
            <th>分类</th>
            <th>说明</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="permission in visiblePermissions" :key="permission.name">
            <td><code>{{ permission.name }}</code></td>
            <td>{{ permission.category || '-' }}</td>
            <td>{{ permission.description || '-' }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
