<template>
  <div class="import-page" data-testid="importPage">
    <page-header title="Імпорт результатів" icon="fas fa-file-import" subtitle="Пакетний імпорт від зовнішніх підрядників або автономних аналізаторів: CSV / XLSX / XML → попередній перегляд (dryRun) → застосування" :breadcrumbs="[{ label: 'Імпорт результатів' }]" />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <div class="medlink-card q-pa-md">
          <q-file v-model="file" outlined dense label="Файл CSV / XLSX / XML" accept=".csv,.xlsx,.xml,.txt" data-testid="importFile" @input="preview = null">
            <template v-slot:prepend><q-icon name="attach_file" /></template>
          </q-file>
          <div class="text-caption text-grey-7 q-mt-sm">Очікувані колонки CSV: <code>barcode;testCode;value;unit;measuredAt;flags</code>. Роздільник — «;» або «,», кодування UTF-8.</div>
          <div class="row q-gutter-sm q-mt-md">
            <q-btn outline color="primary" icon="preview" label="Перевірити (dryRun)" :disable="!file" :loading="busy" data-testid="importDryRun" @click="run(true)" />
            <q-btn color="positive" icon="publish" label="Застосувати" :disable="!file || !preview" :loading="busy" data-testid="importApply" @click="run(false)" />
          </div>
          <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
          <div v-if="applied" class="q-mt-md">
            <q-banner dense rounded class="bg-green-1 text-green-9"><q-icon name="check_circle" /> Імпортовано: прийнято {{ applied.accepted }}, зіставлено {{ applied.matched }}, не зіставлено {{ (applied.unmatched || []).length }}</q-banner>
          </div>
        </div>
      </div>
      <div class="col-12 col-md-8">
        <div class="medlink-card">
          <div class="medlink-card__title"><span>Попередній перегляд</span><span v-if="preview" class="text-caption text-grey-6">рядків: {{ rows.length }} · помилок: {{ errorsCount }}</span></div>
          <q-table v-if="preview" :data="rows" :columns="columns" dense flat row-key="_i" :pagination="{ rowsPerPage: 25 }" no-data-label="Рядків немає" data-testid="importPreview">
            <template v-slot:body="props">
              <q-tr :props="props" :class="{ 'bg-red-1': rowError(props.row), 'row-status--rejected': !rowError(props.row) && !props.row.matched && props.row.matched !== undefined }">
                <q-td v-for="c in columns" :key="c.name" :props="props">
                  <template v-if="c.name === 'status'">
                    <q-badge v-if="rowError(props.row)" color="negative" :label="rowError(props.row)" />
                    <q-badge v-else-if="props.row.matched === false" color="warning" text-color="dark" label="не зіставлено" />
                    <q-badge v-else color="positive" label="OK" />
                  </template>
                  <template v-else>{{ props.row[c.field] }}</template>
                </q-td>
              </q-tr>
            </template>
          </q-table>
          <empty-state v-else title="Завантажте файл і натисніть «Перевірити»" icon="table_view" hint="Сервер поверне розібрані рядки, зіставлення зі штрихкодами/тестами та помилки" />
          <div v-if="preview && (preview.warnings || preview.errors) && (preview.warnings || preview.errors).length" class="q-pa-sm">
            <q-banner v-for="(w, i) in (preview.warnings || preview.errors)" :key="i" dense class="bg-orange-1 text-orange-10 q-mb-xs">{{ typeof w === 'string' ? w : (w.message || JSON.stringify(w)) }}</q-banner>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';

export default {
  name: 'ImportResults',
  mixins: [apiMixin],
  data () { return { file: null, preview: null, applied: null, busy: false, error: '' }; },
  computed: {
    rows () {
      const p = this.preview; if (!p) return [];
      const list = Array.isArray(p) ? p : (p.rows || p.items || p.results || p.parsedResults || []);
      return list.map((r, i) => ({ _i: i, ...r }));
    },
    columns () {
      const first = this.rows[0];
      const keys = first ? Object.keys(first).filter(k => !['_i', 'matched', 'error', 'errors', 'orderTestId'].includes(k)) : ['barcode', 'testCode', 'value', 'unit', 'measuredAt', 'flags'];
      return [...keys.map(k => ({ name: k, label: k, field: k, align: 'left' })), { name: 'status', label: 'Статус', align: 'left' }];
    },
    errorsCount () { return this.rows.filter(r => this.rowError(r)).length; }
  },
  methods: {
    rowError (r) { if (r.error) return r.error; if (Array.isArray(r.errors) && r.errors.length) return r.errors.join('; '); return null; },
    async run (dryRun) {
      this.busy = true; this.error = '';
      const fd = new FormData(); fd.append('file', this.file);
      try {
        const res = await this.$api.importResults(fd, dryRun);
        if (dryRun) { this.preview = res; this.applied = null; } else { this.applied = res; this.notifyOk('Імпорт застосовано'); }
      } catch (e) { this.error = e.userMessage || 'Помилка імпорту'; } finally { this.busy = false; }
    }
  }
};
</script>
