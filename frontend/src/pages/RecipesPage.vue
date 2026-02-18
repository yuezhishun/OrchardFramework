<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { managementApi } from '../api/service'
import type { ManagementRecipe, ManagementRecipeExecutionResult } from '../api/types'

const loading = ref(false)
const executingId = ref('')
const error = ref('')
const success = ref('')
const selectedTenant = ref('')
const recipes = ref<ManagementRecipe[]>([])
const lastExecution = ref<ManagementRecipeExecutionResult | null>(null)

const normalizeTenant = () => {
  const value = selectedTenant.value.trim()
  return value.length > 0 ? value : undefined
}

const loadRecipes = async () => {
  loading.value = true
  error.value = ''
  success.value = ''

  try {
    recipes.value = await managementApi.listRecipes(normalizeTenant())
  } catch (err) {
    const message = err instanceof Error ? err.message : '配方列表加载失败'
    error.value = message
  } finally {
    loading.value = false
  }
}

const executeRecipe = async (recipe: ManagementRecipe) => {
  executingId.value = recipe.id
  error.value = ''
  success.value = ''

  try {
    const result = await managementApi.executeRecipe({
      tenant: normalizeTenant(),
      recipeId: recipe.id,
      releaseShellContext: true,
    })
    await loadRecipes()
    lastExecution.value = result
    success.value = `已执行：${result.displayName || result.recipeName}（tenant=${result.tenant}）`
  } catch (err) {
    const message = err instanceof Error ? err.message : '配方执行失败'
    error.value = message
  } finally {
    executingId.value = ''
  }
}

onMounted(loadRecipes)
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>配方管理</h3>
      <div class="action-row">
        <label>
          租户
          <input v-model="selectedTenant" placeholder="留空表示当前租户" />
        </label>
        <button class="ghost" @click="loadRecipes" :disabled="loading">刷新</button>
      </div>
    </div>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="success" class="success">{{ success }}</p>

    <article class="panel" v-if="lastExecution">
      <h4>最近执行</h4>
      <div class="form-grid two">
        <div>
          <p class="subtle">Execution Id</p>
          <p>{{ lastExecution.executionId }}</p>
        </div>
        <div>
          <p class="subtle">结果</p>
          <p>{{ lastExecution.result }}</p>
        </div>
        <div>
          <p class="subtle">租户</p>
          <p>{{ lastExecution.tenant }}</p>
        </div>
        <div>
          <p class="subtle">配方</p>
          <p>{{ lastExecution.displayName || lastExecution.recipeName }}</p>
        </div>
      </div>
    </article>

    <article class="panel">
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>显示名</th>
              <th>Name</th>
              <th>文件</th>
              <th>路径</th>
              <th>标签</th>
              <th>说明</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="recipe in recipes" :key="recipe.id">
              <td>{{ recipe.displayName || recipe.name }}</td>
              <td>{{ recipe.name }}</td>
              <td>{{ recipe.fileName }}</td>
              <td>{{ recipe.basePath }}</td>
              <td>{{ recipe.tags.join(', ') || '-' }}</td>
              <td>{{ recipe.description || '-' }}</td>
              <td>
                <button
                  @click="executeRecipe(recipe)"
                  :disabled="executingId === recipe.id || loading"
                >
                  {{ executingId === recipe.id ? '执行中...' : '执行' }}
                </button>
              </td>
            </tr>
            <tr v-if="!recipes.length">
              <td colspan="7" class="subtle">当前无可执行配方</td>
            </tr>
          </tbody>
        </table>
      </div>
    </article>
  </section>
</template>
