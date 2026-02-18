<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { saasApi } from '../api/service'
import type { SaasSummary } from '../api/types'

const loading = ref(false)
const error = ref('')
const summary = ref<SaasSummary | null>(null)

const loadSummary = async () => {
  loading.value = true
  error.value = ''

  try {
    summary.value = await saasApi.summary()
  } catch {
    error.value = 'SaaS 状态加载失败，请确认后端服务已启动。'
  } finally {
    loading.value = false
  }
}

onMounted(loadSummary)
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>SaaS 基线总览</h3>
      <button @click="loadSummary" :disabled="loading">刷新</button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="summary" class="panel">
      <div class="form-grid three">
        <div>
          <p class="subtle">系统状态</p>
          <h4>
            <span :class="summary.ready ? 'badge success' : 'badge muted'">
              {{ summary.ready ? 'Ready' : 'Not Ready' }}
            </span>
          </h4>
        </div>
        <div>
          <p class="subtle">租户数</p>
          <h4>{{ summary.tenantCount }}</h4>
        </div>
        <div>
          <p class="subtle">Default Tenant 状态</p>
          <h4>{{ summary.defaultTenantState }}</h4>
        </div>
      </div>

      <div class="form-grid two">
        <div>
          <p class="subtle">站点名称</p>
          <h4>{{ summary.site.siteName || '-' }}</h4>
        </div>
        <div>
          <p class="subtle">时区</p>
          <h4>{{ summary.site.timeZoneId || '-' }}</h4>
        </div>
      </div>

      <p class="subtle">DB：{{ summary.databasePath }}</p>
      <p class="subtle">最后更新时间（UTC）：{{ new Date(summary.lastUpdatedUtc).toLocaleString() }}</p>
      <p v-if="summary.message" class="subtle">{{ summary.message }}</p>
    </div>

    <div v-if="summary" class="stats-grid">
      <article class="stat-card">
        <span>必需功能已启用</span>
        <strong>{{ summary.requiredSaasFeatures.filter((x) => x.enabled).length }}</strong>
      </article>
      <article class="stat-card">
        <span>必需功能总数</span>
        <strong>{{ summary.requiredSaasFeatures.length }}</strong>
      </article>
      <article class="stat-card">
        <span>OpenID 应用</span>
        <strong>{{ summary.openId.applications }}</strong>
      </article>
      <article class="stat-card">
        <span>OpenID Scope</span>
        <strong>{{ summary.openId.scopes }}</strong>
      </article>
      <article class="stat-card">
        <span>OpenID Token</span>
        <strong>{{ summary.openId.tokens }}</strong>
      </article>
      <article class="stat-card">
        <span>OpenID 授权</span>
        <strong>{{ summary.openId.authorizations }}</strong>
      </article>
    </div>
  </section>
</template>
