<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

import AppModal from '../components/AppModal.vue'
import { templateApi, tenantApi } from '../api/service'
import type { Tenant, TerminalTemplate } from '../api/types'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()

const templates = ref<TerminalTemplate[]>([])
const tenants = ref<Tenant[]>([])

const loading = ref(false)
const error = ref('')
const message = ref('')
const editId = ref<number | null>(null)
const isTemplateFormModalOpen = ref(false)

const tenantFilter = ref<number | undefined>(undefined)

const form = reactive({
  tenantId: authStore.profile?.tenantId,
  name: '',
  description: '',
  isEnabled: true,
  applicationConfigs: [{ appName: '', command: '' }],
  startupDirectories: [{ projectName: '', directoryPath: '' }],
  environmentVariables: [{ variable: '', value: '' }],
})

const effectiveTenantFilter = computed(() => {
  if (authStore.isPlatformAdmin) {
    return tenantFilter.value
  }
  return authStore.profile?.tenantId
})

const resetForm = () => {
  editId.value = null
  form.tenantId = authStore.profile?.tenantId
  form.name = ''
  form.description = ''
  form.isEnabled = true
  form.applicationConfigs = [{ appName: '', command: '' }]
  form.startupDirectories = [{ projectName: '', directoryPath: '' }]
  form.environmentVariables = [{ variable: '', value: '' }]
}

const handleTemplateFormModalToggle = (open: boolean) => {
  isTemplateFormModalOpen.value = open
  if (!open) {
    resetForm()
  }
}

const openCreateTemplateForm = () => {
  resetForm()
  isTemplateFormModalOpen.value = true
}

const loadTemplates = async () => {
  loading.value = true
  error.value = ''

  try {
    templates.value = await templateApi.list(effectiveTenantFilter.value)

    if (authStore.isPlatformAdmin) {
      tenants.value = await tenantApi.list()
    }
  } catch {
    error.value = '模板数据加载失败。'
  } finally {
    loading.value = false
  }
}

const saveTemplate = async () => {
  message.value = ''
  error.value = ''

  const payload = {
    tenantId: form.tenantId,
    name: form.name,
    description: form.description,
    isEnabled: form.isEnabled,
    applicationConfigs: form.applicationConfigs.filter((item) => item.appName && item.command),
    startupDirectories: form.startupDirectories.filter(
      (item) => item.projectName && item.directoryPath,
    ),
    environmentVariables: form.environmentVariables.filter((item) => item.variable),
  }

  try {
    if (editId.value) {
      await templateApi.update(editId.value, payload)
      message.value = '模板已更新。'
    } else {
      await templateApi.create(payload)
      message.value = '模板已创建。'
    }

    handleTemplateFormModalToggle(false)
    await loadTemplates()
  } catch {
    error.value = '模板保存失败。'
  }
}

const editTemplate = (template: TerminalTemplate) => {
  editId.value = template.id
  form.tenantId = template.tenantId
  form.name = template.name
  form.description = template.description
  form.isEnabled = template.isEnabled

  form.applicationConfigs =
    template.applicationConfigs.map((item) => ({
      appName: item.appName ?? '',
      command: item.command ?? '',
    })) ?? []

  form.startupDirectories =
    template.startupDirectories.map((item) => ({
      projectName: item.projectName ?? '',
      directoryPath: item.directoryPath ?? '',
    })) ?? []

  form.environmentVariables =
    template.environmentVariables.map((item) => ({
      variable: item.variable ?? '',
      value: item.value ?? '',
    })) ?? []

  if (!form.applicationConfigs.length) {
    form.applicationConfigs = [{ appName: '', command: '' }]
  }
  if (!form.startupDirectories.length) {
    form.startupDirectories = [{ projectName: '', directoryPath: '' }]
  }
  if (!form.environmentVariables.length) {
    form.environmentVariables = [{ variable: '', value: '' }]
  }

  isTemplateFormModalOpen.value = true
}

const toggleStatus = async (template: TerminalTemplate) => {
  try {
    await templateApi.setStatus(template.id, !template.isEnabled)
    await loadTemplates()
  } catch {
    error.value = '模板状态更新失败。'
  }
}

const removeTemplate = async (template: TerminalTemplate) => {
  if (!window.confirm(`确认删除模板 ${template.name} ?`)) {
    return
  }

  try {
    await templateApi.remove(template.id)
    if (editId.value === template.id) {
      handleTemplateFormModalToggle(false)
    }
    await loadTemplates()
  } catch {
    error.value = '模板删除失败。'
  }
}

