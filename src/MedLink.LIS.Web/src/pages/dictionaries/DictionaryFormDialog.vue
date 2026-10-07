<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent>
    <q-card style="min-width: 720px; max-width: 96vw" :data-testid="`dictForm-${name}`">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon :name="schema.icon" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Редагування' : 'Створення' }}: {{ schema.title }}</div>
        <q-space /><q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>
      <q-card-section style="max-height: 70vh; overflow: auto">
        <q-banner v-if="schema.sourceHint && (form.organizationServiceId || name === 'profiles')" dense rounded class="bg-blue-1 text-grey-9 q-mb-sm"><q-icon name="link" color="primary" /> {{ schema.sourceHint }}</q-banner>
        <div class="row q-col-gutter-sm">
          <template v-for="f in visibleFields">
            <div :key="f.name" :class="`col-12 col-md-${f.col || 12}`">
              <!-- text / textarea / number / decimal -->
              <q-input v-if="f.type === 'text'" v-model="form[f.name]" outlined dense :label="f.label + (f.required ? ' *' : '')" :readonly="isReadonly(f)" :bg-color="isReadonly(f) ? 'grey-2' : undefined" :hint="f.hint" :data-testid="`f-${f.name}`" />
              <q-input v-else-if="f.type === 'textarea'" v-model="form[f.name]" outlined dense autogrow :label="f.label" :readonly="isReadonly(f)" :data-testid="`f-${f.name}`" />
              <q-input v-else-if="f.type === 'number'" v-model.number="form[f.name]" outlined dense type="number" :label="f.label" :readonly="isReadonly(f)" :data-testid="`f-${f.name}`" />
              <q-input v-else-if="f.type === 'decimal'" v-model="form[f.name]" outlined dense inputmode="decimal" :label="f.label" :readonly="isReadonly(f)" :bg-color="isReadonly(f) ? 'grey-2' : undefined" :data-testid="`f-${f.name}`" />
              <!-- toggle -->
              <q-toggle v-else-if="f.type === 'toggle'" v-model="form[f.name]" :label="f.label" color="positive" :data-testid="`f-${f.name}`" />
              <!-- color -->
              <q-input v-else-if="f.type === 'color'" v-model="form[f.name]" outlined dense :label="f.label" :data-testid="`f-${f.name}`">
                <template v-slot:prepend><div :style="{ background: form[f.name] || '#ccc', width: '22px', height: '22px', borderRadius: '50%', border: '1px solid #999' }" /></template>
                <template v-slot:append><q-icon name="colorize" class="cursor-pointer"><q-popup-proxy transition-show="scale" transition-hide="scale"><q-color v-model="form[f.name]" default-view="palette" no-header format-model="hex" /></q-popup-proxy></q-icon></template>
              </q-input>
              <!-- select -->
              <q-select v-else-if="f.type === 'select'" v-model="form[f.name]" outlined dense :label="f.label + (f.required ? ' *' : '')" :options="normalizeOptions(f.options)" emit-value map-options clearable :use-input="!!f.allowCustom" :new-value-mode="f.allowCustom ? 'add-unique' : undefined" :data-testid="`f-${f.name}`" />
              <!-- dict -->
              <q-select v-else-if="f.type === 'dict'" v-model="form[f.name]" outlined dense :label="f.label + (f.required ? ' *' : '')" :options="dictOptions(f)" emit-value map-options clearable use-input input-debounce="0" @filter="(val, update) => filterDict(f, val, update)" :data-testid="`f-${f.name}`" />
              <!-- chips -->
              <q-select v-else-if="f.type === 'chips'" v-model="form[f.name]" outlined dense multiple use-chips use-input hide-dropdown-icon new-value-mode="add-unique" :label="f.label + ' (Enter — додати)'" :options="[]" :data-testid="`f-${f.name}`" />
              <!-- profile items -->
              <div v-else-if="f.type === 'profileItems'">
                <div class="section-title">{{ f.label }}</div>
                <q-select v-model="profileTestIds" outlined dense multiple use-chips use-input input-debounce="0" label="Оберіть показники" :options="testOptionsFiltered" emit-value map-options @filter="filterTests" data-testid="f-items" />
                <q-markup-table v-if="(form.items || []).length" dense flat bordered class="q-mt-sm">
                  <thead><tr><th>#</th><th class="text-left">Показник</th><th>Обов’язковий</th><th /></tr></thead>
                  <tbody>
                    <tr v-for="(it, i) in form.items" :key="it.testId">
                      <td class="text-center">{{ i + 1 }}</td>
                      <td>{{ testLabel(it.testId) }}</td>
                      <td class="text-center"><q-checkbox v-model="it.isRequired" dense /></td>
                      <td class="text-right no-wrap">
                        <q-btn flat dense round size="sm" icon="arrow_upward" :disable="i === 0" @click="moveItem(i, -1)" />
                        <q-btn flat dense round size="sm" icon="arrow_downward" :disable="i === form.items.length - 1" @click="moveItem(i, 1)" />
                        <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="form.items.splice(i, 1)" />
                      </td>
                    </tr>
                  </tbody>
                </q-markup-table>
              </div>
              <q-input v-else v-model="form[f.name]" outlined dense :label="f.label" />
            </div>
          </template>
        </div>
        <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" v-close-popup />
        <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!valid" data-testid="dictSave" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { DICTIONARIES } from './dictionarySchemas';
