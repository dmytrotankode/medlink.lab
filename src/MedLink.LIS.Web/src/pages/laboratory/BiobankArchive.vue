<template>
  <div class="biobank-page" data-testid="biobankPage">
    <page-header title="Біобанк та кріо-архів" icon="fas fa-snowflake" subtitle="Зразки за адресою в морозильнику" :breadcrumbs="[{ label: 'Біобанк' }]">
      <q-input v-model="searchBarcode" dense outlined placeholder="Штрихкод зразка" style="width: 240px" data-testid="biobankSearch" @keyup.enter="search">
        <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
        <template v-slot:append><q-btn flat dense round icon="search" aria-label="Знайти зразок" :disable="!(searchBarcode || '').trim()" @click="search"><q-tooltip>Знайти зразок (Enter)</q-tooltip></q-btn></template>
      </q-input>
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="loadRacks" />
      <span class="inline-block">
        <q-btn unelevated dense color="primary" icon="add" label="Новий штатив" :disable="!canWork" data-testid="rackCreate" @click="openRack(null)" />
        <q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip>
      </span>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="loadRacks" />

    <div class="row q-col-gutter-md">
      <!-- Список штативів -->
      <div class="col-12 col-md-3">
        <div class="medlink-card">
          <div class="medlink-card__title"><span>Штативи</span><span class="text-caption text-grey-6">{{ racks.length }}</span></div>
          <q-list dense separator>
            <q-item v-for="r in racks" :key="r.id" clickable :active="rack && rack.id === r.id" active-class="bg-blue-1 text-primary" @click="selectRack(r)" :data-testid="`rack-${r.code || r.id}`">
              <q-item-section avatar><q-icon name="ac_unit" :color="tempColor(r)" /></q-item-section>
              <q-item-section>
                <q-item-label class="text-weight-bold">{{ rackTitle(r) }}</q-item-label>
                <q-item-label caption>{{ rackCaption(r) }}</q-item-label>
              </q-item-section>
              <q-item-section side>
                <div class="row no-wrap">
                  <q-btn flat dense round size="sm" icon="edit" color="primary" aria-label="Змінити штатив" :disable="!canWork" @click.stop="openRack(r)"><q-tooltip>{{ canWork ? 'Змінити штатив' : workHint }}</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити штатив" :disable="!canDelete || occupied(r) > 0" @click.stop="askDeleteRack(r)"><q-tooltip>{{ !canDelete ? deleteHint : (occupied(r) > 0 ? 'Видалити можна лише порожній штатив' : 'Видалити штатив') }}</q-tooltip></q-btn>
                </div>
              </q-item-section>
            </q-item>
          </q-list>
          <empty-state v-if="!racks.length && !loading" title="Штативів немає" icon="ac_unit" hint="Створіть перший кріобокс" />
        </div>
      </div>

      <!-- Сітка -->
      <div class="col-12 col-md-6">
        <div class="medlink-card q-pa-md" v-if="rack">
          <div class="row items-center justify-between q-mb-sm">
            <div>
              <div class="text-subtitle1 text-weight-bold">{{ rackTitle(rack) }} <span class="text-grey-7">({{ capacity(rack) }} комірок)</span></div>
              <div class="text-caption text-grey-7">Температура зберігання: <b>{{ temp(rack) }}</b> · Заповнено: <b>{{ cells.filter(c => c.barcode).length }}/{{ capacity(rack) }}</b></div>
            </div>
            <div class="row q-gutter-sm text-caption items-center">
              <span v-for="l in legend" :key="l.label"><span class="legend-dot" :style="{ background: l.color }" /> {{ l.label }}</span>
            </div>
          </div>
          <div class="cryo-grid-8x12" data-testid="cryoGrid">
            <div />
            <div v-for="c in cols" :key="'h' + c" class="cryo-rowlabel">{{ String(c).padStart(2, '0') }}</div>
            <template v-for="r in rowsCount">
              <div :key="'r' + r" class="cryo-rowlabel">{{ rowLetter(r) }}</div>
              <div
                v-for="c in cols"
                :key="`${r}-${c}`"
                class="cryo-cell"
                :class="cellClass(r, c)"
                :style="cellStyle(r, c)"
                :data-testid="`cell-${rowLetter(r)}-${String(c).padStart(2, '0')}`"
                @click="selectCell(r, c)"
              >
                {{ rowLetter(r) }}-{{ String(c).padStart(2, '0') }}
                <q-tooltip v-if="cellAt(r, c) && cellAt(r, c).barcode">{{ cellAt(r, c).barcode }} · {{ cellAt(r, c).biomaterialName || 'біоматеріал не вказано' }} · до {{ cellAt(r, c).expiryAt | date }}</q-tooltip>
              </div>
            </template>
          </div>
        </div>
        <div v-else class="medlink-card"><empty-state title="Оберіть штатив" icon="grid_on" hint="Сітка 8×12 відобразить зайняті комірки за біоматеріалом і терміном" /></div>
      </div>

      <!-- Паспорт комірки -->
      <div class="col-12 col-md-3">
        <div class="medlink-card" data-testid="cellPassport">
          <div class="medlink-card__title"><span>Комірка {{ selected ? selected.label : '' }}</span><q-badge v-if="selectedCell && selectedCell.barcode" color="positive" label="зайнята" /><q-badge v-else-if="selected" color="grey-6" label="вільна" /></div>
          <div v-if="!selected" class="q-pa-md text-grey-6 text-caption">Оберіть комірку в сітці.</div>
          <div v-else-if="selectedCell && selectedCell.barcode" class="q-pa-md">
            <div class="text-caption text-grey-7">Штрихкод зразка</div>
            <a href="#" class="mono text-h6 text-primary" @click.prevent="$router.push({ name: 'lab-phlebotomy', query: { barcode: selectedCell.barcode } })">{{ selectedCell.barcode }}</a>
            <div class="q-mt-sm text-body2">
              <div v-if="selectedCell.patientName">Пацієнт: <b>{{ selectedCell.patientName }}</b></div>
              <div>Біоматеріал: <b>{{ selectedCell.biomaterialName || 'не вказано' }}</b></div>
              <div>Розміщено: {{ selectedCell.storedAt | datetime }}</div>
              <div :class="expired(selectedCell) ? 'text-negative text-weight-bold' : ''">Придатний до: {{ selectedCell.expiryAt | date }}</div>
              <div v-if="selectedCell.orderNumber">Замовлення: <router-link v-if="selectedCell.orderId" :to="{ name: 'lab-order-card', params: { id: selectedCell.orderId } }">{{ selectedCell.orderNumber }}</router-link><span v-else>{{ selectedCell.orderNumber }}</span></div>
            </div>
            <div class="column q-gutter-sm q-mt-md">
              <span class="inline-block"><q-btn color="primary" icon="edit" label="Змінити термін" no-caps class="full-width" :disable="!canWork" @click="openEditCell" /><q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip></span>
              <span class="inline-block"><q-btn outline color="negative" icon="eject" label="Вилучити з архіву" no-caps class="full-width" :disable="!canWork" data-testid="cellRemove" @click="removeOpen = true" /><q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip></span>
            </div>
          </div>
          <div v-else class="q-pa-md">
            <div class="text-caption text-grey-7 q-mb-sm">Комірка вільна — розмістіть зразок</div>
            <span class="inline-block full-width"><q-btn color="primary" icon="add_location_alt" label="Розмістити зразок" no-caps class="full-width" :disable="!canWork" data-testid="cellPlace" @click="openPlace" /><q-tooltip v-if="!canWork">{{ workHint }}</q-tooltip></span>
          </div>
        </div>
        <div v-if="searchResult" class="medlink-card q-mt-md q-pa-md" data-testid="searchResult">
          <div class="section-title">Результат пошуку</div>
          <div class="mono text-weight-bold">{{ searchResult.barcode }}</div>
          <div class="text-body2">{{ searchResult.rackName || searchResult.rackCode }} · комірка {{ searchResult.coordinate }}</div>
          <q-btn flat dense color="primary" label="Показати" @click="gotoSearchResult" />
        </div>
      </div>
    </div>

    <!-- Діалог штатива -->
    <q-dialog v-model="rackOpen" no-backdrop-dismiss>
      <q-card style="min-width: 520px" data-testid="rackDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="ac_unit" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ rackForm.id ? 'Штатив' : 'Новий штатив' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div class="row q-col-gutter-sm">
            <div class="col-6"><q-input v-model="rackForm.code" outlined dense label="Код *" data-testid="rackCode" :error="rackTouched && !rackForm.code" hide-bottom-space /></div>
            <div class="col-6"><q-input v-model="rackForm.name" outlined dense label="Назва" data-testid="rackName" /></div>
            <div class="col-6"><q-input v-model="rackForm.freezerName" outlined dense label="Морозильник" /></div>
            <div class="col-6"><q-input v-model="rackForm.shelfNumber" outlined dense label="Полиця" /></div>
            <div class="col-4"><q-input v-model="rackForm.temperatureCelsius" outlined dense inputmode="decimal" label="Температура, °C" /></div>
            <div class="col-4"><q-input v-model.number="rackForm.rowsCount" outlined dense type="number" min="1" max="26" label="Рядків" /></div>
            <div class="col-4"><q-input v-model.number="rackForm.colsCount" outlined dense type="number" min="1" max="50" label="Стовпців" /></div>
          </div>
          <div v-if="rackError" class="text-negative">{{ rackError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="rackSave" @click="saveRack" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Розміщення / редагування комірки -->
    <q-dialog v-model="placeOpen" no-backdrop-dismiss>
      <q-card style="min-width: 460px" data-testid="placeDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="add_location_alt" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ placeForm.id ? 'Термін зберігання' : 'Розміщення зразка' }} · комірка {{ selected && selected.label }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-input v-model="placeForm.barcode" outlined dense label="Штрихкод зразка *" :readonly="!!placeForm.id" :autofocus="!placeForm.id" data-testid="placeBarcode" @keyup.enter="savePlace" />
          <q-input v-model="placeForm.expiryAt" outlined dense type="date" stack-label label="Зберігати до *" data-testid="placeExpiry" :autofocus="!!placeForm.id" @keyup.enter="savePlace" />
          <div v-if="placeError" class="text-negative">{{ placeError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!placeForm.barcode || !placeForm.expiryAt" data-testid="placeSave" @click="savePlace" /></q-card-actions>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="removeOpen" title="Вилучити зразок із архіву?" :message="`Зразок ${selectedCell && selectedCell.barcode} буде вилучено з комірки ${selected && selected.label}. Вкажіть причину: повторний аналіз або утилізація.`" ok-label="Вилучити" color="negative" icon="eject" with-reason reason-required reason-label="Причина (повторний аналіз, утилізація) *" @confirm="doRemove" />
    <confirm-dialog v-model="deleteRackOpen" title="Видалити штатив?" message="Штатив буде видалено. Видалити можна лише порожній штатив." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteRack" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import roleMixin from '../../mixins/roleMixin';
import { parseDecimal } from '../../utils/format';

const COLORS = { serum: '#d04f45', plasma: '#7c3aed', edta: '#4274A7', urine: '#f2c037', other: '#318F94' };

export default {
  name: 'BiobankArchive',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      rackTouched: false, racks: [], rack: null, cells: [],
      selected: null, searchBarcode: '', searchResult: null,
      rackOpen: false, placeOpen: false, removeOpen: false, deleteRackOpen: false,
      rackForm: {}, rackError: '', placeForm: {}, placeError: '', saving: false, activeRack: null,
      legend: [
        { label: 'Сироватка', color: COLORS.serum }, { label: 'Плазма', color: COLORS.plasma }, { label: 'ЕДТА / кров', color: COLORS.edta },
        { label: 'Сеча', color: COLORS.urine }, { label: 'Інший біоматеріал', color: COLORS.other }, { label: 'Спливає за ≤ 30 днів', color: '#ff9800' }, { label: 'Прострочено', color: '#212121' }
      ]
    };
  },
  computed: {
    // Дозволи збігаються з серверною політикою: ведуть адміністратор, лікар-лаборант, лаборант; видаляють штативи — адміністратор і лікар-лаборант
    canWork () { return this.canDo('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    workHint () { return this.roleHint('LAB_DOCTOR', 'LAB_TECHNICIAN'); },
    canDelete () { return this.canDo('LAB_DOCTOR'); },
    deleteHint () { return this.roleHint('LAB_DOCTOR'); },
    rowsCount () { return Number(this.rack && this.rack.rowsCount) || 8; },
    cols () { const n = Number(this.rack && this.rack.colsCount) || 12; return Array.from({ length: n }, (_, i) => i + 1); },
    selectedCell () { return this.selected ? this.cellAt(this.selected.row, this.selected.col) : null; }
  },
  created () { this.loadRacks(); },
  methods: {
    rowLetter (r) { return String.fromCharCode(64 + r); },
    capacity (r) { return r.capacity || (Number(r.rowsCount) || 8) * (Number(r.colsCount) || 12); },
    occupied (r) { return r.id === (this.rack && this.rack.id) && this.cells.length ? this.cells.filter(c => c.barcode).length : (Number(r.occupied) || 0); },
    temp (r) { const t = Number(r.temperatureCelsius); return isNaN(t) ? '—' : `${String(t).replace('-', '−')} °C`; },
    // Температура виводиться окремо, тому з назви штатива її прибираємо
    rackTitle (r) { return String(r.name || r.code || '').replace(/,\s*[-−+]?\d+\s*°\s*C(?=\))/i, ''); },
    rackCaption (r) { return [[r.freezerName, r.shelfNumber].filter(Boolean).join(' / '), this.temp(r), `${this.occupied(r)}/${this.capacity(r)}`].filter(Boolean).join(' · '); },
    tempColor (r) { const t = Number(r.temperatureCelsius); if (isNaN(t)) return 'light-blue-4'; return t <= -70 ? 'light-blue-8' : (t <= -15 ? 'light-blue-5' : 'teal-5'); },
    cellAt (r, c) {
      return this.cells.find(x => {
        const row = typeof x.row === 'string' ? x.row.toUpperCase().charCodeAt(0) - 64 : Number(x.row);
        return row === r && Number(x.col) === c;
      }) || null;
    },
    expired (cell) { return cell.expiryAt && new Date(cell.expiryAt) < new Date(); },
    soon (cell) { if (!cell.expiryAt) return false; const d = (new Date(cell.expiryAt) - new Date()) / 86400000; return d >= 0 && d <= 30; },
    cellColor (cell) {
      if (!cell || !cell.barcode) return null;
      if (this.expired(cell)) return '#212121';
      if (this.soon(cell)) return '#ff9800';
      const b = `${cell.biomaterialCode || ''} ${cell.biomaterialName || ''}`.toLowerCase();
      if (b.includes('серов') || b.includes('serum') || b.includes('сир')) return COLORS.serum;
      if (b.includes('плазм') || b.includes('plasma')) return COLORS.plasma;
      if (b.includes('кров') || b.includes('edta') || b.includes('едта') || b.includes('blood')) return COLORS.edta;
      if (b.includes('сеч') || b.includes('urine')) return COLORS.urine;
      return COLORS.other;
    },
    cellClass (r, c) {
      const cell = this.cellAt(r, c);
      const cls = [];
      if (!cell || !cell.barcode) cls.push('cryo-cell--empty');
      if (this.selected && this.selected.row === r && this.selected.col === c) cls.push('cryo-cell--selected');
      return cls;
    },
    cellStyle (r, c) { const color = this.cellColor(this.cellAt(r, c)); return color ? { background: color, color: color === '#f2c037' ? '#333' : '#fff' } : {}; },
    async loadRacks () {
      const res = await this.callApi(() => this.$api.racks());
      if (res === undefined) return;
      this.racks = this.asList(res);
      if (this.rack) { const fresh = this.racks.find(r => r.id === this.rack.id); if (fresh) this.rack = fresh; }
      else if (this.racks.length) this.selectRack(this.racks[0]);
      if (this.rack) this.loadCells();
    },
    async selectRack (r) { this.rack = r; this.selected = null; await this.loadCells(); },
    async loadCells () {
      if (!this.rack) return;
      try { const res = await this.$api.rackCells(this.rack.id); this.cells = this.asList(res.cells ? res.cells : res); } catch (e) { this.cells = []; this.notifyError(e); }
    },
    selectCell (r, c) { this.selected = { row: r, col: c, label: `${this.rowLetter(r)}-${String(c).padStart(2, '0')}` }; },
    openRack (r) {
      this.activeRack = r;
      this.rackForm = r ? { id: r.id, code: r.code, name: r.name, freezerName: r.freezerName || '', shelfNumber: r.shelfNumber || '', roomNumber: r.roomNumber || '', temperatureCelsius: String(r.temperatureCelsius), rowsCount: r.rowsCount || 8, colsCount: r.colsCount || 12 } : { id: null, code: '', name: '', freezerName: '', shelfNumber: '', roomNumber: '', temperatureCelsius: '-80', rowsCount: 8, colsCount: 12 };
      this.rackError = ''; this.rackTouched = false; this.rackOpen = true;
    },
    async saveRack () {
      this.rackTouched = true; this.rackError = '';
      if (!this.rackForm.code) { this.rackError = 'Вкажіть код штатива'; return; }
      const t = parseDecimal(this.rackForm.temperatureCelsius);
      if (t === null) { this.rackError = 'Вкажіть температуру зберігання, наприклад −80'; return; }
      this.saving = true;
      const f = this.rackForm;
      const body = { code: f.code, name: f.name || f.code, roomNumber: f.roomNumber || null, freezerName: f.freezerName || null, shelfNumber: f.shelfNumber || null, temperatureCelsius: t, rowsCount: Number(f.rowsCount) || 8, colsCount: Number(f.colsCount) || 12 };
      try {
        const res = this.rackForm.id ? await this.$api.updateRack(this.rackForm.id, body) : await this.$api.createRack(body);
        this.notifyOk('Штатив збережено'); this.rackOpen = false;
        await this.loadRacks();
        if (res && res.id) { const r = this.racks.find(x => x.id === res.id); if (r) this.selectRack(r); }
      } catch (e) { this.rackError = e.userMessage || 'Не вдалося зберегти штатив'; } finally { this.saving = false; }
    },
    askDeleteRack (r) { this.activeRack = r; this.deleteRackOpen = true; },
    async doDeleteRack () { try { await this.$api.deleteRack(this.activeRack.id); this.notifyOk('Штатив видалено'); if (this.rack && this.rack.id === this.activeRack.id) { this.rack = null; this.cells = []; } this.loadRacks(); } catch (e) { this.notifyError(e); } },
    openPlace () { const d = new Date(); d.setDate(d.getDate() + 180); this.placeForm = { id: null, barcode: this.searchBarcode || '', expiryAt: d.toISOString().slice(0, 10) }; this.placeError = ''; this.placeOpen = true; },
    openEditCell () { const c = this.selectedCell; this.placeForm = { id: c.id, barcode: c.barcode, expiryAt: (c.expiryAt || '').slice(0, 10) }; this.placeError = ''; this.placeOpen = true; },
    async savePlace () {
      // Enter у полі обходить неактивну кнопку, тому перевіряємо заповнення тут
      if (!(this.placeForm.barcode || '').trim()) { this.placeError = 'Вкажіть штрихкод зразка'; return; }
      if (!this.placeForm.expiryAt) { this.placeError = 'Вкажіть, до якої дати зберігати зразок'; return; }
      this.saving = true; this.placeError = '';
      const body = { rackId: this.rack.id, row: this.rowLetter(this.selected.row), col: this.selected.col, barcode: this.placeForm.barcode.trim(), expiryAt: this.placeForm.expiryAt };
      try {
        if (this.placeForm.id) await this.$api.updateCell(this.placeForm.id, { expiryAt: this.placeForm.expiryAt }); else await this.$api.placeCell(body);
        this.notifyOk(this.placeForm.id ? `Термін зберігання змінено: комірка ${this.selected.label}` : `Зразок ${body.barcode} розміщено в комірці ${this.selected.label}`); this.placeOpen = false; this.loadCells(); this.loadRacks();
      } catch (e) { this.placeError = e.userMessage || 'Не вдалося розмістити зразок'; } finally { this.saving = false; }
    },
    async doRemove (reason) { try { await this.$api.removeCell(this.selectedCell.id, reason); this.notifyOk('Зразок вилучено з архіву'); this.loadCells(); this.loadRacks(); } catch (e) { this.notifyError(e); } },
    async search () {
      const code = (this.searchBarcode || '').trim(); if (!code) return;
      this.searchResult = null;
      try {
        const res = await this.$api.biobankSearch(code);
        const hit = Array.isArray(res) ? res[0] : res;
        if (!hit || !(hit.rackId || hit.rack)) { this.$q.notify({ type: 'warning', message: `Зразок ${code} не знайдено в архіві` }); return; }
        this.searchResult = { ...hit, barcode: code };
        this.gotoSearchResult();
      } catch (e) { if (e.apiStatus === 404) this.$q.notify({ type: 'warning', message: `Зразок ${code} не знайдено в архіві` }); else this.notifyError(e); }
    },
    async gotoSearchResult () {
      const hit = this.searchResult; if (!hit) return;
      const rackId = hit.rackId || (hit.rack && hit.rack.id);
      const r = this.racks.find(x => x.id === rackId);
      if (r) await this.selectRack(r);
      const row = typeof hit.row === 'string' ? hit.row.toUpperCase().charCodeAt(0) - 64 : Number(hit.row);
      if (row && hit.col) this.selectCell(row, Number(hit.col));
    }
  }
};
</script>

<style scoped>
.cryo-cell--empty { color: #546e7a; }
.legend-dot { display: inline-block; width: 10px; height: 10px; border-radius: 2px; vertical-align: middle; margin-right: 3px; }
</style>
