<template>
  <div class="order-matrix" data-testid="orderMatrix">
    <div class="row items-center q-col-gutter-sm q-mb-sm">
      <div class="col-12 col-md-5">
        <q-input v-model="search" dense outlined clearable placeholder="Пошук: код, назва, біоматеріал…" data-testid="matrixSearch"><template v-slot:prepend><q-icon name="search" /></template></q-input>
      </div>
      <div class="col-12 col-md-7 row items-center q-gutter-xs">
        <span class="text-caption text-grey-7"><q-icon name="star" color="amber-7" /> Обране:</span>
        <q-chip v-for="f in favoriteRows" :key="'fav' + f.key" dense clickable removable color="amber-1" text-color="grey-9" :icon="isSelected(f) ? 'check_box' : 'check_box_outline_blank'" @click="toggle(f)" @remove="toggleFavorite(f)">{{ f.code }}</q-chip>
        <span v-if="!favoriteRows.length" class="text-caption text-grey-5">немає — натисніть ☆ біля рядка</span>
        <q-btn dense flat size="sm" color="grey-7" icon="clear_all" label="Очистити вибір" :disable="!selectedKeys.length" @click="clear" />
      </div>
    </div>

    <api-error-banner v-if="error" :message="error" @retry="load" />
    <div v-if="loading" class="text-center q-pa-md"><q-spinner color="primary" /></div>

    <div v-else class="matrix-scroll medlink-card" :style="{ maxHeight: height }">
      <q-markup-table dense flat class="matrix-table">
        <thead>
          <tr><th style="width: 36px" /><th style="width: 30px" /><th class="text-left">Код</th><th class="text-left">Назва</th><th class="text-left">Біоматеріал</th><th class="text-center">Пробірка</th><th class="text-right">Ціна</th><th class="text-center">TAT</th></tr>
        </thead>
        <tbody>
          <template v-for="section in groupedRows">
            <tr :key="'s' + section.name" class="matrix-section"><td colspan="8"><q-icon name="folder" size="14px" class="q-mr-xs" />{{ section.name }} <span class="text-grey-6">({{ section.count }})</span></td></tr>
            <template v-for="cat in section.categories">
              <tr v-if="cat.name" :key="'c' + section.name + cat.name" class="matrix-category"><td colspan="8">{{ cat.name }}</td></tr>
              <tr v-for="row in cat.rows" :key="row.key" :class="{ 'matrix-row--selected': isSelected(row), 'matrix-row--profile': row.kind === 'PROFILE' }" class="cursor-pointer" :data-testid="`matrix-${row.code}`" @click="toggle(row)">
                <td @click.stop><q-checkbox :value="isSelected(row)" dense @input="toggle(row)" /></td>
                <td @click.stop><q-icon :name="isFavorite(row) ? 'star' : 'star_border'" :color="isFavorite(row) ? 'amber-7' : 'grey-4'" size="18px" class="cursor-pointer" @click="toggleFavorite(row)" /></td>
                <td class="mono">{{ row.code }}<q-badge v-if="row.kind === 'PROFILE'" color="primary" label="профіль" class="q-ml-xs" /></td>
                <td><div>{{ row.name }}</div><div v-if="row.kind === 'PROFILE'" class="text-caption text-grey-6">{{ (row.memberTestCodes || []).join(', ') }}</div></td>
                <td class="text-caption">{{ row.biomaterialName || '—' }}</td>
                <td class="text-center"><div v-if="row.tubeColor" :style="{ background: row.tubeColor, width: '14px', height: '22px', borderRadius: '3px 3px 7px 7px', border: '1px solid #999', margin: '0 auto' }" :title="row.tubeName" /><span v-else class="text-grey-5">—</span></td>
                <td class="text-right">{{ row.price | money }}</td>
                <td class="text-center text-caption">{{ row.turnaroundHours ? row.turnaroundHours + ' год' : '—' }}</td>
              </tr>
            </template>
          </template>
          <tr v-if="!groupedRows.length"><td colspan="8" class="text-center text-grey-6 q-pa-md">Нічого не знайдено</td></tr>
        </tbody>
      </q-markup-table>
    </div>

    <!-- Підсумок -->
    <div class="row q-col-gutter-md q-mt-sm">
      <div class="col-12 col-md-7">
        <div class="medlink-card">
          <div class="medlink-card__title"><span><q-icon name="science" class="q-mr-xs" />План пробірок</span><span class="text-caption text-grey-6">{{ tubePlan.length }} пробірк.</span></div>
          <q-list v-if="tubePlan.length" dense separator>
            <q-item v-for="t in tubePlan" :key="t.key">
              <q-item-section avatar><div :style="{ background: t.color, width: '18px', height: '28px', borderRadius: '3px 3px 8px 8px', border: '1px solid #999' }" /></q-item-section>
              <q-item-section><q-item-label>{{ t.tubeName }} <span class="text-grey-7">· {{ t.biomaterialName }}</span></q-item-label><q-item-label caption>{{ t.tests.join(', ') }}</q-item-label></q-item-section>
              <q-item-section side><q-badge color="grey-7" :label="`${t.tests.length} тест.`" /></q-item-section>
            </q-item>
          </q-list>
          <div v-else class="q-pa-md text-caption text-grey-6">Оберіть показники — пробірки згрупуються автоматично.</div>
        </div>
      </div>
      <div class="col-12 col-md-5">
        <div class="medlink-card q-pa-md" data-testid="matrixSummary">
          <div class="row items-center justify-between"><span class="text-grey-7">Обрано рядків</span><b data-testid="matrixCount">{{ selectedKeys.length }}</b></div>
          <div class="row items-center justify-between"><span class="text-grey-7">Показників усього</span><b>{{ allTestCodes.length }}</b></div>
          <q-separator class="q-my-sm" />
          <div class="row items-center justify-between text-h6"><span>До сплати</span><span class="text-primary" data-testid="matrixTotal">{{ totalPrice | money }}</span></div>
          <slot name="actions" />
        </div>
      </div>
    </div>
  </div>
