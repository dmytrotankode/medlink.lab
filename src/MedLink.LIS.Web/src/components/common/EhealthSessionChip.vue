<!--
  ТЗ 21.13 (FR-SGN-001…004): індикатор «Сесія ЄСОЗ» у шапці — «не підключено» / «сесія активна» / «проксі недоступний».
  Вхід виконується через eHealth-сервіс MedLink: кнопка «Увійти в ЄСОЗ» відкриває сторінку авторизації ЄСОЗ, після повернення
  на сторінку ЛІС код обмінюється на токен у проксі (ЛІС токена не бачить). Підтвердження особи для відправки звіту — підпис КЕП.
  Компонент можна відкрити програмно: this.$root.$emit('ehealth-login') (напр. з панелі відправки звіту).
-->
<template>
  <div class="row items-center no-wrap">
    <div class="header-chip cursor-pointer" :class="chipClass" data-testid="ehealthChip" @click="open">
      <q-icon :name="state === 'active' ? 'verified_user' : state === 'none' ? 'login' : 'shield'" size="15px" />
      <span class="gt-sm">{{ status.label || 'ЄСОЗ: не підключено' }}</span>
      <q-tooltip>Сесія ЄСОЗ користувача для підпису й відправки звітів{{ status.simulated ? ' (тестовий режим: проксі імітується)' : '' }}</q-tooltip>
    </div>

    <q-dialog v-model="dlg">
      <q-card style="width: 440px" data-testid="ehealthLoginDialog">
        <q-card-section class="text-subtitle1 text-weight-bold"><q-icon name="shield" color="primary" class="q-mr-xs" />Сесія ЄСОЗ</q-card-section>

        <q-card-section v-if="state === 'active'" class="q-pt-none">
          <div><q-icon name="verified_user" color="positive" /> Ви увійшли в ЄСОЗ</div>
          <div class="text-caption text-grey-7 q-mt-xs">Токен доступу зберігає сервіс MedLink; ЛІС його не бачить. Для відправки звіту підпишіть його КЕП.</div>
        </q-card-section>

        <q-card-section v-else-if="state === 'none'" class="q-pt-none">
          <div class="text-body2">Щоб відправляти результати в ЄСОЗ, увійдіть у свій обліковий запис ЄСОЗ.</div>
          <div class="text-caption text-grey-7 q-mt-xs">Відкриється сторінка ЄСОЗ для входу; пароль вводиться лише там. Після входу ви повернетесь у ЛІС.</div>
          <div v-if="status.simulated" class="text-caption text-grey-7 q-mt-xs">Тестовий режим: сторінка ЄСОЗ не відкривається, вхід виконується автоматично.</div>
          <div v-if="status.simulated && status.testCertificate" class="text-caption bg-grey-2 q-pa-sm rounded-borders q-mt-sm">
            Тестовий сертифікат КЕП: <b>{{ status.testCertificate.name }}</b><br>ДРФО {{ status.testCertificate.drfo }} · {{ status.testCertificate.certSerial }}
          </div>
          <div v-if="status.loginAvailable === false" class="text-caption text-orange-9 q-mt-sm">Вхід ще не налаштовано: не задано адресу сторінки авторизації ЄСОЗ.</div>
        </q-card-section>

        <q-card-section v-else class="q-pt-none">
          <div class="text-negative"><q-icon name="cloud_off" /> {{ status.label }}</div>
          <div v-if="status.detail" class="text-caption text-grey-7 q-mt-xs">{{ status.detail }}</div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn v-if="state === 'active'" flat color="negative" label="Вийти з ЄСОЗ" :loading="busy" @click="doLogout" />
          <q-btn v-if="state === 'active'" flat label="Оновити сесію" :loading="busy" @click="doRefresh" />
          <q-space />
          <q-btn flat label="Закрити" v-close-popup />
          <q-btn v-if="state === 'none'" unelevated color="primary" icon="login" label="Увійти в ЄСОЗ" :loading="busy" :disable="status.loginAvailable === false" data-testid="ehealthSignIn" @click="doLogin" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { describeApiError } from '../../services/http';

export default {
  name: 'EhealthSessionChip',
  data () {
    return { status: {}, dlg: false, busy: false, timer: null };
  },
  computed: {
    state () { return this.status.state || 'none'; },
    chipClass () { return this.state === 'active' ? 'header-chip--ok' : this.state === 'none' ? 'header-chip--muted' : 'header-chip--warn'; }
  },
  created () {
    this.refresh();
    this.timer = setInterval(this.refresh, 60000);
    this.$root.$on('ehealth-login', this.open);
    this.$root.$on('ehealth-refresh', this.refresh);
  },
  beforeDestroy () {
    clearInterval(this.timer);
    this.$root.$off('ehealth-login', this.open);
    this.$root.$off('ehealth-refresh', this.refresh);
  },
  methods: {
    async refresh () {
      try { this.status = await this.$api.ehealthSession(); this.$root.$emit('ehealth-status', this.status); } catch (e) { /* API недоступне */ }
    },
    open () { this.dlg = true; this.refresh(); },
    async run (fn, ok) {
      this.busy = true;
      try {
        await fn();
        if (ok) this.$q.notify({ type: 'positive', message: ok });
        await this.refresh();
      } catch (e) {
        this.$q.notify({ type: 'negative', message: describeApiError(e).message, timeout: 5000 });
      } finally { this.busy = false; }
    },
    doLogin () {
      return this.run(async () => {
        const link = await this.$api.ehealthAuthLink();
        if (link.simulated) { await this.$api.ehealthAuthCallback({ code: link.code }); return; }
        try { sessionStorage.setItem('ehealthReturn', this.$route.fullPath); } catch (e) { /* сховище недоступне */ }
        window.location.href = link.url; // сторінка авторизації ЄСОЗ; повернення — на сторінку ЛІС «Вхід в ЄСОЗ» із ?code=…
      }, null);
    },
    doRefresh () { return this.run(() => this.$api.ehealthRefresh(), 'Сесію ЄСОЗ оновлено'); },
    doLogout () { return this.run(async () => { await this.$api.ehealthLogout(); this.dlg = false; }, 'Ви вийшли з ЄСОЗ'); }
  }
};
</script>
