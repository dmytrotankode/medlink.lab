<!--
  ТЗ 22.7, 22.14 п. 8 (FR-PAT-070…072): «Архів блоків і скелець» — місце зберігання, видача з актом (отримувач, документ, мета,
  строк повернення), повернення скануванням; прострочене повернення підсвічується.
-->
<template>
  <div data-testid="pathologyArchivePage">
    <page-header title="Архів блоків і скелець" icon="fas fa-archive" :breadcrumbs="[{ label: 'Патоморфологія' }, { label: 'Архів' }]">
      <q-input v-model="q" dense outlined clearable debounce="300" placeholder="№ справи, ПІБ, штрихкод" style="min-width: 220px" class="q-mr-sm" />
      <q-btn-toggle v-model="status" dense unelevated no-caps toggle-color="primary" color="white" text-color="grey-9" class="q-mr-sm status-toggle" data-testid="archiveStatus"
        :options="[{ label: 'Усі', value: 'all' }, { label: 'В архіві', value: 'ARCHIVED' }, { label: 'Видано', value: 'ISSUED' }, { label: 'Прострочено', value: 'overdue' }]" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" :retryable="apiErrorRetryable" @retry="load" @dismiss="apiError = null" />

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="id" flat dense class="archive-table" :pagination="{ rowsPerPage: 50 }" :loading="loading" no-data-label="Блоків в архіві немає"
        :row-class="r => r.issue && r.issue.overdue ? 'bg-red-1' : ''">
        <template v-slot:body-cell-caseNo="p">
          <q-td :props="p"><router-link :to="{ name: 'lab-path-case', params: { id: p.row.caseId } }" class="text-primary text-weight-bold">{{ p.row.caseNo }}</router-link></q-td>
        </template>
        <template v-slot:body-cell-code="p">
          <q-td :props="p"><b>{{ p.row.code }}</b> <q-badge v-if="p.row.isExternal" outline color="indigo-6" label="зовнішній" /><span v-if="p.row.kind === 'CYTO'" class="text-caption text-grey-7"> препарат</span><div class="text-caption"><code>{{ p.row.barcode }}</code></div></q-td>
        </template>
        <template v-slot:body-cell-status="p">
          <q-td :props="p">
            <q-badge :color="p.row.status === 'ISSUED' ? 'orange-8' : 'positive'" :label="p.row.statusLabel" />
            <div v-if="p.row.issue" class="text-caption"><q-badge v-if="p.row.issue.scope === 'INTERNAL'" outline color="teal-7" :label="`внутрішня · ${p.row.issue.recipientUnit}`" class="q-mr-xs" />{{ p.row.issue.recipient }} · {{ p.row.issue.purpose }}<br>видано {{ p.row.issue.issuedAt | date }}, повернути до
              <b :class="p.row.issue.overdue ? 'text-negative' : ''">{{ p.row.issue.dueBack | date }}</b></div>
            <div v-else-if="p.row.returnedAt" class="text-caption text-grey-7">повернено {{ p.row.returnedAt | datetime }}</div>
          </q-td>
        </template>
        <template v-slot:body-cell-actions="p">
          <q-td :props="p">
            <q-btn v-if="p.row.status === 'ARCHIVED'" flat dense no-caps size="sm" color="grey-8" icon="place" label="Місце" :disable="!canManage" :data-testid="`place-${p.row.code}`" @click="openPlace(p.row)"><q-tooltip>{{ canManage ? 'Місце зберігання' : roleHint('REGISTRAR', 'LAB_TECHNICIAN') }}</q-tooltip></q-btn>
            <q-btn v-if="p.row.status === 'ARCHIVED'" flat dense no-caps size="sm" color="primary" icon="outbox" label="Видати" :disable="!canManage" :data-testid="`issue-${p.row.code}`" @click="openIssue(p.row)"><q-tooltip v-if="!canManage">{{ roleHint('REGISTRAR', 'LAB_TECHNICIAN') }}</q-tooltip></q-btn>
            <q-btn v-else flat dense no-caps size="sm" color="positive" icon="move_to_inbox" label="Прийняти назад" :disable="!canManage" :data-testid="`return-${p.row.code}`" @click="ret(p.row)"><q-tooltip>{{ canManage ? 'Повернути блок в архів' : roleHint('REGISTRAR', 'LAB_TECHNICIAN') }}</q-tooltip></q-btn>
          </q-td>
        </template>
      </q-table>
    </div>

    <q-dialog v-model="issueOpen">
      <q-card style="min-width: 460px">
        <q-card-section class="text-h6">Видача {{ issueRow && issueRow.kind === 'CYTO' ? 'препарата' : 'блока' }} {{ issueRow && issueRow.code }} ({{ issueRow && issueRow.caseNo }})</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-btn-toggle v-model="issue.scope" dense unelevated toggle-color="primary" color="grey-2" text-color="grey-9" :options="[{ label: 'Зовнішня', value: 'EXTERNAL' }, { label: 'Внутрішня (підрозділ)', value: 'INTERNAL' }]" />
          <q-input v-if="issue.scope === 'INTERNAL'" v-model="issue.recipientUnit" dense outlined autofocus label="Підрозділ закладу *" data-testid="issueUnit" />
          <q-input v-model="issue.recipient" dense outlined :autofocus="issue.scope !== 'INTERNAL'" label="Отримувач (ПІБ / заклад) *" @keyup.enter="issueValid && doIssue()" />
          <q-input v-model="issue.document" dense outlined label="Документ (акт, довіреність)" />
          <q-select v-model="issue.purpose" dense outlined :options="purposes" label="Мета *" />
          <q-input v-model="issue.dueBack" dense outlined type="date" label="Плановий строк повернення" stack-label />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Видати" :disable="!issueValid" @click="doIssue" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="placeOpen">
      <q-card style="min-width: 400px">
        <q-card-section class="text-h6">Місце зберігання {{ placeRow && placeRow.kind === 'CYTO' ? 'препарата' : 'блока' }} {{ placeRow && placeRow.code }} ({{ placeRow && placeRow.caseNo }})</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-select v-model="place.roomId" dense outlined clearable emit-value map-options :options="roomOptions" label="Приміщення" />
          <q-input v-model="place.location" dense outlined autofocus label="Шафа / полиця / комірка" hint="Наприклад, шафа 2, полиця 2, комірка 0001" @keyup.enter="doPlace" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Зберегти" @click="doPlace" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';

