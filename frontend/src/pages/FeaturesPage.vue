<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { managementApi } from '../api/service'
import type { ManagementFeature, ManagementTenant } from '../api/types'

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')
const tenantFilter = ref('')
const keyword = ref('')

const tenants = ref<ManagementTenant[]>([])
const features = ref<ManagementFeature[]>([])
const originalEnabledMap = ref<Record<string, boolean>>({})
const toggledMap = ref<Record<string, boolean>>({})

const visibleFeatures = computed(() => {
  const key = keyword.value.trim().toLowerCase()
  if (!key) {
    return features.value
  }

  return features.value.filter((item) => {
    const target = `${item.id} ${item.name} ${item.category} ${item.description}`.toLowerCase()
    return target.includes(key)
  })
})

const changedFeatureCount = computed(() =>
  Object.keys(toggledMap.value).filter((id) => toggledMap.value[id] !== originalEnabledMap.value[id]).length,
)

const effectiveEnabled = (feature: ManagementFeature) =>
  toggledMap.value[feature.id] ?? originalEnabledMap.value[feature.id] ?? feature.enabled

const markEnabled = (feature: ManagementFeature, enabled: boolean) => {
  toggledMap.value = { ...toggledMap.value, [feature.id]: enabled }
}

const loadTenants = async () => {
  tenants.value = await managementApi.listTenants()
}

const loadFeatures = async () => {
  loading.value = true
  error.value = ''
  message.value = ''

  try {
    const payload = await managementApi.features(tenantFilter.value || undefined)
    features.value = payload.features
    originalEnabledMap.value = Object.fromEntries(payload.features.map((item) => [item.id, item.enabled]))
    toggledMap.value = {}
  } catch {
    error.value = '功能列表加载失败。'
  } finally {
    loading.value = false
  }
}

const saveChanges = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const enable = Object.keys(toggledMap.value).filter(
      (id) => toggledMap.value[id] === true && originalEnabledMap.value[id] === false,
    )
    const disable = Object.keys(toggledMap.value).filter(
      (id) => toggledMap.value[id] === false && originalEnabledMap.value[id] === true,
    )

    if (!enable.length && !disable.length) {
      message.value = '没有需要提交的变更。'
      return
    }

    await managementApi.updateFeatures({
      tenant: tenantFilter.value || undefined,
      enable,
      disable,
      force: true,
    })
    message.value = `已提交变更：启用 ${enable.length} 项，禁用 ${disable.length} 项。`
    await loadFeatures()
  } catch {
    error.value = '功能变更提交失败。'
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  try {
    await loadTenants()
  } catch {
    // keep list empty, feature load can still proceed on current tenant
  }
  await loadFeatures()
})
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>功能管理（H2）</h3>
      <div class="action-row">
        <select v-model="tenantFilter" @change="loadFeatures">
          <option value="">当前租户（Default）</option>
          <option
            v-for="tenant in tenants.filter((item) => item.name !== 'Default' && item.state === 'Running')"
            :key="tenant.name"
            :value="tenant.name"
          >
            {{ tenant.name }}
          </option>
        </select>
        <button class="ghost" @click="loadFeatures" :disabled="loading">刷新</button>
        <button @click="saveChanges" :disabled="saving || changedFeatureCount === 0">
          提交变更（{{ changedFeatureCount }}）
        </button>
      </div>
    </div>

    <div class="panel">
      <div class="form-grid two">
        <label>
          关键词筛选
          <input v-model="keyword" placeholder="输入 Feature Id / 名称 / 分类" />
        </label>
        <p class="subtle">
          已加载 {{ features.length }} 项，可见 {{ visibleFeatures.length }} 项。含依赖关系和常驻模块时，请谨慎禁用。
        </p>
      </div>
    </div>

    <p v-if="message" class="success">{{ message }}</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Feature</th>
            <th>分类</th>
            <th>说明</th>
            <th>依赖</th>
            <th>状态</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in visibleFeatures" :key="item.id">
            <td>
              <strong>{{ item.id }}</strong>
              <p class="subtle">{{ item.name }}</p>
            </td>
            <td>{{ item.category || '-' }}</td>
            <td>{{ item.description || '-' }}</td>
            <td>{{ item.dependencies.length ? item.dependencies.join(', ') : '-' }}</td>
            <td>
              <span :class="effectiveEnabled(item) ? 'badge success' : 'badge muted'">
                {{ effectiveEnabled(item) ? 'Enabled' : 'Disabled' }}
              </span>
              <span v-if="item.isAlwaysEnabled" class="badge muted">Always On</span>
              <span v-if="item.enabledByDependencyOnly" class="badge muted">Dependency</span>
            </td>
            <td class="action-row">
              <button
                class="ghost"
                :disabled="item.isAlwaysEnabled"
                @click="markEnabled(item, !effectiveEnabled(item))"
              >
                {{ effectiveEnabled(item) ? '标记禁用' : '标记启用' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
