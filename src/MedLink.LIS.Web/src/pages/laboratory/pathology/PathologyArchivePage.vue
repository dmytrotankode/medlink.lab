<!--
  ТЗ 22.7, 22.14 п. 8 (FR-PAT-070…072): «Архів блоків і скелець» — місце зберігання, видача з актом (отримувач, документ, мета,
  строк повернення), повернення скануванням; прострочене повернення підсвічується.
-->
<template>
  <div data-testid="pathologyArchivePage">
    <page-header title="Архів блоків і скелець" icon="fas fa-archive" subtitle="Блоки підписаних справ: місце зберігання, видача на консультацію / молекулярне дослідження, повернення"
      :breadcrumbs="[{ label: 'Патоморфологія' }, { label: 'Архів' }]">
      <q-input v-model="q" dense outlined clearable debounce="300" placeholder="№ справи, ПІБ, штрихкод" style="min-width: 220px" class="q-mr-sm" />
      <q-btn-toggle v-model="status" dense unelevated toggle-color="primary" color="grey-2" text-color="grey-9" class="q-mr-sm"
        :options="[{ label: 'Усі', value: 'all' }, { label: 'В архіві', value: 'ARCHIVED' }, { label: 'Видано', value: 'ISSUED' }, { label: 'Прострочено', value: 'overdue' }]" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card">
      <q-table :data="rows" :columns="columns" row-key="id" flat dense :pagination="{ rowsPerPage: 50 }" :loading="loading" no-data-label="Блоків в архіві немає"
        :row-class="r => r.issue && r.issue.overdue ? 'bg-red-1' : ''">
        <template v-slot:body-cell-caseNo="p">
          <q-td :props="p"><router-link :to="{ name: 'lab-path-case', params: { id: p.row.caseId } }" class="text-primary text-weight-bold">{{ p.row.caseNo }}</router-link></q-td>
        </template>
        <template v-slot:body-cell-code="p">
          <q-td :props="p"><b>{{ p.row.code }}</b> <q-badge v-if="p.row.isExternal" outline color="indigo-6" label="зовнішній" /><div class="text-caption"><code>{{ p.row.barcode }}</code></div></q-td>
        </template>
        <template v-slot:body-cell-status="p">
          <q-td :props="p">
            <q-badge :color="p.row.status === 'ISSUED' ? 'orange-8' : 'positive'" :label="p.row.statusLabel" />
            <div v-if="p.row.issue" class="text-caption">{{ p.row.issue.recipient }} · {{ p.row.issue.purpose }}<br>видано {{ p.row.issue.issuedAt | date }}, повернути до
              <b :class="p.row.issue.overdue ? 'text-negative' : ''">{{ p.row.issue.dueBack | date }}</b></div>
            <div v-else-if="p.row.returnedAt" class="text-caption text-grey-7">повернено {{ p.row.returnedAt | datetime }}</div>
          </q-td>
        </template>
        <template v-slot:body-cell-actions="p">
          <q-td :props="p">
            <q-btn v-if="p.row.status === 'ARCHIVED'" flat dense size="sm" color="primary" icon="outbox" label="Видати" :data-testid="`issue-${p.row.code}`" @click="openIssue(p.row)" />
            <q-btn v-else flat dense size="sm" color="positive" icon="move_to_inbox" label="Повернення" @click="ret(p.row)" />
          </q-td>
        </template>
      </q-table>
    </div>

    <q-dialog v-model="issueOpen">
      <q-card style="min-width: 460px">
        <q-card-section class="text-h6">Видача блока {{ issueRow && issueRow.code }} ({{ issueRow && issueRow.caseNo }})</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input v-model="issue.recipient" dense outlined label="Отримувач (ПІБ / заклад) *" />
          <q-input v-model="issue.document" dense outlined label="Документ (акт, довіреність)" />
          <q-select v-model="issue.purpose" dense outlined :options="purposes" label="Мета *" />
          <q-input v-model="issue.dueBack" dense outlined type="date" label="Плановий строк повернення" stack-label />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Видати й сформувати акт" :disable="!issue.recipient || !issue.purpose" @click="doIssue" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

export default {
  name: 'PathologyArchivePage',
  mixins: [apiMixin],
  data () {
    return {
      rows: [], q: '', status: 'all', issueOpen: false, issueRow: null, issue: {},
      purposes: ['Консультація', 'Друга думка', 'Молекулярне дослідження', 'Клінічне дослідження'],
      columns: [
        { name: 'caseNo', label: '№ справи', field: 'caseNo', align: 'left', sortable: true },
        { name: 'patient', label: 'Пацієнт', field: 'patient', align: 'left' },
        { name: 'code', label: 'Блок', field: 'code', align: 'left' },
        { name: 'slides', label: 'Скелець', field: 'slides', align: 'center' },
        { name: 'location', label: 'Місце зберігання', field: 'storageLocation', align: 'left', classes: 'text-caption' },
        { name: 'status', label: 'Стан', field: 'status', align: 'left' },
        { name: 'actions', label: '', field: 'id', align: 'right' }
      ]
    };
  },
  watch: { q () { this.load(); }, status () { this.load(); } },
  created () { this.load(); },
  methods: {
    async load () {
      const r = await this.callApi(() => this.$api.pathArchive({ q: this.q, status: this.status }));
      if (r) this.rows = r;
    },
    openIssue (row) {
      const due = new Date(Date.now() + 14 * 86400000).toISOString().slice(0, 10);
      this.issueRow = row; this.issue = { recipient: '', document: '', purpose: 'Консультація', dueBack: due }; this.issueOpen = true;
    },
    async doIssue () {
      const r = await this.callApi(() => this.$api.pathIssue(this.issueRow.id, { ...this.issue, dueBack: this.issue.dueBack || null }), { success: 'Блок видано' });
      if (r) { this.issueOpen = false; await this.load(); }
    },
    async ret (row) {
      if (await this.callApi(() => this.$api.pathReturn(row.id), { success: `Блок ${row.code} повернено в архів` })) await this.load();
    }
  }
};
</script>
