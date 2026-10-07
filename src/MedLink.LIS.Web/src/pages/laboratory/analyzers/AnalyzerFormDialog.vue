<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 860px; max-width: 98vw" data-testid="analyzerDialog">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="memory" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Аналізатор' : 'Новий аналізатор' }}</div>
        <q-space /><q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section style="max-height: 72vh; overflow: auto">
        <div class="section-title">Основне</div>
        <div class="row q-col-gutter-sm">
          <div class="col-3"><q-input v-model="form.code" outlined dense label="Код *" data-testid="anCode" /></div>
          <div class="col-5"><q-input v-model="form.name" outlined dense label="Назва *" data-testid="anName" /></div>
          <div class="col-4">
            <q-select v-model="form.analyzerTypeId" outlined dense label="Модель (тип) *" :options="typeOptions" emit-value map-options use-input input-debounce="0" @filter="filterTypes" data-testid="anType">
              <template v-slot:option="scope"><q-item v-bind="scope.itemProps" v-on="scope.itemEvents"><q-item-section><q-item-label>{{ scope.opt.label }}</q-item-label><q-item-label caption>{{ scope.opt.item.manufacturer }} · {{ scope.opt.item.exchType }} · {{ scope.opt.item.orderTemplate }}</q-item-label></q-item-section></q-item></template>
            </q-select>
          </div>
          <div class="col-4"><q-input :value="protocol" outlined dense readonly label="Протокол (з типу)" bg-color="grey-2" /></div>
          <div class="col-4"><q-select v-model="form.connectorId" outlined dense clearable label="Коннектор (інсталяція)" :options="connectorOptions" emit-value map-options /></div>
          <div class="col-4"><q-select v-model="form.departmentId" outlined dense clearable label="Відділення" :options="departmentOptions" emit-value map-options /></div>
          <div class="col-4"><q-toggle v-model="form.autoQueryOrders" label="Запит замовлень за штрихкодом (host query)" /></div>
          <div class="col-4"><q-toggle v-model="form.isActive" label="Активний" color="positive" /></div>
        </div>

        <div class="section-title q-mt-md">Підключення</div>
        <q-btn-toggle v-model="form.connectionMode" spread no-caps unelevated toggle-color="primary" :options="[{ value: 'TCP', label: 'TCP/IP' }, { value: 'COM', label: 'COM (RS-232)' }, { value: 'FILE', label: 'Файл / папка' }]" class="q-mb-sm" data-testid="anMode" />
        <div v-if="form.connectionMode === 'TCP'" class="row q-col-gutter-sm">
          <div class="col-5"><q-input v-model="form.tcpHost" outlined dense label="Хост / IP" data-testid="anHost" /></div>
          <div class="col-3"><q-input v-model.number="form.tcpPort" outlined dense type="number" label="Порт" data-testid="anPort" /></div>
          <div class="col-4"><q-toggle v-model="form.isTcpServer" label="Коннектор — сервер (прилад підключається до ПК)" /></div>
        </div>
        <div v-else-if="form.connectionMode === 'COM'" class="row q-col-gutter-sm">
          <div class="col-2"><q-input v-model="form.comPort" outlined dense label="Порт (COM3)" /></div>
          <div class="col-2"><q-select v-model="form.baudRate" outlined dense label="Швидкість" :options="[1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200]" /></div>
          <div class="col-2"><q-select v-model="form.parity" outlined dense label="Парність" :options="['None', 'Even', 'Odd', 'Mark', 'Space']" /></div>
          <div class="col-2"><q-select v-model="form.dataBits" outlined dense label="Біти даних" :options="[7, 8]" /></div>
          <div class="col-2"><q-select v-model="form.stopBits" outlined dense label="Стоп-біти" :options="['One', 'OneAndHalf', 'Two']" /></div>
          <div class="col-2"><q-select v-model="form.flowControl" outlined dense label="Flow control" :options="['None', 'XonXoff', 'Hardware']" /></div>
        </div>
        <div v-else class="row q-col-gutter-sm">
          <div class="col-8"><q-input v-model="form.filePath" outlined dense label="Шлях до папки / файлу обміну" /></div>
          <div class="col-4"><q-input v-model.number="form.filePollSec" outlined dense type="number" label="Опитування, с" /></div>
        </div>

        <q-expansion-item dense class="q-mt-md medlink-card" icon="tune" label="Рамкування та протокол (розширено)" header-class="text-weight-bold">
          <div class="row q-col-gutter-sm q-pa-sm">
            <div class="col-3"><q-input v-model="form.bopBase64" outlined dense label="BOP (base64)" :placeholder="typeDefault('bopBase64')" /></div>
            <div class="col-3"><q-input v-model="form.eopBase64" outlined dense label="EOP (base64)" :placeholder="typeDefault('eopBase64')" /></div>
            <div class="col-2"><q-input v-model.number="form.sleepMs" outlined dense type="number" label="Пауза, мс" :placeholder="String(typeDefault('sleepMs') || '')" /></div>
            <div class="col-2"><q-input v-model.number="form.maxFrameLen" outlined dense type="number" label="Макс. кадр" placeholder="240" /></div>
            <div class="col-2"><q-toggle v-model="form.checksum" label="Контрольна сума" /></div>
            <div class="col-3"><q-toggle v-model="form.ackAfterRecord" label="ACK після запису" /></div>
            <div class="col-5"><q-input v-model="form.orderTemplate" outlined dense label="Шаблон замовлення (builder)" :placeholder="typeDefault('orderTemplate')" /></div>
          </div>
        </q-expansion-item>

        <div class="section-title q-mt-md row items-center justify-between">
          <span>Карта параметрів (код приладу → показник ЛІС)</span>
          <q-btn dense flat size="sm" color="primary" icon="add" label="Додати" @click="form.parameterMap.push({ analyzerCode: '', testCode: null, factor: 1, offset: 0, unitOverride: '' })" />
        </div>
        <q-markup-table dense flat bordered data-testid="paramMap">
          <thead><tr><th class="text-left">Код на приладі</th><th class="text-left">Показник ЛІС</th><th>Множник</th><th>Зсув</th><th class="text-left">Одиниця (override)</th><th /></tr></thead>
          <tbody>
            <tr v-for="(p, i) in form.parameterMap" :key="i">
              <td><q-input v-model="p.analyzerCode" dense borderless placeholder="WBC" /></td>
              <td style="min-width: 220px"><q-select v-model="p.testCode" dense borderless :options="testCodeOptions" emit-value map-options use-input input-debounce="0" @filter="filterTests" /></td>
              <td><q-input v-model.number="p.factor" dense borderless type="number" step="0.001" input-class="text-right" /></td>
              <td><q-input v-model.number="p.offset" dense borderless type="number" step="0.001" input-class="text-right" /></td>
              <td><q-input v-model="p.unitOverride" dense borderless /></td>
              <td class="text-right"><q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="form.parameterMap.splice(i, 1)" /></td>
            </tr>
            <tr v-if="!form.parameterMap.length"><td colspan="6" class="text-center text-grey-6">Без карти параметрів сервер зіставляє результати за analyzerCode шарів норм / кодом тесту</td></tr>
          </tbody>
        </q-markup-table>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!form.code || !form.name || !form.analyzerTypeId" data-testid="anSave" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
