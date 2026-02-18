<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { saasApi } from '../api/service'
import type { SaasCapabilities } from '../api/types'

const loading = ref(false)
const error = ref('')
const capabilities = ref<SaasCapabilities | null>(null)

const loadCapabilities = async () => {
  loading.value = true
  error.value = ''

  try {
    capabilities.value = await saasApi.capabilities()
  } catch {
    error.value = '能力探测读取失败。'
  } finally {
    loading.value = false
  }
}

onMounted(loadCapabilities)
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>Headless 能力探测</h3>
      <button @click="loadCapabilities" :disabled="loading">刷新</button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="capabilities" class="panel">
      <div class="form-grid three">
        <div>
          <p class="subtle">模式</p>
          <h4>{{ capabilities.mode }}</h4>
        </div>
        <div>
          <p class="subtle">管理入口目标</p>
          <h4>{{ capabilities.managementUi }}</h4>
        </div>
        <div>
          <p class="subtle">后台兜底入口</p>
          <h4>{{ capabilities.adminPathAccessDisabled ? '临时关闭' : capabilities.fallbackAdminUi }}</h4>
        </div>
      </div>

      <p class="subtle">
        最后更新时间（UTC）：{{ new Date(capabilities.lastUpdatedUtc).toLocaleString() }}
      </p>
      <p class="subtle">
        管理 API 访问策略：{{ capabilities.allowAnonymousManagementApi ? '开发阶段匿名可访问' : '需认证 + Orchard 权限' }}
      </p>
    </div>

    <div v-if="capabilities" class="split-grid">
      <div class="panel">
        <h4>Headless 关键模块状态</h4>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Feature</th>
                <th>状态</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in capabilities.headlessFeatures" :key="item.name">
                <td>{{ item.name }}</td>
                <td>
                  <span :class="item.enabled ? 'badge success' : 'badge muted'">
                    {{ item.enabled ? 'Enabled' : 'Disabled' }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="panel">
        <h4>缺失模块</h4>
        <p v-if="capabilities.missingHeadlessFeatures.length === 0" class="subtle">无</p>
        <ul v-else>
          <li v-for="item in capabilities.missingHeadlessFeatures" :key="item">{{ item }}</li>
        </ul>
      </div>
    </div>

    <div v-if="capabilities" class="split-grid">
      <div class="panel">
        <h4>可直接复用的内置 API</h4>
        <ul>
          <li v-for="item in capabilities.builtInApis" :key="item">{{ item }}</li>
        </ul>
      </div>
      <div class="panel" v-if="capabilities.availableAdapters?.length">
        <h4>已提供薄适配 API（H4）</h4>
        <ul>
          <li v-for="item in capabilities.availableAdapters" :key="item">{{ item }}</li>
        </ul>
      </div>
      <div class="panel">
        <h4>计划中的薄适配 API</h4>
        <ul>
          <li v-for="item in capabilities.plannedAdapters" :key="item">{{ item }}</li>
        </ul>
      </div>
    </div>
  </section>
</template>
