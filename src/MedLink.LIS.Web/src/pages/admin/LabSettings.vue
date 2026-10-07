<template>
  <div class="settings-page" data-testid="settingsPage">
    <page-header title="Налаштування лабораторії" icon="fas fa-cog" subtitle="Реквізити, логотип, нумератори, принтер етикеток, бланки, телефон паніки, retention" :breadcrumbs="[{ label: 'Адміністрування' }, { label: 'Налаштування' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <q-btn unelevated dense color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="settingsSave" @click="save" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-lg-6">
        <div class="medlink-card q-pa-md q-mb-md">
          <div class="section-title">Реквізити</div>
          <div class="row q-col-gutter-sm">
            <div class="col-12"><q-input v-model="form.name" outlined dense label="Назва лабораторії *" data-testid="labName" /></div>
            <div class="col-4"><q-input v-model="form.edrpou" outlined dense label="ЄДРПОУ" /></div>
            <div class="col-8"><q-input v-model="form.licenseNumber" outlined dense label="Ліцензія" /></div>
            <div class="col-12"><q-input v-model="form.address" outlined dense label="Адреса" /></div>
            <div class="col-6"><q-input v-model="form.phone" outlined dense label="Телефон" /></div>
            <div class="col-6"><q-input v-model="form.email" outlined dense label="E-mail" /></div>
            <div class="col-6"><q-input v-model="form.directorName" outlined dense label="Керівник / завідувач" /></div>
            <div class="col-6"><q-input v-model="form.workingHours" outlined dense label="Графік роботи" /></div>
            <div class="col-12"><q-input v-model="form.panicPhone" outlined dense label="Телефон для панічних сповіщень (черговий лікар)" data-testid="panicPhone"><template v-slot:prepend><q-icon name="call" color="negative" /></template></q-input></div>
          </div>
        </div>

        <div class="medlink-card q-pa-md q-mb-md">
          <div class="section-title">Логотип бланка</div>
          <div class="row items-center q-col-gutter-md">
            <div class="col-auto">
              <div style="width: 160px; height: 80px; border: 1px dashed #bbb; border-radius: 4px; display: flex; align-items: center; justify-content: center; background: #fff">
                <img v-if="form.logoBase64" :src="logoSrc" style="max-width: 100%; max-height: 100%" alt="логотип">
                <span v-else class="text-grey-5 text-caption">немає</span>
              </div>
            </div>
            <div class="col">
              <q-file v-model="logoFile" outlined dense label="PNG / SVG / JPG до 300 КБ" accept="image/*" @input="readLogo" />
              <q-btn v-if="form.logoBase64" flat dense color="negative" label="Прибрати" class="q-mt-xs" @click="form.logoBase64 = null" />
            </div>
          </div>
        </div>

        <div class="medlink-card q-pa-md q-mb-md">
          <div class="section-title">Нумерація та штрихкоди</div>
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-input v-model="form.orderNumberMask" outlined dense label="Маска номера замовлення" hint="{yyMM}-{000000}" /></div>
            <div class="col-6"><q-input v-model="form.barcodePrefix" outlined dense label="Префікс штрихкоду" /></div>
          </div>
          <div v-if="numerators" class="q-mt-sm">
            <div class="text-caption text-grey-7">Нумератори (/settings/numerators)</div>
            <q-markup-table dense flat bordered>
              <thead><tr><th class="text-left">Лічильник</th><th>Поточне значення</th></tr></thead>
              <tbody>
                <tr v-for="(v, k) in numeratorRows" :key="k"><td>{{ k }}</td><td class="text-center"><q-input v-model.number="numeratorRows[k]" dense borderless type="number" input-class="text-center" /></td></tr>
              </tbody>
            </q-markup-table>
            <q-btn flat dense color="primary" label="Зберегти нумератори" class="q-mt-xs" @click="saveNumerators" />
          </div>
        </div>
      </div>

      <div class="col-12 col-lg-6">
        <div class="medlink-card q-pa-md q-mb-md" data-testid="printerSettings">
          <div class="section-title row items-center justify-between">
            <span><q-icon name="print" class="q-mr-xs" />Принтер етикеток (агент друку localhost:5088)</span>
            <q-badge :color="agentOnline === null ? 'grey-5' : (agentOnline ? 'positive' : 'negative')" :label="agentOnline === null ? 'агент: перевірка…' : (agentOnline ? 'агент онлайн' : 'агент не запущено')" />
          </div>
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-input v-model="form.labelPrinterHost" outlined dense label="Хост / IP принтера (ZPL raw)" placeholder="192.168.1.50" /></div>
            <div class="col-3"><q-input v-model.number="form.labelPrinterPort" outlined dense type="number" label="Порт" placeholder="9100" /></div>
            <div class="col-3 row items-center"><q-btn outline color="primary" icon="print" label="Тестовий друк" class="full-width" :loading="testing" data-testid="printTest" @click="testPrint" /></div>
            <div class="col-12"><q-input v-model="form.labelPrinterName" outlined dense label="або ім’я системного принтера (Windows), якщо хост не задано" placeholder="Zebra ZD220" /></div>
          </div>
          <div class="text-caption text-grey-7 q-mt-xs">Етикетки 40×25 мм (Code128, ZPL). Якщо хост і ім’я не задані — агент друкує на принтер за замовчуванням зі своїх налаштувань.</div>
        </div>

        <div class="medlink-card q-pa-md q-mb-md" data-testid="reportSettings">
          <div class="section-title"><q-icon name="description" class="q-mr-xs" />Бланки результатів</div>
          <q-list dense>
            <q-item tag="label"><q-item-section avatar><q-toggle v-model="form.reportFinalEnabled" color="positive" /></q-item-section><q-item-section><q-item-label>Остаточний бланк</q-item-label><q-item-label caption>підписаний, з QR верифікації (/verify/{token})</q-item-label></q-item-section></q-item>
            <q-item tag="label"><q-item-section avatar><q-toggle v-model="form.reportPreliminaryEnabled" color="orange-7" /></q-item-section><q-item-section><q-item-label>Попередній бланк</q-item-label><q-item-label caption>з водяним знаком «ПОПЕРЕДНІЙ», до верифікації</q-item-label></q-item-section></q-item>
            <q-item tag="label"><q-item-section avatar><q-toggle v-model="form.reportCitoEnabled" color="deep-orange-6" /></q-item-section><q-item-section><q-item-label>CITO-бланк</q-item-label><q-item-label caption>термінова форма з позначкою CITO</q-item-label></q-item-section></q-item>
          </q-list>
          <q-input v-model="form.reportFooter" outlined dense autogrow label="Нижній колонтитул бланка (підписи, застереження)" class="q-mt-sm" />
        </div>

        <div class="medlink-card q-pa-md q-mb-md" data-testid="retentionSettings">
          <div class="section-title"><q-icon name="auto_delete" class="q-mr-xs" />Зберігання журналів (retention)</div>
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-input v-model.number="form.messageRetentionDays" outlined dense type="number" label="Повідомлення аналізаторів, днів" hint="журнал обміну ASTM/HL7" /></div>
            <div class="col-6"><q-input v-model.number="form.connectorLogRetentionDays" outlined dense type="number" label="Логи коннектора, днів" /></div>
          </div>
        </div>

        <div class="medlink-card q-pa-md">
          <div class="section-title">Поточний контекст</div>
          <div class="text-caption">Працюю як: <b>{{ me ? (me.fullName || me.name) : '—' }}</b> · роль {{ roleLabel }} · заголовок <code>X-MedLink-Employee-Id: {{ employeeId || '(не задано — сервер використає Lab:DefaultEmployeeId)' }}</code></div>
          <div class="text-caption q-mt-xs">API: <code>{{ apiBase }}</code> · <a :href="apiBase.replace(/\/api\/v1\/lab$/, '') + '/swagger'" target="_blank" class="text-primary">Swagger</a> · <a :href="apiBase.replace(/\/api\/v1\/lab$/, '') + '/health'" target="_blank" class="text-primary">/health</a></div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { ROLE } from '../../utils/statuses';