onMounted(loadTemplates)
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>终端模板管理</h3>
      <div class="action-row">
        <select v-if="authStore.isPlatformAdmin" v-model.number="tenantFilter">
          <option :value="undefined">全部租户</option>
          <option v-for="tenant in tenants" :key="tenant.id" :value="tenant.id">{{ tenant.name }}</option>
        </select>
        <button class="ghost" @click="loadTemplates" :disabled="loading">刷新</button>
        <button @click="openCreateTemplateForm">新建模板</button>
      </div>
    </div>

    <AppModal
      :model-value="isTemplateFormModalOpen"
      :title="editId ? '编辑模板' : '新建模板'"
      max-width="1100px"
      @update:model-value="handleTemplateFormModalToggle"
    >
      <form class="panel modal-form" @submit.prevent="saveTemplate">
        <div class="form-grid four">
          <label>
            模板名称
            <input v-model="form.name" required />
          </label>
          <label>
            描述
            <input v-model="form.description" required />
          </label>
          <label>
            租户
            <select v-model.number="form.tenantId" :disabled="!authStore.isPlatformAdmin" required>
              <option
                v-for="tenant in authStore.isPlatformAdmin ? tenants : [{ id: authStore.profile?.tenantId, name: '当前租户' }]"
                :key="tenant.id"
                :value="tenant.id"
              >
                {{ tenant.name }}
              </option>
            </select>
          </label>
          <label class="checkbox inline">
            <input v-model="form.isEnabled" type="checkbox" />
            启用模板
          </label>
        </div>

        <div class="dynamic-section">
          <h5>应用配置</h5>
          <div v-for="(item, idx) in form.applicationConfigs" :key="`app-${idx}`" class="form-grid two compact">
            <input v-model="item.appName" placeholder="应用名" required />
            <input v-model="item.command" placeholder="启动命令" required />
          </div>
          <div class="action-row">
            <button type="button" class="ghost" @click="form.applicationConfigs.push({ appName: '', command: '' })">
              增加应用
            </button>
            <button
              type="button"
              class="ghost"
              :disabled="form.applicationConfigs.length <= 1"
              @click="form.applicationConfigs.pop()"
            >
              删除最后一项
            </button>
          </div>
        </div>

        <div class="dynamic-section">
          <h5>启动目录</h5>
          <div v-for="(item, idx) in form.startupDirectories" :key="`dir-${idx}`" class="form-grid two compact">
            <input v-model="item.projectName" placeholder="项目名" required />
            <input v-model="item.directoryPath" placeholder="目录路径" required />
          </div>
          <div class="action-row">
            <button
              type="button"
              class="ghost"
              @click="form.startupDirectories.push({ projectName: '', directoryPath: '' })"
            >
              增加目录
            </button>
            <button
              type="button"
              class="ghost"
              :disabled="form.startupDirectories.length <= 1"
              @click="form.startupDirectories.pop()"
            >
              删除最后一项
            </button>
          </div>
        </div>

        <div class="dynamic-section">
          <h5>环境变量</h5>
          <div v-for="(item, idx) in form.environmentVariables" :key="`env-${idx}`" class="form-grid two compact">
            <input v-model="item.variable" placeholder="变量名" required />
            <input v-model="item.value" placeholder="变量值" />
          </div>
          <div class="action-row">
            <button
              type="button"
              class="ghost"
              @click="form.environmentVariables.push({ variable: '', value: '' })"
            >
              增加变量
            </button>
            <button
              type="button"
              class="ghost"
              :disabled="form.environmentVariables.length <= 1"
              @click="form.environmentVariables.pop()"
            >
              删除最后一项
            </button>
          </div>
        </div>

        <div class="action-row">
          <button type="submit">{{ editId ? '保存修改' : '创建模板' }}</button>
          <button type="button" class="ghost" @click="handleTemplateFormModalToggle(false)">取消</button>
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
            <th>租户</th>
            <th>模板名</th>
            <th>描述</th>
            <th>状态</th>
            <th>应用数</th>
            <th>目录数</th>
            <th>变量数</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="template in templates" :key="template.id">
            <td>{{ template.id }}</td>
            <td>{{ template.tenantId }}</td>
            <td>{{ template.name }}</td>
            <td>{{ template.description }}</td>
            <td>
              <span :class="template.isEnabled ? 'badge success' : 'badge muted'">
                {{ template.isEnabled ? '启用' : '禁用' }}
              </span>
            </td>
            <td>{{ template.applicationConfigs.length }}</td>
            <td>{{ template.startupDirectories.length }}</td>
            <td>{{ template.environmentVariables.length }}</td>
            <td class="action-row">
              <button class="ghost" @click="editTemplate(template)">编辑</button>
              <button class="ghost" @click="toggleStatus(template)">
                {{ template.isEnabled ? '禁用' : '启用' }}
              </button>
              <button class="danger" @click="removeTemplate(template)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
