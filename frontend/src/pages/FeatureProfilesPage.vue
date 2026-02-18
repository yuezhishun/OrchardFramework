<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

import AppModal from '../components/AppModal.vue'
import { managementApi } from '../api/service'
import type { FeatureProfileItem } from '../api/types'

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')
const profiles = ref<FeatureProfileItem[]>([])
const editingId = ref<string | null>(null)
const isProfileFormModalOpen = ref(false)

const form = reactive({
  id: '',
  name: '',
  rulesJson: '[]',
})

const resetForm = () => {
  editingId.value = null
  form.id = ''
  form.name = ''
  form.rulesJson = '[]'
}

const handleProfileFormModalToggle = (open: boolean) => {
  isProfileFormModalOpen.value = open
  if (!open) {
    resetForm()
  }
}

const openCreateProfileForm = () => {
  resetForm()
  isProfileFormModalOpen.value = true
}

const loadProfiles = async () => {
  loading.value = true
  error.value = ''

  try {
    profiles.value = await managementApi.listFeatureProfiles()
  } catch {
    error.value = '功能配置模板读取失败。'
  } finally {
    loading.value = false
  }
}

const editProfile = (profile: FeatureProfileItem) => {
  editingId.value = profile.id
  form.id = profile.id
  form.name = profile.name
  form.rulesJson = JSON.stringify(profile.featureRules, null, 2)
  isProfileFormModalOpen.value = true
}

const saveProfile = async () => {
  saving.value = true
  error.value = ''
  message.value = ''

  try {
    const parsedRules = JSON.parse(form.rulesJson || '[]')
    if (!Array.isArray(parsedRules)) {
      throw new Error('invalid rules')
    }

    await managementApi.upsertFeatureProfile({
      id: form.id.trim(),
      name: form.name.trim(),
      featureRules: parsedRules,
    })
    message.value = `模板 ${form.id.trim()} 已保存。`
    handleProfileFormModalToggle(false)
    await loadProfiles()
  } catch {
    error.value = '保存失败。请确认规则 JSON 为数组，且每项包含 rule/expression。'
  } finally {
    saving.value = false
  }
}

const removeProfile = async (profile: FeatureProfileItem) => {
  if (!window.confirm(`确认删除模板 ${profile.id} ?`)) {
    return
  }

  error.value = ''
  message.value = ''

  try {
    await managementApi.upsertFeatureProfile({
      id: profile.id,
      featureRules: [],
      delete: true,
    })
    message.value = `模板 ${profile.id} 已删除。`
    if (editingId.value === profile.id) {
      handleProfileFormModalToggle(false)
    }
    await loadProfiles()
  } catch {
    error.value = '模板删除失败。'
  }
}

onMounted(loadProfiles)
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>租户功能配置模板（H2）</h3>
      <div class="action-row">
        <button class="ghost" @click="loadProfiles" :disabled="loading">刷新</button>
        <button @click="openCreateProfileForm">新建模板</button>
      </div>
    </div>

    <AppModal
      :model-value="isProfileFormModalOpen"
      :title="editingId ? `编辑模板：${editingId}` : '新建模板'"
      max-width="900px"
      @update:model-value="handleProfileFormModalToggle"
    >
      <form class="panel modal-form" @submit.prevent="saveProfile">
        <div class="form-grid two">
          <label>
            模板 ID
            <input v-model="form.id" :disabled="!!editingId" required />
          </label>
          <label>
            显示名称
            <input v-model="form.name" />
          </label>
        </div>

        <label>
          规则 JSON（数组）
          <textarea
            v-model="form.rulesJson"
            rows="9"
            placeholder='[{"rule":"AlwaysEnabled","expression":"OrchardCore.Admin"}]'
          />
        </label>

        <div class="action-row">
          <button type="submit" :disabled="saving">{{ editingId ? '保存修改' : '创建模板' }}</button>
          <button type="button" class="ghost" @click="handleProfileFormModalToggle(false)">取消</button>
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
            <th>名称</th>
            <th>规则数</th>
            <th>已分配租户</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="profile in profiles" :key="profile.id">
            <td>{{ profile.id }}</td>
            <td>{{ profile.name }}</td>
            <td>{{ profile.featureRules.length }}</td>
            <td>{{ profile.assignedTenants.length ? profile.assignedTenants.join(', ') : '-' }}</td>
            <td class="action-row">
              <button class="ghost" @click="editProfile(profile)">编辑</button>
              <button class="danger" @click="removeProfile(profile)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
