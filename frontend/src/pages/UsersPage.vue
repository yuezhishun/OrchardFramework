<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

import AppModal from '../components/AppModal.vue'
import { managementApi } from '../api/service'
import type { ManagementRole, ManagementTenant, ManagementUser } from '../api/types'

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')

const tenantFilter = ref('')
const tenants = ref<ManagementTenant[]>([])
const users = ref<ManagementUser[]>([])
const roles = ref<ManagementRole[]>([])
const editingUserId = ref<string | null>(null)
const isUserFormModalOpen = ref(false)

const form = reactive({
  userName: '',
  email: '',
  password: '',
  isEnabled: true,
  roleNames: [] as string[],
})

const effectiveTenant = computed(() => tenantFilter.value || undefined)
const availableRoleNames = computed(() => roles.value.map((item) => item.name).sort((a, b) => a.localeCompare(b)))

const resetForm = () => {
  editingUserId.value = null
  form.userName = ''
  form.email = ''
  form.password = ''
  form.isEnabled = true
  form.roleNames = []
}

const handleUserFormModalToggle = (open: boolean) => {
  isUserFormModalOpen.value = open
  if (!open) {
    resetForm()
  }
}

const openCreateUserForm = () => {
  resetForm()
  isUserFormModalOpen.value = true
}

const loadTenants = async () => {
  const all = await managementApi.listTenants()
  tenants.value = all.filter((item) => item.isDefault || item.state === 'Running')
}

const loadUsers = async () => {
  loading.value = true
  error.value = ''

  try {
    const [userList, roleList] = await Promise.all([
      managementApi.listUsers(effectiveTenant.value),
      managementApi.listRoles(effectiveTenant.value),
    ])

    users.value = userList
    roles.value = roleList

    if (!tenants.value.length) {
      await loadTenants()
    }
  } catch {
    error.value = '用户数据加载失败。'
  } finally {
    loading.value = false
  }
}

const saveUser = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const roleNames = [...new Set(form.roleNames.map((item) => item.trim()).filter(Boolean))]

    if (editingUserId.value) {
      const patchPayload: {
        tenant?: string
        email: string
        isEnabled: boolean
        roleNames: string[]
        password?: string
      } = {
        tenant: effectiveTenant.value,
        email: form.email.trim(),
        isEnabled: form.isEnabled,
        roleNames,
      }

      if (form.password.trim()) {
        patchPayload.password = form.password.trim()
      }

      await managementApi.patchUser(editingUserId.value, patchPayload)
      message.value = '用户已更新。'
    } else {
      await managementApi.createUser({
        tenant: effectiveTenant.value,
        userName: form.userName.trim(),
        email: form.email.trim(),
        password: form.password,
        isEnabled: form.isEnabled,
        roleNames,
      })
      message.value = '用户已创建。'
    }

    handleUserFormModalToggle(false)
    await loadUsers()
  } catch {
    error.value = '用户保存失败。请检查用户名、邮箱、密码与角色配置。'
  } finally {
    saving.value = false
  }
}

const editUser = (user: ManagementUser) => {
  editingUserId.value = user.id
  form.userName = user.userName
  form.email = user.email
  form.password = ''
  form.isEnabled = user.isEnabled
  form.roleNames = [...user.roleNames]
  isUserFormModalOpen.value = true
}

const removeUser = async (user: ManagementUser) => {
  if (!window.confirm(`确认删除用户 ${user.userName} ?`)) {
    return
  }

  error.value = ''
  message.value = ''

  try {
    await managementApi.patchUser(user.id, {
      tenant: effectiveTenant.value,
      operation: 'remove',
    })

    message.value = `用户 ${user.userName} 已删除。`
    if (editingUserId.value === user.id) {
      handleUserFormModalToggle(false)
    }
    await loadUsers()
  } catch {
    error.value = '用户删除失败。'
  }
}

onMounted(async () => {
  try {
    await loadTenants()
  } catch {
    // keep tenant filter on current scope
  }
  await loadUsers()
})
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>用户管理（H3）</h3>
      <div class="action-row">
        <select v-model="tenantFilter" @change="loadUsers">
          <option value="">当前租户（Default）</option>
          <option
            v-for="tenant in tenants.filter((item) => !item.isDefault && item.state === 'Running')"
            :key="tenant.name"
            :value="tenant.name"
          >
            {{ tenant.name }}
          </option>
        </select>
        <button class="ghost" @click="loadUsers" :disabled="loading">刷新</button>
        <button @click="openCreateUserForm">新建用户</button>
      </div>
    </div>

    <AppModal
      :model-value="isUserFormModalOpen"
      :title="editingUserId ? `编辑用户：${editingUserId}` : '新建用户'"
      max-width="900px"
      @update:model-value="handleUserFormModalToggle"
    >
      <form class="panel modal-form" @submit.prevent="saveUser">
        <div class="form-grid three">
          <label>
            用户名
            <input v-model="form.userName" :disabled="!!editingUserId" required />
          </label>
          <label>
            邮箱
            <input v-model="form.email" type="email" required />
          </label>
          <label>
            密码{{ editingUserId ? '（留空则不修改）' : '' }}
            <input v-model="form.password" type="password" :required="!editingUserId" />
          </label>
        </div>

        <div class="form-grid two">
          <label>
            角色分配
            <select v-model="form.roleNames" multiple>
              <option v-for="roleName in availableRoleNames" :key="roleName" :value="roleName">{{ roleName }}</option>
            </select>
          </label>

          <label class="checkbox inline">
            <input v-model="form.isEnabled" type="checkbox" />
            启用用户
          </label>
        </div>

        <div class="action-row">
          <button type="submit" :disabled="saving">{{ editingUserId ? '保存修改' : '创建用户' }}</button>
          <button type="button" class="ghost" @click="handleUserFormModalToggle(false)">取消</button>
        </div>
      </form>
    </AppModal>

    <p v-if="message" class="success">{{ message }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>ID</th>
            <th>用户名</th>
            <th>邮箱</th>
            <th>启用状态</th>
            <th>邮箱确认</th>
            <th>角色</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="user in users" :key="user.id">
            <td>{{ user.id }}</td>
            <td>{{ user.userName }}</td>
            <td>{{ user.email || '-' }}</td>
            <td>
              <span :class="user.isEnabled ? 'badge success' : 'badge muted'">
                {{ user.isEnabled ? '启用' : '禁用' }}
              </span>
            </td>
            <td>
              <span :class="user.emailConfirmed ? 'badge success' : 'badge muted'">
                {{ user.emailConfirmed ? '已确认' : '未确认' }}
              </span>
            </td>
            <td>{{ user.roleNames.length ? user.roleNames.join(', ') : '-' }}</td>
            <td class="action-row">
              <button class="ghost" @click="editUser(user)">编辑</button>
              <button class="danger" @click="removeUser(user)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
