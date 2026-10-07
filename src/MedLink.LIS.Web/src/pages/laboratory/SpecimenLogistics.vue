<template>
  <div class="logistics-page" data-testid="logisticsPage">
    <page-header title="Логістика зразків" icon="fas fa-truck" subtitle="Маршрутні листи (маніфести), кур’єри, холодовий ланцюг +2…+8 °C" :breadcrumbs="[{ label: 'Логістика' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
      <q-btn unelevated color="primary" icon="add" label="Створити маніфест" data-testid="createManifestBtn" @click="openCreate" />
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-md row items-center q-col-gutter-sm">
      <div class="col-12 col-md-3">
        <q-select v-model="statusFilter" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options @input="load" />
      </div>
      <div class="col-12 col-md-9 text-caption text-grey-7">
        <q-icon name="ac_unit" color="primary" /> Порушення холодового ланцюга (температура прийому поза +2…+8 °C) підсвічуються червоним.
      </div>
    </div>

    <div class="medlink-card">
      <q-table :data="manifests" :columns="columns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[15, 50]" no-data-label="Маніфестів немає" data-testid="manifestsTable">
        <template v-slot:body="props">
          <q-tr :props="props" :class="{ 'bg-red-1': isViolated(props.row) }" class="cursor-pointer" @click="openCard(props.row)">
            <q-td key="code" :props="props"><span class="mono text-weight-bold">{{ props.row.manifestNumber || props.row.code || shortId(props.row.id) }}</span></q-td>
            <q-td key="createdAt" :props="props">{{ props.row.dispatchedAt || props.row.createdOn || props.row.createdAt | datetime }}</q-td>
            <q-td key="route" :props="props">{{ depName(props.row.originDepartmentId) }} → {{ depName(props.row.destinationDepartmentId) }}</q-td>
            <q-td key="courier" :props="props">{{ props.row.courierName }} <span class="text-grey-7">{{ props.row.courierPhone }}</span></q-td>
            <q-td key="items" :props="props" class="text-center">{{ itemsCount(props.row) }}</q-td>
            <q-td key="temp" :props="props" class="text-center">
              <span>{{ props.row.temperatureDispatch | num }} °C</span>
              <span class="text-grey-6"> → </span>
              <span :class="isViolated(props.row) ? 'text-negative text-weight-bold' : ''">{{ props.row.temperatureReceipt !== null && props.row.temperatureReceipt !== undefined ? formatNumber(props.row.temperatureReceipt) + ' °C' : '—' }}</span>
              <q-icon v-if="isViolated(props.row)" name="warning" color="negative" class="q-ml-xs"><q-tooltip>Порушення холодового ланцюга</q-tooltip></q-icon>
            </q-td>
            <q-td key="status" :props="props"><status-chip :value="props.row.status" type="manifest" icon /></q-td>
            <q-td key="actions" :props="props" class="text-right no-wrap" @click.stop>
              <q-btn flat dense round size="sm" icon="inbox" color="cyan-7" :disable="props.row.status === 'RECEIVED'" @click="openReceive(props.row)"><q-tooltip>Прийняти</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="edit" color="primary" @click="openEdit(props.row)"><q-tooltip>Редагувати</q-tooltip></q-btn>
              <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDelete(props.row)"><q-tooltip>Видалити</q-tooltip></q-btn>
            </q-td>
          </q-tr>
        </template>
      </q-table>
    </div>

    <!-- Створення / редагування -->
    <q-dialog v-model="formOpen" persistent>
      <q-card style="min-width: 640px; max-width: 96vw" data-testid="manifestDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm">
          <q-icon name="local_shipping" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Редагування маніфесту' : 'Новий маршрутний лист' }}</div>
          <q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-select v-model="form.originDepartmentId" outlined dense label="Звідки (відділення / пункт забору)" :options="depOptions" emit-value map-options /></div>
            <div class="col-6"><q-select v-model="form.destinationDepartmentId" outlined dense label="Куди (лабораторія)" :options="depOptions" emit-value map-options /></div>
            <div class="col-6"><q-input v-model="form.courierName" outlined dense label="Кур’єр *" data-testid="courierName" /></div>
            <div class="col-3"><q-input v-model="form.courierPhone" outlined dense label="Телефон" /></div>
            <div class="col-3"><q-input v-model="form.temperatureDispatch" outlined dense label="t° відправки, °C" inputmode="decimal" data-testid="tempDispatch" /></div>
          </div>
          <div class="section-title">Проби в контейнері</div>
          <q-select
            v-model="form.barcodes"
            outlined dense multiple use-chips use-input
            label="Штрихкоди (оберіть зі списку забраних або введіть вручну + Enter)"
            :options="collectedOptions"
            new-value-mode="add-unique"
            input-debounce="0"
            @filter="filterCollected"
            data-testid="manifestBarcodes"
          />
          <div class="text-caption text-grey-6">Забраних проб, що очікують відправки: {{ collected.length }}</div>
          <div v-if="formError" class="text-negative">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" icon="save" :label="form.id ? 'Зберегти' : 'Відправити'" :loading="saving" :disable="!form.courierName || !form.barcodes.length" data-testid="manifestSave" @click="saveForm" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Прийом -->
    <q-dialog v-model="receiveOpen" persistent>
      <q-card style="min-width: 560px; max-width: 96vw" data-testid="receiveManifestDialog">
        <q-card-section class="row items-center bg-cyan-7 text-white q-py-sm">
          <q-icon name="inbox" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">Прийом контейнера {{ active && (active.manifestNumber || active.code || shortId(active.id)) }}</div>
          <q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-input v-model="receive.temperatureReceipt" outlined dense label="Температура при прийомі, °C *" inputmode="decimal" data-testid="tempReceipt" :hint="tempHint" :error="tempOutOfRange" error-message="Поза діапазоном +2…+8 °C — буде зафіксовано порушення холодового ланцюга" />
          <div class="section-title">Отримані проби</div>
          <q-option-group v-model="receive.receivedBarcodes" type="checkbox" dense :options="(activeBarcodes).map(b => ({ value: b, label: b }))" />
          <q-input v-model="receive.notes" outlined dense autogrow label="Примітки (пошкодження, невідповідності)" />
          <div v-if="receiveError" class="text-negative">{{ receiveError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="cyan-7" icon="check" label="Прийняти" :loading="saving" :disable="receive.temperatureReceipt === '' || !receive.receivedBarcodes.length" data-testid="receiveManifestConfirm" @click="doReceive" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Картка -->
    <q-dialog v-model="cardOpen">
      <q-card style="min-width: 560px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm">
          <div class="text-subtitle1 text-weight-bold">Маніфест {{ active && (active.manifestNumber || active.code || shortId(active.id)) }}</div>
          <q-space /><status-chip v-if="active" :value="active.status" type="manifest" /><q-btn flat round dense icon="close" v-close-popup class="q-ml-sm" />
        </q-card-section>
        <q-card-section v-if="active">
          <q-banner v-if="isViolated(active)" dense rounded class="bg-red-1 text-negative q-mb-sm"><q-icon name="warning" /> Порушення холодового ланцюга: t° прийому {{ active.temperatureReceipt | num }} °C</q-banner>
          <div class="row q-col-gutter-sm text-body2">
            <div class="col-6">Маршрут: <b>{{ depName(active.originDepartmentId) }} → {{ depName(active.destinationDepartmentId) }}</b></div>
            <div class="col-6">Кур’єр: <b>{{ active.courierName }}</b> {{ active.courierPhone }}</div>
            <div class="col-6">Відправлено: {{ active.dispatchedAt || active.createdOn | datetime }} · {{ active.temperatureDispatch | num }} °C</div>
            <div class="col-6">Прийнято: {{ active.receivedAt | datetime }} · {{ active.temperatureReceipt !== null && active.temperatureReceipt !== undefined ? formatNumber(active.temperatureReceipt) + ' °C' : '—' }}</div>
            <div class="col-12" v-if="active.notes">Примітки: {{ active.notes }}</div>
          </div>
          <div class="section-title q-mt-md">Проби ({{ activeBarcodes.length }})</div>
          <div class="row q-gutter-xs">
            <q-chip v-for="b in activeBarcodes" :key="b" dense square class="mono" :color="receivedSet.has(b) ? 'green-2' : 'grey-3'" clickable @click="$router.push({ name: 'lab-phlebotomy', query: { barcode: b } })">{{ b }}</q-chip>
          </div>
        </q-card-section>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="deleteOpen" title="Видалити маніфест" message="Маніфест буде видалено; проби повернуться до статусу «Забрано»." ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { MANIFEST_STATUS, toOptions } from '../../utils/statuses';
import { formatNumber, parseDecimal } from '../../utils/format';

const emptyForm = () => ({ id: null, originDepartmentId: null, destinationDepartmentId: null, courierName: '', courierPhone: '', temperatureDispatch: '4', barcodes: [] });

export default {
  name: 'SpecimenLogistics',
  mixins: [apiMixin],
  data () {
    return {
      manifests: [],
      collected: [],
      collectedFilter: '',
      statusFilter: null,
      statusOptions: toOptions(MANIFEST_STATUS),
      pagination: { rowsPerPage: 15 },
      formOpen: false, receiveOpen: false, cardOpen: false, deleteOpen: false,
      form: emptyForm(), formError: '', saving: false,
      receive: { temperatureReceipt: '', receivedBarcodes: [], notes: '' }, receiveError: '',
      active: null,
      columns: [
        { name: 'code', label: '№', align: 'left' },
        { name: 'createdAt', label: 'Відправлено', align: 'left' },
        { name: 'route', label: 'Маршрут', align: 'left' },
        { name: 'courier', label: 'Кур’єр', align: 'left' },
        { name: 'items', label: 'Проб', align: 'center' },
        { name: 'temp', label: 't° відпр. → прийом', align: 'center' },
        { name: 'status', label: 'Статус', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    depOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    collectedOptions () {
      const f = this.collectedFilter.toLowerCase();
      return this.collected.map(s => s.barcode).filter(b => !f || b.toLowerCase().includes(f));
    },
    activeBarcodes () { return this.active ? this.barcodesOf(this.active) : []; },
    receivedSet () { return new Set((this.active && (this.active.receivedBarcodes || (this.active.items || []).filter(i => i.isReceived).map(i => i.barcode))) || []); },
    tempOutOfRange () {
      const t = parseDecimal(this.receive.temperatureReceipt);
      return t !== null && (t < 2 || t > 8);
    },
    tempHint () { return 'Норма холодового ланцюга: +2…+8 °C'; }
  },
  created () {
    this.$store.dispatch('dictionaries/load', 'departments');
    this.load();
  },
  methods: {
    formatNumber,
    shortId (id) { return id ? String(id).slice(0, 8) : ''; },
    depName (id) { const d = this.$store.getters['dictionaries/byId']('departments', id); return d ? d.name : (id || '—'); },
    barcodesOf (m) { return m.barcodes || (m.items || []).map(i => i.barcode || i.sampleBarcode).filter(Boolean); },
    itemsCount (m) { return this.barcodesOf(m).length || m.itemsCount || 0; },
    isViolated (m) {
      if (m.isColdChainViolated) return true;
      const t = m.temperatureReceipt;
      return t !== null && t !== undefined && (Number(t) < 2 || Number(t) > 8);
    },
    async load () {
      const res = await this.callApi(() => this.$api.getManifests(this.statusFilter));
      if (res !== undefined) this.manifests = this.asList(res);
      try { this.collected = this.asList(await this.$api.getSamples({ status: 'COLLECTED' })); } catch (e) { /* ignore */ }
    },
    filterCollected (val, update) { update(() => { this.collectedFilter = val || ''; }); },
    openCreate () { this.form = emptyForm(); this.formError = ''; this.formOpen = true; },
    openEdit (m) {
      this.form = { id: m.id, originDepartmentId: m.originDepartmentId, destinationDepartmentId: m.destinationDepartmentId, courierName: m.courierName, courierPhone: m.courierPhone, temperatureDispatch: m.temperatureDispatch === null || m.temperatureDispatch === undefined ? '' : String(m.temperatureDispatch), barcodes: this.barcodesOf(m) };
      this.formError = ''; this.formOpen = true;
    },
    async saveForm () {
      this.saving = true; this.formError = '';
      const body = { ...this.form, temperatureDispatch: parseDecimal(this.form.temperatureDispatch) };
      delete body.id;
      try {
        if (this.form.id) await this.$api.updateManifest(this.form.id, body);
        else await this.$api.createManifest(body);
        this.notifyOk(this.form.id ? 'Маніфест оновлено' : 'Маніфест створено, проби → IN_TRANSIT');
        this.formOpen = false;
        this.load();
      } catch (e) { this.formError = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    },
    openReceive (m) {
      this.active = m;
      this.receive = { temperatureReceipt: '', receivedBarcodes: this.barcodesOf(m), notes: '' };
      this.receiveError = '';
      this.receiveOpen = true;
    },
    async doReceive () {
      this.saving = true; this.receiveError = '';
      try {
        const res = await this.$api.receiveManifest(this.active.id, { temperatureReceipt: parseDecimal(this.receive.temperatureReceipt), receivedBarcodes: this.receive.receivedBarcodes, notes: this.receive.notes || null });
        if ((res && res.isColdChainViolated) || this.tempOutOfRange) this.$q.notify({ type: 'warning', message: 'Контейнер прийнято з порушенням холодового ланцюга', timeout: 5000 });
        else this.notifyOk('Контейнер прийнято, проби → RECEIVED');
        this.receiveOpen = false;
        this.load();
      } catch (e) { this.receiveError = e.userMessage || 'Помилка прийому'; } finally { this.saving = false; }
    },
    openCard (m) { this.active = m; this.cardOpen = true; },
    askDelete (m) { this.active = m; this.deleteOpen = true; },
    async doDelete () {
      try { await this.$api.deleteManifest(this.active.id); this.notifyOk('Маніфест видалено'); this.load(); } catch (e) { this.notifyError(e); }
    }
  }
};
</script>
