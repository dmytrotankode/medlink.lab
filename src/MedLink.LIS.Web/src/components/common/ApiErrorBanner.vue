<template>
  <q-banner v-if="offline || message" dense rounded class="api-error-banner q-mb-md" :class="offline ? 'api-error-banner--offline' : 'api-error-banner--error'">
    <template v-slot:avatar>
      <q-icon :name="offline ? 'cloud_off' : 'error_outline'" :color="offline ? 'warning' : 'negative'" size="26px" />
    </template>
    <div class="text-weight-bold">
      {{ offline ? 'API лабораторії недоступне' : 'Не вдалося виконати дію' }}
    </div>
    <div class="text-caption">
      <template v-if="offline">
        Сервіс MedLink.LIS.Api не відповідає ({{ apiBase }}). Сторінка показана без даних — дії не виконуються, результати не імітуються.
      </template>
      <template v-else>{{ message }}</template>
    </div>
    <template v-slot:action>
      <q-btn v-if="offline || retryable" flat dense color="primary" icon="refresh" label="Повторити" @click="$emit('retry')" />
      <q-btn v-if="!offline && $listeners.dismiss" flat dense round color="grey-7" icon="close" aria-label="Закрити" @click="$emit('dismiss')"><q-tooltip>Закрити</q-tooltip></q-btn>
    </template>
  </q-banner>
</template>

<script>
import { API_BASE } from '../../services/http';

export default {
  name: 'ApiErrorBanner',
  props: {
    message: { type: String, default: '' },
    /** false — повтор запиту не допоможе (помилка перевірки, права): кнопку «Повторити» не показуємо */
    retryable: { type: Boolean, default: true },
    offlineOnly: { type: Boolean, default: false }
  },
  computed: {
    offline () { return this.$store.state.laboratory.apiOffline; },
    apiBase () { return API_BASE; }
  }
};
</script>

<style lang="stylus" scoped>
.api-error-banner
  border 1px solid
  background #fff
.api-error-banner--offline
  border-color rgba(242, 192, 55, 0.8)
  background #fffbe6
.api-error-banner--error
  border-color rgba(208, 79, 69, 0.6)
  background #fdf2f2
</style>
