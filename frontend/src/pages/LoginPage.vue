<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

import { useAuthStore } from '../stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const form = reactive({
  username: 'admin',
  password: 'Admin@123',
})

const loading = ref(false)
const errorMessage = ref('')

const submit = async () => {
  loading.value = true
  errorMessage.value = ''

  try {
    await authStore.login(form.username, form.password)
    await router.push('/')
  } catch {
    errorMessage.value = '登录失败，请检查用户名和密码。'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <form class="card" @submit.prevent="submit">
      <h1>Orchard SaaS 控制台</h1>
      <p class="subtle">使用平台管理员账号登录</p>

      <label>
        用户名
        <input v-model="form.username" required autocomplete="username" />
      </label>

      <label>
        密码
        <input v-model="form.password" type="password" required autocomplete="current-password" />
      </label>

      <button type="submit" :disabled="loading">
        {{ loading ? '登录中...' : '登录' }}
      </button>

      <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    </form>
  </div>
</template>
