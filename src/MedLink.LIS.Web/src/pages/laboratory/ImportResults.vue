<template>
  <div class="import-page" data-testid="importPage">
    <page-header title="Імпорт результатів" icon="fas fa-file-import" subtitle="Пакетний імпорт результатів від зовнішніх лабораторій або автономних аналізаторів: файл → перевірка → імпорт" :breadcrumbs="[{ label: 'Імпорт результатів' }]" />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <div class="medlink-card q-pa-md">
          <q-file v-model="file" outlined dense label="Файл CSV / XLSX / XML" accept=".csv,.xlsx,.xml,.txt" data-testid="importFile" :disable="!canImport" @input="preview = null; applied = null">
            <template v-slot:prepend><q-icon name="attach_file" /></template>
          </q-file>
          <div class="text-caption text-grey-7 q-mt-sm">
            Колонки: <code>barcode;testCode;value;unit;measuredAt;flags</code> — штрихкод; код показника; значення; одиниця; час вимірювання; прапорці. Назви колонок англійською не змінюйте. Роздільник — «;» або «,», кодування UTF-8.
            <a href="#" class="text-primary" @click.prevent="downloadSample">Завантажити зразок CSV</a>
          </div>
          <div v-if="!canImport" class="text-caption text-orange-9 q-mt-sm"><q-icon name="lock" /> {{ importHint }}</div>
          <div class="row q-gutter-sm q-mt-md">
            <q-btn outline color="primary" icon="preview" label="Перевірити файл" :disable="!file || !canImport" :loading="busy" data-testid="importDryRun" @click="run(true)" />
            <span class="inline-block">
              <q-btn color="positive" icon="publish" label="Імпортувати" :disable="!file || !preview || !readyCount || !canImport" :loading="busy" data-testid="importApply" @click="confirmApply" />
              <q-tooltip v-if="preview && !readyCount">Немає жодного рядка, готового до імпорту</q-tooltip>
              <q-tooltip v-else-if="!preview">Спершу перевірте файл</q-tooltip>
            </span>
          </div>
          <div v-if="error" class="text-negative q-mt-sm">{{ error }}</div>
          <div v-if="applied" class="q-mt-md">
            <q-banner dense rounded class="bg-green-1 text-green-9"><q-icon name="check_circle" /> Імпортовано рядків: {{ applied.applied }}; з помилками: {{ applied.errors }}; пропущено: {{ applied.skipped }}</q-banner>
          </div>
        </div>
      </div>
      <div class="col-12 col-md-8">
        <div class="medlink-card">
          <div class="medlink-card__title"><span>Попередній перегляд</span><span v-if="preview" class="text-caption text-grey-6">рядків: {{ rows.length }} · готово до імпорту: {{ readyCount }} · помилок: {{ errorsCount }} · пропущено: {{ skippedCount }}</span></div>
          <q-table v-if="preview" :data="rows" :columns="columns" dense flat row-key="line" :pagination="{ rowsPerPage: 25 }" no-data-label="Рядків немає" data-testid="importPreview">
            <template v-slot:body-cell-state="props">
              <q-td :props="props">
                <q-badge v-if="props.row.status === 'ERROR'" color="negative" :label="props.row.message || 'Помилка'" />
                <q-badge v-else-if="props.row.status === 'SKIP'" color="warning" text-color="dark" :label="props.row.message || 'Пропущено'" />
                <q-badge v-else-if="props.row.status === 'OVERWRITE'" color="orange-7" label="Замінить наявний результат" />
                <q-badge v-else-if="props.row.status === 'APPLIED'" color="positive" label="Імпортовано" />
                <q-badge v-else color="positive" label="Готово до імпорту" />
              </q-td>
            </template>
          </q-table>
          <empty-state v-else title="Завантажте файл і натисніть «Перевірити файл»" icon="table_view" hint="Буде показано розібрані рядки, зіставлення зі штрихкодами й показниками та помилки" />
          <div v-if="preview && preview.warnings && preview.warnings.length" class="q-pa-sm">
            <q-banner v-for="(w, i) in preview.warnings" :key="i" dense class="bg-orange-1 text-orange-10 q-mb-xs">{{ typeof w === 'string' ? w : (w.message || 'Є зауваження до файлу') }}</q-banner>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import roleMixin from '../../mixins/roleMixin';
