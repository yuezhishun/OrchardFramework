<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

import { managementApi } from '../api/service'
import type { ManagementLocalization, ManagementTenant } from '../api/types'

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')
const tenantFilter = ref('')

const tenants = ref<ManagementTenant[]>([])
const localization = ref<ManagementLocalization | null>(null)

const form = reactive({
  defaultCulture: '',
  supportedCulturesText: '',
})

const effectiveTenant = computed(() => tenantFilter.value || undefined)

const parseCultures = (text: string) =>
  text
    .split(/[\n,]/g)
    .map((item) => item.trim())
    .filter(Boolean)

const loadTenants = async () => {
  const all = await managementApi.listTenants()
  tenants.value = all.filter((item) => item.isDefault || item.state === 'Running')
}

const applyLocalization = (data: ManagementLocalization) => {
  localization.value = data
  form.defaultCulture = data.defaultCulture || ''
  form.supportedCulturesText = data.supportedCultures.join(', ')
}

const loadLocalization = async () => {
  loading.value = true
  error.value = ''
  message.value = ''

  try {
    const data = await managementApi.localization(effectiveTenant.value)
    applyLocalization(data)

    if (!tenants.value.length) {
      await loadTenants()
    }
  } catch {
    error.value = '本地化设置加载失败。'
  } finally {
    loading.value = false
  }
}

const saveLocalization = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const payload = {
      tenant: effectiveTenant.value,
      defaultCulture: form.defaultCulture.trim(),
      supportedCultures: parseCultures(form.supportedCulturesText),
    }

    const updated = await managementApi.updateLocalization(payload)
    applyLocalization(updated)
    message.value = '本地化设置已更新。'
  } catch {
    error.value = '本地化设置保存失败。请检查文化代码格式（如 zh-CN、en-US）。'
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

  await loadLocalization()
})
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>本地化设置（H4）</h3>
      <div class="action-row">
        <select v-model="tenantFilter" @change="loadLocalization">
          <option value="">当前租户（Default）</option>
          <option
            v-for="tenant in tenants.filter((item) => !item.isDefault && item.state === 'Running')"
            :key="tenant.name"
            :value="tenant.name"
          >
            {{ tenant.name }}
          </option>
        </select>
        <button class="ghost" @click="loadLocalization" :disabled="loading">刷新</button>
      </div>
    </div>

    <form class="panel" @submit.prevent="saveLocalization">
      <h4>文化配置</h4>

      <div class="form-grid two">
        <label>
          默认文化
          <input v-model="form.defaultCulture" placeholder="例如：zh-CN" required />
        </label>
        <p class="subtle">支持文化列表需包含默认文化，可用逗号或换行分隔。</p>
      </div>

      <label>
        支持文化列表
        <textarea
          v-model="form.supportedCulturesText"
          rows="6"
          placeholder="zh-CN, en-US"
        />
      </label>

      <div class="action-row">
        <button type="submit" :disabled="saving">保存设置</button>
      </div>
    </form>

    <p v-if="message" class="success">{{ message }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="localization" class="panel">
      <h4>当前文化状态</h4>
      <p class="subtle">Default: {{ localization.defaultCulture || '-' }}</p>
      <p class="subtle">
        Supported: {{ localization.supportedCultures.length ? localization.supportedCultures.join(', ') : '-' }}
      </p>
    </div>
  </section>
</template>