export default {
  name: 'PathologyArchivePage',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      rows: [], q: '', status: 'all', issueOpen: false, issueRow: null, issue: {}, rooms: [], placeOpen: false, placeRow: null, place: {},
      purposes: ['Консультація', 'Друга думка', 'Молекулярне дослідження', 'Клінічне дослідження'],
      columns: [
        { name: 'caseNo', label: '№ справи', field: 'caseNo', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: 'patient', align: 'left' },
        { name: 'code', label: 'Блок / препарат', field: 'code', align: 'left' },
        { name: 'slides', label: 'Скелець', field: 'slides', align: 'center' },
        { name: 'location', label: 'Місце зберігання', field: r => [r.roomName, r.storageLocation].filter(Boolean).join(' · '), align: 'left', classes: 'text-caption' },
        { name: 'status', label: 'Стан', field: 'status', align: 'left' },
        { name: 'actions', label: '', field: 'id', align: 'right' }
      ]
    };
  },
  watch: { q () { this.load(); }, status () { this.load(); } },
  computed: {
    roomOptions () { return this.rooms.map(r => ({ label: r.name, value: r.id })); },
    canManage () { return this.canDo('REGISTRAR', 'LAB_TECHNICIAN'); }, // видача, повернення й місце зберігання
    issueValid () { return !!(this.issue.recipient && this.issue.recipient.trim() && this.issue.purpose && (this.issue.scope !== 'INTERNAL' || (this.issue.recipientUnit && this.issue.recipientUnit.trim()))); }
  },
  async created () { this.rooms = await this.$api.pathRooms().catch(() => []); this.load(); },
  methods: {
    async load () {
      const r = await this.callApi(() => this.$api.pathArchive({ q: this.q, status: this.status }));
      if (r) this.rows = r;
    },
    openIssue (row) {
      const due = new Date(Date.now() + 14 * 86400000).toISOString().slice(0, 10);
      this.issueRow = row; this.issue = { scope: 'EXTERNAL', recipientUnit: '', recipient: '', document: '', purpose: 'Консультація', dueBack: due }; this.issueOpen = true;
    },
    async doIssue () {
      const r = await this.callApi(() => this.$api.pathIssue(this.issueRow.id, { ...this.issue, dueBack: this.issue.dueBack || null }), { success: `${this.issueRow.kind === 'CYTO' ? 'Препарат' : 'Блок'} ${this.issueRow.code} видано: ${this.issue.recipient.trim()}` });
      if (r) { this.issueOpen = false; await this.load(); }
    },
    openPlace (row) { this.placeRow = row; this.place = { roomId: row.roomId || null, location: row.storageLocation || '' }; this.placeOpen = true; },
    async doPlace () {
      if (!this.canManage) return;
      if (await this.callApi(() => this.$api.pathSetStorage(this.placeRow.id, this.place), { success: 'Місце зберігання збережено' })) { this.placeOpen = false; await this.load(); }
    },
    async ret (row) {
      const what = row.kind === 'CYTO' ? 'препарат' : 'блок';
      const ok = await new Promise(resolve => this.$q.dialog({
        title: 'Прийняти назад?', message: `Повернути ${what} ${row.code} (${row.caseNo}) в архів? Видачу буде закрито.`,
        cancel: { label: 'Скасувати', flat: true }, ok: { label: 'Прийняти назад', color: 'positive' }, persistent: true
      }).onOk(() => resolve(true)).onCancel(() => resolve(false)).onDismiss(() => resolve(false)));
      if (!ok) return;
      if (await this.callApi(() => this.$api.pathReturn(row.id), { success: `${what.charAt(0).toUpperCase() + what.slice(1)} ${row.code} повернено в архів` })) await this.load();
    }
  }
};
</script>

<style scoped>
.status-toggle { border: 1px solid #c5ccd6; }
/* Колонка дій завжди на екрані: на вузьких вікнах таблиця прокручується, а кнопки лишаються праворуч */
.archive-table >>> th:last-child, .archive-table >>> td:last-child { position: sticky; right: 0; background: #fff; box-shadow: -4px 0 4px -4px rgba(0, 0, 0, 0.15); }
.archive-table >>> tr.bg-red-1 td:last-child { background: #ffebee; }
</style>
