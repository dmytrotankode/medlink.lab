<!--
  ТЗ 21.13 (FR-SGN-001…004): індикатор сесії ЕСОЗ у шапці — «не підключено» / «пароль введено» / «підтверджено до HH:mm».
  Вхід: логін і пароль ЕСОЗ (L1), далі обов'язкове підтвердження особи КЕП або 2FA (TOTP) → L2. Пароль ніде не зберігається.
  Компонент можна відкрити програмно: this.$root.$emit('ehealth-login') (напр. з панелі відправки звіту).
-->
<template>
  <div class="row items-center no-wrap">
    <div class="header-chip cursor-pointer" :class="chipClass" data-testid="ehealthChip" @click="open">
      <q-icon :name="level === 'L2' ? 'verified_user' : level === 'L1' ? 'gpp_maybe' : 'shield'" size="15px" />
      <span class="gt-sm">{{ status.label || 'ЕСОЗ: не підключено' }}</span>
      <q-tooltip>Сесія ЕСОЗ користувача для підпису й відправки звітів{{ status.mode === 'MOCK' ? ' (тестовий режим)' : '' }}</q-tooltip>
    </div>

    <q-dialog v-model="dlg">
      <q-card style="width: 440px" data-testid="ehealthLoginDialog">
        <q-card-section class="text-subtitle1 text-weight-bold"><q-icon name="shield" color="primary" class="q-mr-xs" />Вхід в ЕСОЗ</q-card-section>

        <q-card-section v-if="level === 'L0'" class="q-gutter-sm q-pt-none">
          <q-input v-model="login" dense outlined autofocus label="Ел. пошта або логін ЕСОЗ" autocomplete="username" />
          <q-input v-model="password" dense outlined type="password" label="Пароль ЕСОЗ" autocomplete="current-password" @keyup.enter="doLogin" />
          <div class="text-caption text-grey-7">Пароль передається лише шлюзу ЕСОЗ і не зберігається в ЛІС. Після входу потрібне підтвердження особи.</div>
        </q-card-section>

        <q-card-section v-else-if="level === 'L1'" class="q-pt-none">
          <div class="text-caption q-mb-sm">Підтвердіть особу: КЕП (кваліфікований електронний підпис) або код із додатка автентифікації (двофакторний вхід).</div>
          <q-tabs v-model="method" dense align="left" active-color="primary" class="text-grey-8">
            <q-tab name="KEP" label="КЕП" data-testid="ehealthTabKep" />
            <q-tab name="TOTP" label="Код з додатка" data-testid="ehealthTabTotp" />
          </q-tabs>
          <div v-if="method === 'KEP'" class="q-pt-sm">
            <div v-if="status.testCertificate" class="text-caption bg-grey-2 q-pa-sm rounded-borders">
              Тестовий сертифікат: <b>{{ status.testCertificate.name }}</b><br>ДРФО {{ status.testCertificate.drfo }} · {{ status.testCertificate.certSerial }}
            </div>
            <div class="text-caption text-grey-7 q-mt-xs">Приватний ключ не залишає пристрій; ЛІС отримує лише підпис і дані сертифіката.</div>
          </div>
          <div v-else class="q-pt-sm q-gutter-sm">
            <q-input v-model="code" dense outlined label="Код з додатка" data-testid="ehealthTotpCode" mask="######" @keyup.enter="doConfirm" />
            <div v-if="!status.totpEnabled" class="text-caption text-orange-9">Двофакторний вхід вимкнено для облікового запису. <a href="#" data-testid="ehealthEnableTotp" @click.prevent="enableTotp">Увімкнути</a></div>
            <div v-if="status.demoTotpCode" class="text-caption text-grey-7">Тестовий режим: код {{ status.demoTotpCode }}</div>
          </div>
        </q-card-section>

        <q-card-section v-else class="q-pt-none">
          <div><q-icon name="verified_user" color="positive" /> {{ status.label }}</div>
          <div class="text-caption text-grey-7">{{ status.login }} · спосіб: {{ status.method }}<span v-if="status.signerName"> · {{ status.signerName }}</span></div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn v-if="level !== 'L0'" flat color="negative" label="Вийти з ЕСОЗ" @click="doLogout" />
          <q-space />
          <q-btn flat label="Закрити" v-close-popup />
          <q-btn v-if="level === 'L0'" unelevated color="primary" label="Увійти" :loading="busy" :disable="!login || !password" @click="doLogin">
            <q-tooltip v-if="!login || !password">Введіть логін і пароль</q-tooltip>
          </q-btn>
          <q-btn v-if="level === 'L1'" unelevated color="primary" :label="method === 'KEP' ? 'Підписати КЕП' : 'Підтвердити'" data-testid="ehealthConfirm" :loading="busy" @click="doConfirm" />
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
    return { status: {}, dlg: false, login: '', password: '', method: 'KEP', code: '', busy: false, timer: null };
  },
  computed: {
    level () { return this.status.level || 'L0'; },
    chipClass () { return this.level === 'L2' ? 'header-chip--ok' : this.level === 'L1' ? 'header-chip--warn' : 'header-chip--muted'; }
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
    doLogin () { return this.run(async () => { await this.$api.ehealthLogin({ login: this.login, password: this.password }); this.password = ''; }, 'Пароль прийнято — підтвердіть особу'); },
    doConfirm () {
      const body = this.method === 'KEP' ? { method: 'KEP', signer: this.status.testCertificate } : { method: 'TOTP', code: this.code };
      return this.run(async () => { await this.$api.ehealthConfirm(body); this.code = ''; this.dlg = false; }, 'Особу підтверджено');
    },
    enableTotp () { return this.run(() => this.$api.ehealthEnableTotp(), 'Двофакторний вхід увімкнено'); },
    doLogout () { return this.run(async () => { await this.$api.ehealthLogout(); this.dlg = false; }, 'Сесію ЕСОЗ завершено'); }
  }
};
</script>