export default {
  name: 'AnalyzerFormDialog',
  props: { value: Boolean, analyzer: { type: Object, default: null }, connectors: { type: Array, default: () => [] } },
  data () { return { form: this.empty(), saving: false, error: '', typeFilter: '', testFilter: '' }; },
  computed: {
    types () { return this.$store.getters['dictionaries/items']('analyzer-types'); },
    typeOptions () { const f = this.typeFilter.toLowerCase(); return this.types.filter(t => t.isActive !== false).filter(t => !f || `${t.code} ${t.name} ${t.manufacturer}`.toLowerCase().includes(f)).map(t => ({ value: t.id, label: `${t.name} (${t.code})`, item: t })); },
    selectedType () { return this.types.find(t => t.id === this.form.analyzerTypeId) || null; },
    protocol () { return (this.selectedType && this.selectedType.exchType) || this.form.protocol || '—'; },
    connectorOptions () { return this.connectors.map(c => ({ value: c.id, label: `${c.name}${c.hostName ? ' · ' + c.hostName : ''}` })); },
    departmentOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    testCodeOptions () { const f = this.testFilter.toLowerCase(); return this.$store.getters['dictionaries/items']('tests').filter(t => !f || `${t.code} ${t.name}`.toLowerCase().includes(f)).map(t => ({ value: t.code, label: `${t.code} — ${t.name}` })); }
  },
  watch: {
    value (v) {
      if (v) {
        this.error = '';
        const a = this.analyzer;
        this.form = a ? { ...this.empty(), ...JSON.parse(JSON.stringify(a)), parameterMap: (a.parameterMap || []).map(p => ({ ...p })) } : this.empty();
        this.$store.dispatch('dictionaries/loadMany', ['analyzer-types', 'departments', 'tests']);
      }
    }
  },
  methods: {
    empty () { return { id: null, code: '', name: '', analyzerTypeId: null, connectorId: null, departmentId: null, connectionMode: 'TCP', tcpHost: '', tcpPort: 5100, isTcpServer: true, comPort: 'COM1', baudRate: 9600, parity: 'None', dataBits: 8, stopBits: 'One', flowControl: 'None', filePath: '', filePollSec: 10, autoQueryOrders: true, isActive: true, bopBase64: '', eopBase64: '', sleepMs: null, maxFrameLen: null, checksum: true, ackAfterRecord: true, orderTemplate: '', parameterMap: [] }; },
    typeDefault (k) { return this.selectedType ? (this.selectedType[k] || '') : ''; },
    filterTypes (val, update) { update(() => { this.typeFilter = val || ''; }); },
    filterTests (val, update) { update(() => { this.testFilter = val || ''; }); },
    async save () {
      this.saving = true; this.error = '';
      const body = { ...this.form, protocol: this.protocol !== '—' ? this.protocol : null, parameterMap: this.form.parameterMap.filter(p => p.analyzerCode && p.testCode).map(p => ({ ...p, unitOverride: p.unitOverride || null })) };
      ['bopBase64', 'eopBase64', 'orderTemplate', 'tcpHost', 'comPort', 'filePath'].forEach(k => { if (body[k] === '') body[k] = null; });
      const id = body.id; delete body.id; delete body.isOnline; delete body.lastMessageAt; delete body.lastError;
      try {
        const res = id ? await this.$api.updateAnalyzer(id, body) : await this.$api.createAnalyzer(body);
        this.$q.notify({ type: 'positive', message: 'Аналізатор збережено' });
        this.$emit('saved', res); this.$emit('input', false);
      } catch (e) { this.error = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    }
  }
};
</script>