</template>

<script>
/**
 * Матриця призначень: GET /dictionaries/order-matrix → рядки { kind: PROFILE|TEST, id, code, name, section, category,
 * biomaterialName, tubeName, tubeColor, price, turnaroundHours, memberTestIds[], memberTestCodes[] }.
 * Fallback (404): будується з довідників profiles/tests/biomaterials/tube-types.
 */
import { LAB_SECTIONS } from '../../utils/statuses';

export default {
  name: 'OrderMatrix',
  props: {
    value: { type: Object, default: () => ({ profileIds: [], testIds: [] }) },
    height: { type: String, default: '46vh' }
  },
  data () { return { rows: [], loading: false, error: '', search: '', selectedKeys: [], favorites: [] }; },
  computed: {
    employeeId () { return this.$store.state.context.employeeId; },
    filteredRows () {
      const s = (this.search || '').toLowerCase();
      return this.rows.filter(r => !s || `${r.code} ${r.name} ${r.biomaterialName || ''} ${(r.memberTestCodes || []).join(' ')}`.toLowerCase().includes(s));
    },
    groupedRows () {
      const bySection = {};
      this.filteredRows.forEach(r => {
        const sec = r.section || 'Інше';
        bySection[sec] = bySection[sec] || {};
        const cat = r.category || '';
        bySection[sec][cat] = bySection[sec][cat] || [];
        bySection[sec][cat].push(r);
      });
      const order = s => { const i = LAB_SECTIONS.indexOf(s); return i < 0 ? 99 : i; };
      return Object.keys(bySection).sort((a, b) => order(a) - order(b) || a.localeCompare(b)).map(name => ({
        name,
        count: Object.values(bySection[name]).reduce((n, arr) => n + arr.length, 0),
        categories: Object.keys(bySection[name]).sort().map(cat => ({ name: cat, rows: bySection[name][cat].sort((a, b) => (a.kind === b.kind ? a.code.localeCompare(b.code) : (a.kind === 'PROFILE' ? -1 : 1))) }))
      }));
    },
    selectedRows () { return this.rows.filter(r => this.selectedKeys.includes(r.key)); },
    favoriteRows () { return this.rows.filter(r => this.favorites.includes(r.key)); },
    allTestCodes () {
      const codes = new Set();
      this.selectedRows.forEach(r => { if (r.kind === 'PROFILE') (r.memberTestCodes || []).forEach(c => codes.add(c)); else codes.add(r.code); });
      return [...codes];
    },
    totalPrice () {
      const inProfiles = new Set();
      this.selectedRows.filter(r => r.kind === 'PROFILE').forEach(r => (r.memberTestCodes || []).forEach(c => inProfiles.add(c)));
      return this.selectedRows.reduce((s, r) => s + ((r.kind === 'TEST' && inProfiles.has(r.code)) ? 0 : (Number(r.price) || 0)), 0);
    },
    tubePlan () {
      const groups = {};
      const testRows = this.rows.filter(r => r.kind === 'TEST');
      this.allTestCodes.forEach(code => {
        const t = testRows.find(r => r.code === code);
        if (!t) return;
        const key = `${t.biomaterialName || 'x'}|${t.tubeName || 'def'}`;
        if (!groups[key]) groups[key] = { key, biomaterialName: t.biomaterialName || 'Біоматеріал', tubeName: t.tubeName || 'Пробірка за замовчуванням', color: t.tubeColor || '#bbb', order: t.orderOfDrawIndex || 99, tests: [] };
        groups[key].tests.push(code);
      });
      return Object.values(groups).sort((a, b) => a.order - b.order);
    }
  },
  watch: {
    selectedKeys () { this.emitSelection(); },
    employeeId () { this.loadFavorites(); }
  },
  created () { this.load(); },
  methods: {
    isSelected (r) { return this.selectedKeys.includes(r.key); },
    isFavorite (r) { return this.favorites.includes(r.key); },
    toggle (r) { this.selectedKeys = this.isSelected(r) ? this.selectedKeys.filter(k => k !== r.key) : [...this.selectedKeys, r.key]; },
    clear () { this.selectedKeys = []; },
    emitSelection () {
      this.$emit('input', {
        profileIds: this.selectedRows.filter(r => r.kind === 'PROFILE').map(r => r.id),
        testIds: this.selectedRows.filter(r => r.kind === 'TEST').map(r => r.id),
        totalPrice: this.totalPrice,
        testCodes: this.allTestCodes
      });
    },
    async load () {
      this.loading = true; this.error = '';
      try {
        const res = await this.$api.orderMatrix();
        const list = Array.isArray(res) ? res : (res && (res.rows || res.items)) || [];
        this.rows = list.map(r => this.normalize(r));
      } catch (e) {
        if (e.apiStatus === 404) await this.buildFallback();
        else if (!e.isOffline) this.error = e.userMessage;
        else this.rows = [];
      } finally { this.loading = false; }
      this.loadFavorites();
    },
    normalize (r) {
      const kind = (r.kind || r.type || (r.memberTestIds || r.items ? 'PROFILE' : 'TEST')).toUpperCase();
      return {
        ...r,
        kind,
        key: `${kind}:${r.id}`,
        memberTestIds: r.memberTestIds || (r.items || []).map(i => i.testId),
        memberTestCodes: r.memberTestCodes || (r.items || []).map(i => i.testCode).filter(Boolean),
        tubeColor: r.tubeColor || r.tubeColorCode || (r.tube && r.tube.colorCode),
        tubeName: r.tubeName || (r.tube && r.tube.name),
        biomaterialName: r.biomaterialName || (r.biomaterial && r.biomaterial.name)
      };
    },
    async buildFallback () {
      await this.$store.dispatch('dictionaries/loadMany', ['profiles', 'tests', 'biomaterials', 'tube-types']);
      const g = n => this.$store.getters['dictionaries/items'](n);
      const bios = g('biomaterials'); const tubes = g('tube-types'); const tests = g('tests');
      const testRow = t => {
        const bio = bios.find(b => b.id === t.biomaterialTypeId);
        const tube = tubes.find(tt => bio && (tt.code === bio.defaultContainer || tt.name === bio.defaultContainer));
        return this.normalize({ kind: 'TEST', id: t.id, code: t.code, name: t.name, section: t.section || this.sectionOf(t.category), category: t.category, biomaterialName: bio && bio.name, tubeName: tube && tube.name, tubeColor: tube && tube.colorCode, orderOfDrawIndex: tube && tube.orderOfDrawIndex, price: t.price, turnaroundHours: t.turnaroundHours });
      };
      const profileRow = p => this.normalize({ kind: 'PROFILE', id: p.id, code: p.code, name: p.name, section: p.section || this.sectionOf(p.category), category: p.category, price: p.price, turnaroundHours: p.turnaroundHours, memberTestIds: (p.items || []).map(i => i.testId), memberTestCodes: (p.items || []).map(i => (tests.find(t => t.id === i.testId) || {}).code).filter(Boolean) });
      this.rows = [...g('profiles').filter(p => p.isActive !== false).map(profileRow), ...tests.filter(t => t.isActive !== false).map(testRow)];
    },
    sectionOf (category) {
      const c = (category || '').toLowerCase();
      if (c.includes('біохім')) return 'Біохімія';
      if (c.includes('гемат')) return 'Гематологія';
      if (c.includes('гормон') || c.includes('імун')) return 'Імунохімія';
      if (c.includes('коагул')) return 'Коагулологія';
      if (c.includes('сеч')) return 'Сеча';
      if (c.includes('мікроб')) return 'Мікробіологія';
      if (c.includes('патог') || c.includes('гістол')) return 'Патогістологія';
      if (c.includes('цитол')) return 'Цитологія';
      return category || 'Інше';
    },
    async loadFavorites () {
      if (!this.employeeId) return;
      try {
        const res = await this.$api.orderMatrixFavorites(this.employeeId);
        const list = Array.isArray(res) ? res : (res && (res.items || res.keys || res.favorites)) || [];
        this.favorites = list.map(f => typeof f === 'string' ? f : `${(f.kind || 'TEST').toUpperCase()}:${f.id}`);
      } catch (e) { /* 404 — обраного ще немає */ }
    },
    async toggleFavorite (r) {
      this.favorites = this.isFavorite(r) ? this.favorites.filter(k => k !== r.key) : [...this.favorites, r.key];
      try {
        await this.$api.saveOrderMatrixFavorites(this.employeeId, { items: this.favorites.map(k => { const [kind, id] = k.split(':'); return { kind, id }; }) });
      } catch (e) { this.$q.notify({ type: 'warning', message: 'Обране не збережено на сервері: ' + (e.userMessage || '') }); }
    }
  }
};
</script>

<style lang="stylus" scoped>
.matrix-scroll
  overflow auto
.matrix-table
  thead th
    position sticky
    top 0
    z-index 1
    background #f8fafc
.matrix-section td
  background #e8eef5
  font-weight 700
  color #1e3a5f
  text-transform uppercase
  font-size 12px
  letter-spacing .04em
.matrix-category td
  background #f8fafc
  font-weight 600
  color #475569
  font-size 12px
.matrix-row--selected td
  background #dce7f2 !important
.matrix-row--profile td
  font-weight 600
</style>
