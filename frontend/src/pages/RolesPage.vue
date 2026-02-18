<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

import AppModal from '../components/AppModal.vue'
import { managementApi } from '../api/service'
import type { ManagementPermission, ManagementRole, ManagementTenant } from '../api/types'

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')

const tenantFilter = ref('')
const permissionKeyword = ref('')

const tenants = ref<ManagementTenant[]>([])
const roles = ref<ManagementRole[]>([])
const permissions = ref<ManagementPermission[]>([])

const editingRoleId = ref<string | null>(null)
const selectedRoleId = ref<string | null>(null)
const selectedPermissionNames = ref<string[]>([])
const isRoleFormModalOpen = ref(false)

const form = reactive({
  name: '',
})

const effectiveTenant = computed(() => tenantFilter.value || undefined)

const visiblePermissions = computed(() => {
  const keyword = permissionKeyword.value.trim().toLowerCase()
  if (!keyword) {
    return permissions.value
  }

  return permissions.value.filter((item) => {
    const target = `${item.name} ${item.category} ${item.description}`.toLowerCase()
    return target.includes(keyword)
  })
})

const resetForm = () => {
  editingRoleId.value = null
  form.name = ''
}

const handleRoleFormModalToggle = (open: boolean) => {
  isRoleFormModalOpen.value = open
  if (!open) {
    resetForm()
  }
}

const openCreateRoleForm = () => {
  resetForm()
  isRoleFormModalOpen.value = true
}

const loadTenants = async () => {
  const all = await managementApi.listTenants()
  tenants.value = all.filter((item) => item.isDefault || item.state === 'Running')
}

const loadRoles = async () => {
  loading.value = true
  error.value = ''

  try {
    const [roleList, permissionList] = await Promise.all([
      managementApi.listRoles(effectiveTenant.value),
      managementApi.listPermissions(effectiveTenant.value),
    ])

    roles.value = roleList
    permissions.value = permissionList

    if (!tenants.value.length) {
      await loadTenants()
    }

    if (selectedRoleId.value) {
      const selected = roles.value.find((item) => item.id === selectedRoleId.value)
      selectedPermissionNames.value = selected ? [...selected.permissionNames] : []
    }
  } catch {
    error.value = '角色数据加载失败。'
  } finally {
    loading.value = false
  }
}

const saveRole = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    if (editingRoleId.value) {
      await managementApi.patchRole(editingRoleId.value, {
        tenant: effectiveTenant.value,
        name: form.name.trim(),
      })
      message.value = '角色已更新。'
    } else {
      await managementApi.createRole({
        tenant: effectiveTenant.value,
        name: form.name.trim(),
        permissionNames: [],
      })
      message.value = '角色已创建。'
    }

    handleRoleFormModalToggle(false)
    await loadRoles()
  } catch {
    error.value = '角色保存失败。'
  } finally {
    saving.value = false
  }
}

const editRole = (role: ManagementRole) => {
  editingRoleId.value = role.id
  form.name = role.name
  isRoleFormModalOpen.value = true
}

const selectRole = (role: ManagementRole) => {
  selectedRoleId.value = role.id
  selectedPermissionNames.value = [...role.permissionNames]
}

const saveRolePermissions = async () => {
  if (!selectedRoleId.value) {
    return
  }

  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const permissionNames = [...new Set(selectedPermissionNames.value)]
    await managementApi.updateRolePermissions(selectedRoleId.value, {
      tenant: effectiveTenant.value,
      permissionNames,
    })
    message.value = '角色权限已更新。'
    await loadRoles()
  } catch {
    error.value = '角色权限更新失败。'
  } finally {
    saving.value = false
  }
}

const removeRole = async (role: ManagementRole) => {
  if (!window.confirm(`确认删除角色 ${role.name} ?`)) {
    return
  }

  error.value = ''
  message.value = ''

  try {
    await managementApi.patchRole(role.id, {
      tenant: effectiveTenant.value,
      operation: 'remove',
    })

    message.value = `角色 ${role.name} 已删除。`
    if (editingRoleId.value === role.id) {
      handleRoleFormModalToggle(false)
    }
    if (selectedRoleId.value === role.id) {
      selectedRoleId.value = null
      selectedPermissionNames.value = []
    }
    await loadRoles()
  } catch {
    error.value = '角色删除失败。'
  }
}

onMounted(async () => {
  try {
    await loadTenants()
  } catch {
    // keep tenant filter on current scope
  }
  await loadRoles()
})
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>角色与权限（H3）</h3>
      <div class="action-row">
        <select v-model="tenantFilter" @change="loadRoles">
          <option value="">当前租户（Default）</option>
          <option
            v-for="tenant in tenants.filter((item) => !item.isDefault && item.state === 'Running')"
            :key="tenant.name"
            :value="tenant.name"
          >
            {{ tenant.name }}
          </option>
        </select>
        <button class="ghost" @click="loadRoles" :disabled="loading">刷新</button>
        <button @click="openCreateRoleForm">新建角色</button>
      </div>
    </div>

    <AppModal
      :model-value="isRoleFormModalOpen"
      :title="editingRoleId ? `编辑角色：${editingRoleId}` : '新建角色'"
      max-width="620px"
      @update:model-value="handleRoleFormModalToggle"
    >
      <form class="panel modal-form" @submit.prevent="saveRole">
        <div class="form-grid two">
          <label>
            角色名称
            <input v-model="form.name" required />
          </label>
          <p class="subtle">角色权限请在下方“角色权限分配”区域保存。</p>
        </div>

        <div class="action-row">
          <button type="submit" :disabled="saving">{{ editingRoleId ? '保存修改' : '创建角色' }}</button>
          <button type="button" class="ghost" @click="handleRoleFormModalToggle(false)">取消</button>
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
              <th>ID</th>
              <th>名称</th>
              <th>权限数</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="role in roles"
              :key="role.id"
              :class="{ selected: selectedRoleId === role.id }"
              @click="selectRole(role)"
            >
              <td>{{ role.id }}</td>
              <td>{{ role.name }}</td>
              <td>{{ role.permissionNames.length }}</td>
              <td class="action-row">
                <button class="ghost" @click.stop="editRole(role)">编辑</button>
                <button class="danger" @click.stop="removeRole(role)">删除</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="panel">
        <h4>角色权限分配</h4>
        <p class="subtle">选择角色后，可按关键词筛选权限再保存。</p>

        <label>
          权限筛选
          <input v-model="permissionKeyword" placeholder="输入权限名 / 分类 / 描述" />
        </label>

        <div class="check-grid">
          <label v-for="permission in visiblePermissions" :key="permission.name" class="checkbox">
            <input v-model="selectedPermissionNames" type="checkbox" :value="permission.name" :disabled="!selectedRoleId" />
            {{ permission.name }}
          </label>
        </div>

        <button :disabled="!selectedRoleId || saving" @click="saveRolePermissions">保存权限</button>
      </div>
    </div>
  </section>
</template>