import { parseDecimal } from '../../utils/format';

export default {
  name: 'DictionaryFormDialog',
  props: { value: Boolean, name: { type: String, required: true }, item: { type: Object, default: null } },
  data () { return { form: {}, saving: false, error: '', dictFilters: {}, testFilter: '' }; },
  computed: {
    schema () { return DICTIONARIES[this.name] || { title: this.name, fields: [], icon: 'list' }; },
    visibleFields () { return this.schema.fields.filter(f => !f.showIf || f.showIf(this.form)); },
    valid () { return this.schema.fields.filter(f => f.required).every(f => this.form[f.name] !== null && this.form[f.name] !== undefined && this.form[f.name] !== ''); },
    tests () { return this.$store.getters['dictionaries/items']('tests'); },
    testOptionsFiltered () { const f = this.testFilter.toLowerCase(); return this.tests.filter(t => !f || `${t.code} ${t.name}`.toLowerCase().includes(f)).map(t => ({ value: t.id, label: `${t.code} — ${t.name}` })); },
    profileTestIds: {
      get () { return (this.form.items || []).map(i => i.testId); },
      set (ids) {
        const existing = this.form.items || [];
        const next = ids.map((id, idx) => existing.find(e => e.testId === id) || { testId: id, displayOrder: idx + 1, isRequired: true });
        next.forEach((it, idx) => { it.displayOrder = idx + 1; });
        this.$set(this.form, 'items', next);
      }
    }
  },
  watch: {
    value (v) {
      if (v) {
        this.error = '';
        const base = {};
        this.schema.fields.forEach(f => { base[f.name] = f.default !== undefined ? f.default : (f.type === 'toggle' ? false : (f.type === 'chips' ? [] : (f.type === 'profileItems' ? [] : null))); });
        const src = this.item ? JSON.parse(JSON.stringify(this.item)) : {};
        this.form = { ...base, ...src };
        this.schema.fields.filter(f => f.type === 'decimal').forEach(f => { if (this.form[f.name] !== null && this.form[f.name] !== undefined) this.form[f.name] = String(this.form[f.name]).replace('.', ','); });
        if (this.schema.fields.some(f => f.type === 'profileItems')) { this.$store.dispatch('dictionaries/load', 'tests'); if (!Array.isArray(this.form.items)) this.$set(this.form, 'items', []); }
        this.schema.fields.filter(f => f.type === 'dict').forEach(f => this.$store.dispatch('dictionaries/load', f.dict));
      }
    }
  },
  methods: {
    isReadonly (f) { return !!(f.readonly || (f.readonlyIf && f.readonlyIf(this.form))); },
    normalizeOptions (opts) { return (opts || []).map(o => typeof o === 'string' ? { value: o, label: o } : o); },
    dictOptions (f) {
      const items = this.$store.getters['dictionaries/items'](f.dict);
      const q = (this.dictFilters[f.name] || '').toLowerCase();
      return items.filter(i => i.isActive !== false || i.id === this.form[f.name]).map(i => ({ value: i[f.valueField || 'id'], label: f.labelField ? (f.labelField === 'code' ? `${i.code} — ${i.name}` : i[f.labelField]) : (i.name || i.fullName || i.code) }))
        .filter(o => !q || String(o.label).toLowerCase().includes(q));
    },
    filterDict (f, val, update) { update(() => { this.$set(this.dictFilters, f.name, val || ''); }); },
    filterTests (val, update) { update(() => { this.testFilter = val || ''; }); },
    testLabel (id) { const t = this.tests.find(x => x.id === id); return t ? `${t.code} — ${t.name}` : id; },
    moveItem (i, d) { const arr = this.form.items; const j = i + d; if (j < 0 || j >= arr.length) return; const tmp = arr[i]; this.$set(arr, i, arr[j]); this.$set(arr, j, tmp); arr.forEach((it, idx) => { it.displayOrder = idx + 1; }); },
    async save () {
      this.saving = true; this.error = '';
      const body = { ...this.form };
      this.schema.fields.filter(f => f.type === 'decimal').forEach(f => { body[f.name] = parseDecimal(body[f.name]); });
      this.schema.fields.filter(f => f.type === 'select' && f.options && f.options[0] === '—').forEach(f => { if (body[f.name] === '—') body[f.name] = null; });
      const id = body.id; delete body.id;
      try {
        const res = id ? await this.$api.dictUpdate(this.name, id, body) : await this.$api.dictCreate(this.name, body);
        this.$q.notify({ type: 'positive', message: id ? 'Запис оновлено' : 'Запис створено' });
        this.$store.dispatch('dictionaries/invalidate', this.name);
        this.$emit('saved', res); this.$emit('input', false);
      } catch (e) { this.error = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    }
  }
};
</script>
