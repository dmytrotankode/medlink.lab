<!--
  ТЗ 21.13 (FR-SGN-005…014): блок «ЕСОЗ» картки замовлення — статус звіту, підготовка пакета DiagnosticReport,
  підпис КЕП і відправка, повтор збереженого підписаного пакета, анулювання з КЕП, історія відправок.
-->
<template>
  <div class="medlink-card q-mb-md" data-testid="ehealthPanel">
    <div class="medlink-card__title">
      <span><q-icon name="cloud_upload" class="q-mr-xs" />ЕСОЗ: діагностичний звіт</span>
      <q-badge :color="color" :label="info.label || '—'" />
    </div>
    <div class="q-pa-sm">
      <div v-if="info.problems && info.problems.length" class="text-caption text-grey-7 q-mb-sm">Відправка недоступна: {{ info.problems.join('; ') }}</div>
      <div class="row q-gutter-sm">
        <q-btn v-if="info.canSubmit" unelevated dense color="primary" icon="draw" label="Підписати КЕП і відправити" :loading="busy" data-testid="ehealthSubmit" @click="submit" />
        <q-btn v-if="['ERROR', 'AUTH_REQUIRED'].includes(info.status)" outline dense color="primary" icon="replay" label="Повторити відправку" :loading="busy" @click="retry" />
        <q-btn v-if="info.canAnnul" flat dense color="negative" icon="block" label="Анулювати" :loading="busy" @click="annul" />
        <q-btn v-if="sessionLevel !== 'L2'" flat dense color="grey-8" icon="shield" label="Вхід в ЕСОЗ" @click="$root.$emit('ehealth-login')" />
      </div>
      <q-list v-if="info.submissions && info.submissions.length" dense separator class="q-mt-sm">
        <q-item v-for="s in info.submissions" :key="s.id">
          <q-item-section>
            <q-item-label>{{ s.kind === 'ANNUL' ? 'Анулювання' : 'Відправка' }} · {{ s.label }}<span v-if="s.attempts"> · спроб {{ s.attempts }}</span></q-item-label>
            <q-item-label caption>
              <span v-if="s.signedByName">Підписав {{ s.signedByName }} {{ s.signedOn | datetime }} · {{ s.certSerial }}</span>
              <span v-if="s.ehealthReportId"> · ID ЕСОЗ {{ s.ehealthReportId.slice(0, 8) }}…</span>
              <span v-if="s.lastError" class="text-negative"> · {{ s.lastError }}</span>
            </q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
    </div>
  </div>
</template>

<script>
import { describeApiError } from '../../services/http';

const COLORS = { NOT_SENT: 'grey-6', READY_TO_SIGN: 'orange-7', SIGNED: 'orange-7', PROCESSING: 'primary', REGISTERED: 'teal-6', COMPLETED: 'positive', ERROR: 'negative', AUTH_REQUIRED: 'deep-orange-6', ANNULLED: 'grey-7', REJECTED: 'negative' };

export default {
  name: 'EhealthSubmitPanel',
  props: { orderId: { type: String, required: true } },
  data () { return { info: {}, busy: false, session: {} }; },
  computed: {
    color () { return COLORS[this.info.status] || 'grey-6'; },
    sessionLevel () { return this.session.level || 'L0'; }
  },
  created () {
    this.load();
    this.$root.$on('ehealth-status', this.onSession);
    this.$api.ehealthSession().then(this.onSession).catch(() => {});
  },
  beforeDestroy () { this.$root.$off('ehealth-status', this.onSession); },
  methods: {
    onSession (s) { this.session = s || {}; },
    async load () { try { this.info = await this.$api.ehealthOrderStatus(this.orderId); } catch (e) { this.info = {}; } },
    async run (fn, ok) {
      this.busy = true;
      try {
        const r = await fn();
        if (ok) this.$q.notify({ type: r && r.ehealthStatus === 'ERROR' ? 'warning' : 'positive', message: r && r.label ? `${ok}: ${r.label}` : ok });
      } catch (e) {
        const info = describeApiError(e);
        this.$q.notify({ type: 'negative', message: info.message, timeout: 5000 });
        if (e.apiStatus === 401 || e.apiStatus === 403) this.$root.$emit('ehealth-login');
      } finally {
        this.busy = false;
        this.load();
        this.$root.$emit('ehealth-refresh');
      }
    },
    // Тестовий режим: підпис імітується віджетом — base64 від хешу пакета з даними тестового сертифіката
    sign (hash) { return btoa(`CAdES-DEMO:${hash}:${Date.now()}`); },
    submit () {
      return this.run(async () => {
        const prepared = await this.$api.ehealthPrepare(this.orderId);
        return this.$api.ehealthSubmit(this.orderId, { signedData: this.sign(prepared.submission.packageHash), signer: this.session.testCertificate });
      }, 'Звіт відправлено');
    },
    retry () { return this.run(() => this.$api.ehealthRetry(this.orderId), 'Повтор відправки'); },
    annul () {
      this.$q.dialog({ title: 'Анулювати звіт в ЕСОЗ?', message: 'Анулювання підписується КЕП; буде створено новий звіт для повторної відправки', prompt: { model: '', type: 'text', label: 'Причина' }, cancel: true })
        .onOk(reason => {
          if (!reason) return;
          this.run(() => this.$api.ehealthAnnul(this.orderId, { signedData: this.sign('annul'), signer: this.session.testCertificate, reason }), 'Звіт анульовано');
        });
    }
  }
};
</script>