import { API_BASE } from '../../services/http';
import { printTest, printerFromSettings, agentStatus, AGENT_UNREACHABLE_MESSAGE } from '../../services/printAgentService';

export default {
  name: 'LabSettings',
  mixins: [apiMixin],
  data () {
    return {
      form: { reportFinalEnabled: true, reportPreliminaryEnabled: true, reportCitoEnabled: true, messageRetentionDays: 90, connectorLogRetentionDays: 30, labelPrinterPort: 9100 },
      numerators: null, numeratorRows: {}, logoFile: null, saving: false, testing: false, agentOnline: null
    };
  },
  computed: {
    me () { return this.$store.getters['context/currentEmployee']; },
    roleLabel () { return ROLE[this.$store.getters['context/currentRole']] || '—'; },
    employeeId () { return this.$store.state.context.employeeId; },
    apiBase () { return API_BASE.startsWith('http') ? API_BASE : window.location.origin + API_BASE; },
    logoSrc () { const b = this.form.logoBase64 || ''; return b.startsWith('data:') ? b : `data:image/png;base64,${b}`; }
  },
  created () { this.load(); agentStatus().then(s => { this.agentOnline = s.online; }); },
  methods: {
    async load () {
      const res = await this.callApi(() => this.$api.labSettings());
      if (res) this.form = { ...this.form, ...res };
      try { this.numerators = await this.$api.numerators(); this.numeratorRows = Array.isArray(this.numerators) ? Object.fromEntries(this.numerators.map(n => [n.name || n.key, n.value || n.current])) : { ...this.numerators }; } catch (e) { this.numerators = null; }
    },
    async save () {
      this.saving = true;
      try { const res = await this.$api.saveLabSettings(this.form); if (res) this.form = { ...this.form, ...res }; this.notifyOk('Налаштування збережено'); this.$store.commit('context/SET_LAB_SETTINGS', this.form); this.$store.dispatch('context/loadMe'); } catch (e) { this.notifyError(e); } finally { this.saving = false; }
    },
    async saveNumerators () { try { await this.$api.saveNumerators(this.numeratorRows); this.notifyOk('Нумератори збережено'); } catch (e) { this.notifyError(e); } },
    readLogo (file) {
      if (!file) return;
      if (file.size > 300 * 1024) { this.$q.notify({ type: 'warning', message: 'Файл понад 300 КБ' }); return; }
      const reader = new FileReader();
      reader.onload = () => { this.form.logoBase64 = reader.result; };
      reader.readAsDataURL(file);
    },
    async testPrint () {
      this.testing = true;
      try { await printTest(printerFromSettings(this.form)); this.notifyOk('Тестову етикетку надіслано на принтер'); this.agentOnline = true; } catch (e) { this.agentOnline = !e.agentUnreachable; this.$q.notify({ type: 'warning', icon: 'print_disabled', message: e.agentUnreachable ? AGENT_UNREACHABLE_MESSAGE : e.message, timeout: 7000 }); } finally { this.testing = false; }
    }
  }
};
</script>
