<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

import { managementApi } from '../api/service'
import type { ManagementSiteSettings, ManagementTenant } from '../api/types'

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')
const tenantFilter = ref('')

const tenants = ref<ManagementTenant[]>([])
const settings = ref<ManagementSiteSettings | null>(null)

const form = reactive({
  siteName: '',
  timeZoneId: '',
  calendar: '',
  baseUrl: '',
  pageSize: 10,
  maxPageSize: 100,
  maxPagedCount: 1000,
  useCdn: false,
  cdnBaseUrl: '',
  appendVersion: true,
  resourceDebugMode: 'FromConfiguration',
  cacheMode: 'FromConfiguration',
})

const effectiveTenant = computed(() => tenantFilter.value || undefined)

const loadTenants = async () => {
  const all = await managementApi.listTenants()
  tenants.value = all.filter((item) => item.isDefault || item.state === 'Running')
}

const applySettings = (data: ManagementSiteSettings) => {
  settings.value = data
  form.siteName = data.siteName || ''
  form.timeZoneId = data.timeZoneId || ''
  form.calendar = data.calendar || ''
  form.baseUrl = data.baseUrl || ''
  form.pageSize = data.pageSize
  form.maxPageSize = data.maxPageSize
  form.maxPagedCount = data.maxPagedCount
  form.useCdn = data.useCdn
  form.cdnBaseUrl = data.cdnBaseUrl || ''
  form.appendVersion = data.appendVersion
  form.resourceDebugMode = data.resourceDebugMode || 'FromConfiguration'
  form.cacheMode = data.cacheMode || 'FromConfiguration'
}

const loadSettings = async () => {
  loading.value = true
  error.value = ''
  message.value = ''

  try {
    const data = await managementApi.siteSettings(effectiveTenant.value)
    applySettings(data)

    if (!tenants.value.length) {
      await loadTenants()
    }
  } catch {
    error.value = '站点设置加载失败。'
  } finally {
    loading.value = false
  }
}

const saveSettings = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const payload = {
      tenant: effectiveTenant.value,
      siteName: form.siteName,
      timeZoneId: form.timeZoneId,
      calendar: form.calendar,
      baseUrl: form.baseUrl,
      pageSize: form.pageSize,
      maxPageSize: form.maxPageSize,
      maxPagedCount: form.maxPagedCount,
      useCdn: form.useCdn,
      cdnBaseUrl: form.cdnBaseUrl,
      appendVersion: form.appendVersion,
      resourceDebugMode: form.resourceDebugMode,
      cacheMode: form.cacheMode,
    }

    const updated = await managementApi.updateSiteSettings(payload)
    applySettings(updated)
    message.value = '站点设置已更新。'
  } catch {
    error.value = '站点设置保存失败。请检查时区、URL 和分页参数。'
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  try {
    await loadTenants()
  } catch {
    // tenant list is optional for current scope
  }

  await loadSettings()
})
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>站点设置（H4）</h3>
      <div class="action-row">
        <select v-model="tenantFilter" @change="loadSettings">
          <option value="">当前租户（Default）</option>
          <option
            v-for="tenant in tenants.filter((item) => !item.isDefault && item.state === 'Running')"
            :key="tenant.name"
            :value="tenant.name"
          >
            {{ tenant.name }}
          </option>
        </select>
        <button class="ghost" @click="loadSettings" :disabled="loading">刷新</button>
      </div>
    </div>

    <form class="panel" @submit.prevent="saveSettings">
      <h4>基础配置</h4>

      <div class="form-grid three">
        <label>
          站点名称
          <input v-model="form.siteName" required />
        </label>
        <label>
          时区
          <input v-model="form.timeZoneId" placeholder="例如：Asia/Shanghai" />
        </label>
        <label>
          日历
          <input v-model="form.calendar" placeholder="例如：Gregorian" />
        </label>
      </div>

      <div class="form-grid two">
        <label>
          BaseUrl
          <input v-model="form.baseUrl" placeholder="例如：https://tenant-a.example.com" />
        </label>
        <label>
          CDN BaseUrl
          <input v-model="form.cdnBaseUrl" :disabled="!form.useCdn" placeholder="例如：https://cdn.example.com" />
        </label>
      </div>

      <div class="form-grid four">
        <label>
          PageSize
          <input v-model.number="form.pageSize" type="number" min="1" required />
        </label>
        <label>
          MaxPageSize
          <input v-model.number="form.maxPageSize" type="number" min="1" required />
        </label>
        <label>
          MaxPagedCount
          <input v-model.number="form.maxPagedCount" type="number" min="1" required />
        </label>
        <label>
          ResourceDebugMode
          <input v-model="form.resourceDebugMode" placeholder="FromConfiguration/Enabled/Disabled" />
        </label>
      </div>

      <div class="form-grid two">
        <label>
          CacheMode
          <input v-model="form.cacheMode" placeholder="FromConfiguration/Enabled/Disabled" />
        </label>

        <div class="action-row">
          <label class="checkbox inline">
            <input v-model="form.useCdn" type="checkbox" />
            启用 CDN
          </label>
          <label class="checkbox inline">
            <input v-model="form.appendVersion" type="checkbox" />
            静态资源追加版本号
          </label>
        </div>
      </div>

      <div class="action-row">
        <button type="submit" :disabled="saving">保存设置</button>
      </div>
    </form>

    <p v-if="message" class="success">{{ message }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="settings" class="panel">
      <h4>当前状态</h4>
      <p class="subtle">Site: {{ settings.siteName || '-' }}</p>
      <p class="subtle">TimeZone: {{ settings.timeZoneId || '-' }}</p>
      <p class="subtle">BaseUrl: {{ settings.baseUrl || '-' }}</p>
      <p class="subtle">
        Pagination: {{ settings.pageSize }} / {{ settings.maxPageSize }} / {{ settings.maxPagedCount }}
      </p>
    </div>
  </section>
</template>
