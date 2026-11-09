<!--
  ТЗ 22.10: «Довідники патоморфології» — шаблони описів, МКХ-О, методики забарвлення та ІГХ з цінами,
  правила автозамовлення ІГХ, типи матеріалу та строки, фіксатори, причини відмови.
  Ціни методик змінює лише адміністратор лабораторії: іншим ролям поля лише для читання (з підказкою), а не 403 після зміни.
-->
<template>
  <div data-testid="pathologyDictionariesPage">
    <page-header title="Довідники патоморфології" icon="fas fa-book-medical" :breadcrumbs="[{ label: 'Довідники' }, { label: 'Патоморфологія' }]">
      <q-input v-model="search" dense outlined clearable placeholder="Пошук у довіднику" style="min-width: 220px" data-testid="dictSearch">
        <template v-slot:prepend><q-icon name="search" /></template>
      </q-input>
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" :retryable="apiErrorRetryable" @retry="load" @dismiss="apiError = null" />
    <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm">
      <q-tab v-for="t in tabs" :key="t.name" :name="t.name" :label="t.label" />
    </q-tabs>
    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="__k" flat dense :pagination="{ rowsPerPage: 50 }" :loading="loading" wrap-cells no-data-label="Нічого не знайдено">
        <template v-if="tab === 'stains'" #body-cell-price="p">
          <q-td :props="p">
            <q-input :value="p.row.price" dense outlined type="number" min="0" step="1" style="width: 110px" :readonly="!canEditPrices" :data-testid="`price-${p.row.code}`"
                     @keydown="blockMinus" @change="e => savePrice(p.row, e.target.value, e.target)" />
            <q-tooltip v-if="!canEditPrices">{{ roleHint() }}</q-tooltip>
          </q-td>
        </template>
      </q-table>
      <div v-if="tab === 'stains'" class="text-caption text-grey-7 q-pa-sm">
        {{ canEditPrices ? 'Ціна зберігається, коли ви виходите з поля.' : 'Ціни змінює адміністратор лабораторії.' }}
        Для гістологічних методик 0 означає «входить у вартість блока»; для методик цитології (Папаніколау, Май-Грюнвальд-Гімза) це вартість одного скельця.
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';

const CYTO_METHODS = ['PAP', 'MGG'];
const methodGroup = r => (r.isIhc ? 'ІГХ' : CYTO_METHODS.includes(r.code) ? 'Цитологія' : 'Гістологія');
const GROUP_ORDER = { Гістологія: 1, Цитологія: 2, ІГХ: 3 };
const slaText = v => (v === 0 ? '20 хв' : `${v} роб. дн.`);

const COLS = {
  templates: [['name', 'Орган · вид матеріалу'], ['macro', 'Макроопис'], ['micro', 'Мікроопис']],
  topo: [['code', 'Код'], ['name', 'Топографія']],
  morph: [['code', 'Код (морфологія / поведінка)'], ['name', 'Морфологія']],
  stains: [['group', 'Група'], ['code', 'Код'], ['name', 'Методика'], ['price', 'Ціна, грн']],
  reflex: [['topo', 'Топографія'], ['morph', 'Морфологія'], ['panel', 'Панель'], ['why', 'Обґрунтування']],
  materialTypes: [['name', 'Тип матеріалу'], ['sla', 'Строк'], ['basePrice', 'Базова ціна, грн']],
  fixatives: [['name', 'Фіксатор'], ['minHours', 'Мін., год'], ['maxHours', 'Макс., год']],
  rejectReasons: [['name', 'Причина відмови']]
};

export default {
  name: 'PathologyDictionariesPage',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      dict: {}, tab: 'templates', search: '',
      tabs: [
        { name: 'templates', label: 'Шаблони описів' }, { name: 'topo', label: 'МКХ-О топографія' }, { name: 'morph', label: 'МКХ-О морфологія' },
        { name: 'stains', label: 'Методики та ІГХ' }, { name: 'reflex', label: 'Правила автозамовлення ІГХ' }, { name: 'materialTypes', label: 'Типи матеріалу та строки' },
        { name: 'fixatives', label: 'Фіксатори' }, { name: 'rejectReasons', label: 'Причини відмови' }
      ]
    };
  },
  computed: {
    canEditPrices () { return this.canDo(); }, // лише адміністратор лабораторії
    columns () {
      return COLS[this.tab].map(([k, l]) => ({ name: k, label: l, field: k, align: 'left', classes: k === 'macro' || k === 'micro' ? 'text-caption' : '' }));
    },
    rows () {
      const q = (this.search || '').trim().toLowerCase();
      let list = (this.dict[this.tab] || []).map(r => this.decorate(r));
      if (this.tab === 'stains') list = list.slice().sort((a, b) => GROUP_ORDER[a.group] - GROUP_ORDER[b.group] || String(a.name).localeCompare(String(b.name), 'uk'));
      if (q) list = list.filter(r => Object.keys(r).some(k => k !== '__k' && String(r[k] === null || r[k] === undefined ? '' : r[k]).toLowerCase().includes(q)));
      return list.map((r, i) => ({ ...r, __k: i }));
    }
  },
  created () { this.load(); },
  methods: {
    topoName (code) { const t = (this.dict.topo || []).find(x => x.code === code); return t ? `${code} ${t.name}` : code; },
    morphName (code) { const t = (this.dict.morph || []).find(x => x.code === code); return t ? `${code} ${t.name}` : code; },
    /** Підставляє в рядок довідника читабельні значення замість сирих кодів */
    decorate (r) {
      if (this.tab === 'stains') return { ...r, group: methodGroup(r) };
      if (this.tab === 'materialTypes') return { ...r, sla: slaText(r.slaDays) };
      if (this.tab === 'reflex') {
        const topo = r.topoFrom === r.topoTo ? this.topoName(r.topoFrom) : `${this.topoName(r.topoFrom)} — ${this.topoName(r.topoTo)}`;
        const morph = String(r.morphCodes || '').split(',').map(s => s.trim()).filter(Boolean).map(c => this.morphName(c)).join('; ');
        return { ...r, topo, morph };
      }
      return { ...r };
    },
    blockMinus (e) { if (e.key === '-' || e.key === 'e' || e.key === 'E') e.preventDefault(); },
    async savePrice (row, v, el) {
      if (!this.canEditPrices) return;
      const price = Number(v);
      const old = (this.dict.stains.find(s => s.code === row.code) || {}).price;
      if (v === '' || !(price >= 0)) { this.$q.notify({ type: 'negative', message: 'Ціна має бути числом від 0' }); if (el) el.value = old; return; }
      if (price === old) return;
      const r = await this.callApi(() => this.$api.pathMethodPrice(row.code, price), { success: `Ціну «${row.name}» збережено` });
      const target = this.dict.stains.find(s => s.code === row.code);
      // таблицю не перемальовуємо: оновлюємо лише змінений рядок, фокус і прокрутка лишаються
      if (r !== undefined && target) target.price = price;
      else if (el) el.value = old;
    },
    async load () { const d = await this.callApi(() => this.$api.pathDictionaries()); if (d) this.dict = d; }
  }
};
</script>
