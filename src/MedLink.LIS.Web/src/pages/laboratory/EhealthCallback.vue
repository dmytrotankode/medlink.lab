<!--
  Сторінка-призначення входу в ЄСОЗ (redirect_uri): ЄСОЗ повертає сюди ?code=…, ЛІС передає код eHealth-сервісу MedLink (він обмінює його на токен і зберігає),
  після чого користувач повертається на сторінку, з якої почав вхід. Код і токен ніде в ЛІС не зберігаються.
-->
<template>
  <q-page padding class="flex flex-center" data-testid="ehealthCallbackPage">
    <div class="text-center" style="max-width: 460px">
      <q-spinner v-if="busy" size="40px" color="primary" />
      <q-icon v-else-if="ok" name="verified_user" size="44px" color="positive" />
      <q-icon v-else name="error_outline" size="44px" color="negative" />
      <div class="text-subtitle1 q-mt-md" data-testid="ehealthCallbackText">{{ message }}</div>
      <q-btn v-if="!busy" class="q-mt-md" unelevated color="primary" :label="ok ? 'Продовжити' : 'До робочого столу'" @click="back" />
    </div>
  </q-page>
</template>

<script>
import { describeApiError } from '../../services/http';

export default {
  name: 'EhealthCallback',
  data () { return { busy: true, ok: false, message: 'Завершуємо вхід в ЄСОЗ…' }; },
  async created () {
    const q = this.$route.query || {};
    if (q.error || !q.code) {
      this.busy = false;
      this.message = q.error_description || (q.error ? 'ЄСОЗ відхилила вхід.' : 'ЄСОЗ не передала код авторизації. Повторіть вхід.');
      return;
    }
    try {
      await this.$api.ehealthAuthCallback({ code: String(q.code) });
      this.ok = true;
      this.message = 'Вхід в ЄСОЗ виконано. Тепер можна підписувати й відправляти звіти.';
      this.$root.$emit('ehealth-refresh');
    } catch (e) {
      this.message = describeApiError(e).message;
    } finally { this.busy = false; }
  },
  methods: {
    back () {
      let to = '/laboratory/dashboard';
      try { const saved = sessionStorage.getItem('ehealthReturn'); if (saved) { to = saved; sessionStorage.removeItem('ehealthReturn'); } } catch (e) { /* сховище недоступне */ }
      this.$router.replace(to).catch(() => {});
    }
  }
};
</script>
