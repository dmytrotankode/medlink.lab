<!--
  Шаблони бланків результатів: версії бланків за варіантами (основний, попередній, CITO),
  правка реквізитів (заголовок, примітка в шапці, підвал, мови) без дизайнера, активація версії.
  Перегляд — для персоналу лабораторії, зміни — лише адміністратор.
-->
<template>
  <div data-testid="reportTemplatesPage">
    <page-header title="Шаблони бланків результатів" icon="fas fa-file-alt" :breadcrumbs="[{ label: 'Довідники' }, { label: 'Шаблони бланків' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" :retryable="apiErrorRetryable" @retry="load" @dismiss="apiError = null" />
    <q-banner v-if="!canWrite" dense rounded class="bg-blue-1 text-grey-9 q-mb-md" data-testid="tplReadOnly">
      <template v-slot:avatar><q-icon name="visibility" color="primary" /></template>
      Режим перегляду: бланки змінює адміністратор лабораторії.
    </q-banner>

    <div v-for="g in groups" :key="g.variant" class="medlink-card q-mb-md" :data-testid="`tplGroup-${g.variant}`">
      <div class="medlink-card__title">
        <span><q-icon name="description" class="q-mr-xs" />{{ g.label }}</span>
        <span>
          <q-btn dense unelevated color="primary" icon="add" label="Нова версія" :disable="!canWrite" :data-testid="`tplNew-${g.variant}`" @click="openForm(null, g.variant)">
            <q-tooltip v-if="!canWrite">{{ roleHint() }}</q-tooltip>
          </q-btn>
        </span>
      </div>
      <q-markup-table flat dense>
        <thead><tr><th class="text-left">Версія</th><th class="text-left">Назва</th><th class="text-left">Заголовок</th><th class="text-left">Мови</th><th class="text-left">Статус</th><th class="text-right">Дії</th></tr></thead>
        <tbody>
          <tr v-for="t in g.items" :key="t.id" :class="{ 'text-grey-6': !t.isActive }">
            <td>{{ t.version }}</td>
            <td>{{ t.name }}</td>
            <td>{{ t.title || 'за замовчуванням' }}</td>
            <td>{{ langLabel(t.languages) }}</td>
            <td><q-badge :color="t.isActive ? 'positive' : 'grey-6'" :label="t.isActive ? 'Діє' : 'Неактивна'" /></td>
            <td class="text-right">
              <q-btn flat dense round size="sm" icon="edit" color="primary" :disable="!canWrite" :data-testid="`tplEdit-${t.id}`" @click="openForm(t)"><q-tooltip>{{ canWrite ? 'Реквізити' : roleHint() }}</q-tooltip></q-btn>
              <q-btn v-if="!t.isActive" flat dense round size="sm" icon="check_circle" color="positive" :disable="!canWrite" :data-testid="`tplActivate-${t.id}`" @click="activate(t)"><q-tooltip>Зробити діючою</q-tooltip></q-btn>
              <q-btn v-if="!t.isActive" flat dense round size="sm" icon="delete" color="grey-7" :disable="!canWrite" @click="askRemove(t)"><q-tooltip>Вилучити версію</q-tooltip></q-btn>
            </td>
          </tr>
          <tr v-if="!g.items.length"><td colspan="6" class="text-grey-6">Версій ще немає</td></tr>
        </tbody>
      </q-markup-table>
    </div>

    <q-dialog v-model="formOpen" persistent>
      <q-card style="min-width: 560px; max-width: 96vw" data-testid="tplForm">
        <q-card-section class="row items-center bg-primary text-white q-py-sm">
          <div class="text-subtitle1 text-weight-bold">{{ form.id ? 'Реквізити бланка' : 'Нова версія бланка' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-y-sm" style="max-height: 70vh; overflow: auto">
          <q-input v-model="form.name" outlined dense label="Назва версії" data-testid="tplName" />
          <q-input v-model="form.title" outlined dense label="Заголовок бланка" hint="Якщо порожньо — стандартний заголовок" data-testid="tplTitle" />
          <q-input v-model="form.headerNote" outlined dense type="textarea" autogrow label="Примітка в шапці (адреса, ліцензія тощо)" data-testid="tplHeader" />
          <q-input v-model="form.footerText" outlined dense type="textarea" autogrow label="Підвал бланка" data-testid="tplFooter" />
          <div>
            <div class="text-caption text-grey-8">Мови бланка</div>
            <q-option-group v-model="form.langs" type="checkbox" inline :options="langOptions" />
          </div>
          <q-input v-model="form.note" outlined dense label="Примітка до версії (внутрішня)" />
          <q-toggle v-if="!form.id" v-model="form.activate" label="Одразу зробити діючою" color="positive" />
          <div v-if="formError" class="text-negative">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="tplSave" @click="save" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import roleMixin from '../../mixins/roleMixin';

const VARIANTS = [
  { variant: 'final', label: 'Основний бланк' },
  { variant: 'preliminary', label: 'Попередній бланк' },
  { variant: 'cito', label: 'Бланк CITO (терміновий)' }
];

export default {
  name: 'ReportTemplatesPage',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      items: [], loading: false, formOpen: false, form: {}, formError: '', saving: false,
      langOptions: [{ label: 'Українська', value: 'uk' }, { label: 'Англійська', value: 'en' }]
    };
  },
  computed: {
    /** Зміни бланків — лише адміністратор лабораторії (серверна політика) */
    canWrite () { return this.canDo(); },
    groups () { return VARIANTS.map(v => ({ ...v, items: this.items.filter(t => t.variant === v.variant) })); }
  },
  created () { this.load(); },
  methods: {
    async load () {
      this.loading = true;
      const res = await this.callApi(() => this.$api.reportTemplates());
      if (res) this.items = this.asList(res);
      this.loading = false;
    },
    langLabel (l) { const a = String(l || 'uk').split(',').map(x => x.trim()); return a.map(x => (x === 'en' ? 'англ.' : 'укр.')).join(' + '); },
    openForm (t, variant) {
      this.formError = '';
      this.form = t
        ? { id: t.id, variant: t.variant, name: t.name, title: t.title || '', headerNote: t.headerNote || '', footerText: t.footerText || '', langs: String(t.languages || 'uk').split(',').map(x => x.trim()).filter(Boolean), note: t.note || '' }
        : { id: null, variant, name: '', title: '', headerNote: '', footerText: '', langs: ['uk'], note: '', activate: false };
      this.formOpen = true;
    },
    async save () {
      const f = this.form;
      if (!f.langs || !f.langs.length) { this.formError = 'Оберіть хоча б одну мову'; return; }
      this.saving = true; this.formError = '';
      try {
        const body = { variant: f.variant, name: f.name || null, title: f.title || null, headerNote: f.headerNote || null, footerText: f.footerText || null, languages: f.langs.join(','), note: f.note || null, activate: !!f.activate };
        await this.$api.saveReportTemplate(f.id, body);
        this.notifyOk(f.id ? 'Реквізити бланка збережено' : 'Нову версію бланка створено');
        this.formOpen = false; this.load();
      } catch (e) { this.formError = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    },
    async activate (t) {
      try { await this.$api.activateReportTemplate(t.id); this.notifyOk(`Версію ${t.version} зроблено діючою`); this.load(); } catch (e) { this.notifyError(e); }
    },
    askRemove (t) {
      this.$q.dialog({ title: 'Вилучити версію бланка?', message: `Версія ${t.version} «${t.name}» буде вилучена остаточно.`, cancel: 'Скасувати', ok: 'Вилучити' })
        .onOk(async () => { try { await this.$api.deleteReportTemplate(t.id); this.notifyOk('Версію вилучено'); this.load(); } catch (e) { this.notifyError(e); } });
    }
  }
};
</script>
