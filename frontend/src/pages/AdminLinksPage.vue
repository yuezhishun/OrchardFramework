<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { saasApi } from '../api/service'
import type { SaasLinksItem } from '../api/types'

const loading = ref(false)
const error = ref('')
const links = ref<SaasLinksItem[]>([])

const loadLinks = async () => {
  loading.value = true
  error.value = ''

  try {
    links.value = await saasApi.links()
  } catch {
    error.value = '管理入口加载失败。'
  } finally {
    loading.value = false
  }
}

onMounted(loadLinks)
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>Orchard 管理入口</h3>
      <button @click="loadLinks" :disabled="loading">刷新</button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div class="form-grid two">
      <article v-for="item in links" :key="item.name" class="panel">
        <h4>{{ item.name }}</h4>
        <p class="subtle">{{ item.description }}</p>
        <div class="action-row">
          <a
            v-if="item.enabled !== false"
            class="inline-link"
            :href="item.url"
            target="_blank"
            rel="noreferrer"
          >
            打开 {{ item.url }}
          </a>
          <span v-else class="subtle">当前入口临时关闭</span>
        </div>
      </article>
    </div>
  </section>
</template>
