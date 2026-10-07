<template>
  <div class="verify-page flex flex-center" style="min-height: 100vh; background: #f5f5f5">
    <div class="medlink-card q-pa-lg text-center" style="max-width: 480px; width: 100%" data-testid="verifyPage">
      <div class="brand-gradient-line q-mb-md" />
      <q-icon name="fas fa-flask" color="primary" size="40px" />
      <div class="text-h6 text-weight-bold q-mt-sm">Перевірка автентичності бланка</div>
      <div v-if="loading" class="q-pa-lg"><q-spinner color="primary" size="36px" /></div>
      <div v-else-if="result && result.valid" class="q-mt-md">
        <q-icon name="verified" color="positive" size="64px" />
        <div class="text-subtitle1 text-positive text-weight-bold">Бланк справжній</div>
        <q-markup-table dense flat class="q-mt-sm text-left">
          <tbody>
            <tr><td class="text-grey-7">Номер замовлення</td><td class="text-weight-bold">{{ result.orderNumber }}</td></tr>
            <tr><td class="text-grey-7">Видано</td><td>{{ result.releasedAt | datetime }}</td></tr>
            <tr><td class="text-grey-7">Лабораторія</td><td>{{ result.lab && (result.lab.name || result.lab) }}</td></tr>
          </tbody>
        </q-markup-table>
        <div class="text-caption text-grey-6 q-mt-sm">Персональні дані не розкриваються. Для порівняння звірте номер замовлення та дату видачі з паперовим бланком.</div>
      </div>
      <div v-else class="q-mt-md">
        <q-icon name="gpp_bad" color="negative" size="64px" />
        <div class="text-subtitle1 text-negative text-weight-bold">Бланк не підтверджено</div>
        <div class="text-caption text-grey-7">{{ error || 'Токен недійсний або бланк не видавався цією лабораторією.' }}</div>
      </div>
      <q-btn flat color="primary" label="Повторити перевірку" class="q-mt-md" @click="load" />
    </div>
  </div>
</template>

<script>
export default {
  name: 'VerifyReport',
  data () { return { result: null, loading: false, error: '' }; },
  created () { this.load(); },
  methods: {
    async load () {
      this.loading = true; this.error = ''; this.result = null;
      try { this.result = await this.$api.verifyToken(this.$route.params.token); } catch (e) { this.error = e.userMessage || ''; } finally { this.loading = false; }
    }
  }
};
</script>