import { downloadBlob } from '../../utils/format';

const IMPORT_ROLES = ['LAB_DOCTOR', 'LAB_TECHNICIAN'];

export default {
  name: 'ImportResults',
  mixins: [apiMixin, roleMixin],
  data () { return { file: null, preview: null, applied: null, busy: false, error: '' }; },
  computed: {
    // Відображення політики сервера: імпорт — адміністратор, лікар-лаборант, лаборант
    canImport () { return this.canDo(...IMPORT_ROLES); },
    importHint () { return 'Імпорт доступний лабораторній команді: адміністратору, лікарю-лаборанту, лаборанту'; },
    rows () { return ((this.preview && this.preview.rows) || []).map(r => ({ ...r })); },
    // Явні колонки: сирі назви полів і службові ідентифікатори на екран не виводимо
    columns () {
      return [
        { name: 'line', label: 'Рядок', field: 'line', align: 'left' },
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left' },
        { name: 'testCode', label: 'Код показника', field: 'testCode', align: 'left' },
        { name: 'value', label: 'Значення', field: 'value', align: 'right' },
        { name: 'unit', label: 'Од. вим.', field: r => r.unit || '—', align: 'left' },
        { name: 'flags', label: 'Прапорці', field: r => r.flags || '—', align: 'left' },
        { name: 'orderNumber', label: 'Замовлення', field: r => r.orderNumber || '—', align: 'left' },
        { name: 'patientName', label: 'Пацієнт', field: r => r.patientName || '—', align: 'left' },
        { name: 'state', label: 'Результат перевірки', align: 'left' }
      ];
    },
    readyCount () { return this.rows.filter(r => r.status === 'READY' || r.status === 'OVERWRITE').length; },
    errorsCount () { return this.rows.filter(r => r.status === 'ERROR').length; },
    skippedCount () { return this.rows.filter(r => r.status === 'SKIP').length; }
  },
  methods: {
    downloadSample () {
      const csv = 'barcode;testCode;value;unit;measuredAt;flags\n10260048;GLU;5,4;ммоль/л;2026-10-07 08:14;N\n10260048;ALT;28;Од/л;2026-10-07 08:14;N\n';
      downloadBlob(new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' }), 'zrazok-import-rezultativ.csv');
    },
    /** Імпорт змінює дані пацієнтів і не скасовується — показуємо, скільки рядків буде імпортовано */
    confirmApply () {
      const total = this.rows.length; const ready = this.readyCount; const rest = total - ready;
      const message = `Буде імпортовано рядків: ${ready} з ${total}${rest ? `; решту (${rest}) буде пропущено` : ''}. Імпортовані результати потраплять у картки пацієнтів; скасувати імпорт не можна.`;
      this.$q.dialog({ title: 'Імпортувати результати?', message, cancel: { label: 'Скасувати', flat: true }, ok: { label: 'Імпортувати', color: 'positive' }, focus: 'cancel' })
        .onOk(() => this.run(false));
    },
    async run (dryRun) {
      this.busy = true; this.error = '';
      const fd = new FormData(); fd.append('file', this.file);
      try {
        const res = await this.$api.importResults(fd, dryRun);
        if (dryRun) { this.preview = res; this.applied = null; } else { this.applied = res; this.preview = res; this.notifyOk('Імпорт виконано'); }
      } catch (e) { this.error = e.userMessage || 'Не вдалося виконати імпорт'; } finally { this.busy = false; }
    }
  }
};
</script>
