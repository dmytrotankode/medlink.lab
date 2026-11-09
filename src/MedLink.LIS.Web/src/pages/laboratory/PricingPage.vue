<template>
  <div data-testid="pricingPage">
    <page-header title="Прайси та платники" icon="fas fa-file-invoice-dollar" subtitle="Платники (каса, страхові програми, НСЗУ, клініки-партнери) та прайс-листи" :breadcrumbs="[{ label: 'Прайси та платники' }]">
      <q-badge v-if="hasDirty" color="orange-8" label="Є незбережені зміни" class="q-mr-sm" data-testid="priceDirtyBadge" />
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="reload" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />
    <div class="medlink-card">
      <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8">
        <q-tab name="payers" icon="account_balance" :label="`Платники (${payers.length})`" />
        <q-tab name="lists" icon="list_alt" :label="`Прайс-листи (${lists.length})`" />
      </q-tabs>
      <q-separator />
      <div v-show="tab === 'payers'" class="q-pa-sm">
        <div class="row items-center q-mb-xs">
          <q-toggle v-model="showArchive" dense label="Показувати архівні" color="primary" data-testid="payerArchiveToggle" />
          <q-space />
          <span><q-btn unelevated dense color="primary" icon="add" label="Додати платника" :disable="!canEditPrices" data-testid="payerCreate" @click="openPayer(null)"><q-tooltip v-if="!canEditPrices">{{ roleHint() }}</q-tooltip></q-btn></span>
        </div>
        <q-table :data="visiblePayers" :columns="payerColumns" row-key="id" dense flat :pagination="{ rowsPerPage: 50 }">
          <template v-slot:body-cell-actions="props">
            <q-td :props="props" class="text-right no-wrap">
              <q-btn flat dense round size="sm" icon="edit" color="primary" :disable="!canEditPrices" :data-testid="`payerEdit-${props.row.code}`" @click="openPayer(props.row)"><q-tooltip>{{ canEditPrices ? 'Редагувати' : roleHint() }}</q-tooltip></q-btn>
              <q-btn v-if="props.row.isActive === false" flat dense round size="sm" icon="restore_from_trash" color="positive" :disable="!canEditPrices" @click="restorePayer(props.row)"><q-tooltip>Повернути з архіву</q-tooltip></q-btn>
              <q-btn v-else-if="props.row.kind !== 'PATIENT'" flat dense round size="sm" icon="delete" color="grey-7" :disable="!canEditPrices" :data-testid="`payerDelete-${props.row.code}`" @click="askRemove('payer', props.row)"><q-tooltip>{{ canEditPrices ? 'В архів або видалити' : roleHint() }}</q-tooltip></q-btn>
            </q-td>
          </template>
          <template v-slot:body-cell-name="props"><q-td :props="props">{{ props.row.name }} <q-badge v-if="props.row.isActive === false" color="grey-6" label="архів" class="q-ml-xs" /></q-td></template>
          <template v-slot:body-cell-kind="props"><q-td :props="props"><q-badge :color="kindColor(props.row.kind)" :label="props.row.kindName" /></q-td></template>
          <template v-slot:body-cell-terms="props">
            <q-td :props="props" class="text-caption">
              покриття {{ props.row.coveragePct }}%<span v-if="props.row.franchiseAmount"> · франшиза {{ props.row.franchiseAmount | money }}</span>
              <span v-if="props.row.requiresPolicy"> · поліс обов’язковий</span><span v-if="props.row.requiresAuthorization"> · гарантійний лист обов’язковий</span>
            </q-td>
          </template>
        </q-table>
      </div>
      <div v-show="tab === 'lists'" class="q-pa-sm">
        <div class="row items-center q-mb-xs">
          <q-toggle v-model="showArchive" dense label="Показувати архівні" color="primary" />
          <q-space />
          <span><q-btn unelevated dense color="primary" icon="add" label="Додати прайс-лист" :disable="!canEditPrices" data-testid="listCreate" @click="openList(null)"><q-tooltip v-if="!canEditPrices">{{ roleHint() }}</q-tooltip></q-btn></span>
        </div>
        <q-list separator>
          <q-expansion-item v-for="l in visibleLists" :key="l.id" group="pl">
            <template v-slot:header>
              <q-item-section avatar><q-icon :name="l.isDefault ? 'storefront' : 'description'" :color="l.isDefault ? 'positive' : 'primary'" /></q-item-section>
              <q-item-section>
                <q-item-label><b>{{ l.name }}</b> <span class="text-grey-6">· {{ l.code }}</span> <q-badge v-if="l.isDefault" color="positive" label="роздрібний" class="q-ml-xs" /> <q-badge v-if="dirty[l.id]" color="orange-8" label="не збережено" class="q-ml-xs" /> <q-badge v-if="l.isActive === false" color="grey-6" label="архів" class="q-ml-xs" /></q-item-label>
                <q-item-label caption>{{ l.validFrom | date }} — {{ l.validTo ? $options.filters.date(l.validTo) : 'безстроково' }} · <span v-if="l.isDefault && !l.items.length">ціни беруться з прайсу МІС</span><span v-else>позицій {{ l.itemsCount }}</span> · пакетів {{ l.packages.length }}</q-item-label>
              </q-item-section>
            </template>
            <div class="q-pa-sm bg-grey-1">
              <div v-if="l.isDefault && !l.items.length" class="text-caption text-grey-7 q-mb-sm">Роздрібні ціни беруться з прайсу послуг МІС; змінити їх можна там.</div>
              <q-markup-table v-if="l.items.length" dense flat bordered class="q-mb-sm">
                <thead><tr><th class="text-left">Послуга / показник</th><th class="text-right" style="width:190px">Ціна, грн</th></tr></thead>
                <tbody>
                  <tr v-for="i in l.items" :key="i.id">
                    <td>{{ itemCode(i) }} <span class="text-grey-7">{{ itemTitle(i) }}</span></td>
                    <td class="text-right">
                      <q-input
                        v-model.number="i.price"
                        dense
                        outlined
                        hide-bottom-space
                        input-class="text-right"
                        type="number"
                        min="0"
                        step="0.01"
                        suffix="грн"
                        :readonly="!canEditPrices"
                        :error="!!priceError(i)"
                        :error-message="priceError(i)"
                        :hint="!priceError(i) && Number(i.price) === 0 ? 'Ціну не задано' : undefined"
                        @input="markDirty(l)"
                      />
                    </td>
                  </tr>
                </tbody>
              </q-markup-table>
              <div v-for="p in l.packages" :key="p.id" class="text-caption q-mb-xs">
                <q-icon name="inventory_2" color="primary" /> Пакет <b>{{ p.name }}</b> · {{ p.code }} — {{ p.price | money }} <q-badge v-if="p.isActive === false" color="grey-6" label="архів" />
                <q-btn flat dense round size="xs" icon="edit" color="primary" :disable="!canEditPrices" :data-testid="`pkgEdit-${p.code}`" @click="openPackage(l, p)" />
                <q-btn flat dense round size="xs" icon="delete" color="grey-7" :disable="!canEditPrices" :data-testid="`pkgDelete-${p.code}`" @click="removePackage(l, p)" />
                <ul class="q-my-none"><li v-for="m in p.members" :key="m">{{ itemName({ profileId: m, testId: m }) }}</li></ul>
              </div>
              <div class="row q-gutter-xs q-mt-xs">
                <span><q-btn dense outline color="primary" icon="inventory_2" label="Додати пакет" :disable="!canEditPrices" data-testid="pkgCreate" @click="openPackage(l, null)" /></span>
                <span><q-btn dense outline color="primary" icon="upload_file" label="Оновити ціни з файлу" :disable="!canEditPrices" data-testid="priceImport" @click="openImport(l)" /></span>
                <span><q-btn dense outline color="grey-8" icon="edit" label="Реквізити прайсу" :disable="!canEditPrices" data-testid="listEdit" @click="openList(l)" /></span>
                <span><q-btn v-if="l.isActive === false" dense outline color="positive" icon="restore_from_trash" label="З архіву" :disable="!canEditPrices" @click="restoreList(l)" /><q-btn v-else-if="!l.isDefault" dense outline color="grey-8" icon="delete" label="В архів / видалити" :disable="!canEditPrices" data-testid="listDelete" @click="askRemove('list', l)" /></span>
              </div>
              <div v-if="!canEditPrices && l.items.length" class="text-caption text-grey-7">Змінювати ціни може адміністратор лабораторії.</div>
              <q-btn v-if="dirty[l.id] && canEditPrices" dense unelevated color="primary" icon="save" label="Зберегти ціни" class="q-mt-sm" data-testid="savePricesBtn" :disable="!!listError(l)" @click="saveList(l)">
                <q-tooltip v-if="listError(l)">{{ listError(l) }}</q-tooltip>
              </q-btn>
            </div>
          </q-expansion-item>
        </q-list>
      </div>
    </div>

    <!-- платник -->
    <q-dialog v-model="payerOpen" persistent>
      <q-card style="min-width: 640px; max-width: 96vw" data-testid="payerForm">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">{{ payerForm.id ? 'Платник' : 'Новий платник' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="row q-col-gutter-sm" style="max-height: 70vh; overflow: auto">
          <div class="col-4"><q-input v-model="payerForm.code" outlined dense label="Код *" data-testid="pf-code" /></div>
          <div class="col-8"><q-input v-model="payerForm.name" outlined dense label="Назва платника / програми *" data-testid="pf-name" /></div>
          <div class="col-6"><q-select v-model="payerForm.kind" outlined dense emit-value map-options label="Тип *" :options="kindOptions" /></div>
          <div class="col-6"><q-select v-model="payerForm.priceListId" outlined dense emit-value map-options clearable label="Прайс-лист" :options="listOptions" /></div>
          <div class="col-8"><q-input v-model="payerForm.organizationName" outlined dense label="Юридична особа" /></div>
          <div class="col-4"><q-input v-model="payerForm.edrpou" outlined dense label="ЄДРПОУ" /></div>
          <div class="col-4"><q-input v-model="payerForm.contractNumber" outlined dense label="Номер договору" /></div>
          <div class="col-4"><q-input v-model="payerForm.contractDate" outlined dense type="date" label="Дата договору" /></div>
          <div class="col-4"><q-input v-model="payerForm.contractValidTo" outlined dense type="date" label="Договір діє до" /></div>
          <div class="col-4"><q-input v-model.number="payerForm.coveragePct" outlined dense type="number" min="0" max="100" suffix="%" label="Покриття платником" /></div>
          <div class="col-4"><q-input v-model.number="payerForm.franchiseAmount" outlined dense type="number" min="0" suffix="грн" label="Франшиза" /></div>
          <div class="col-4"><q-input v-model="payerForm.contactPerson" outlined dense label="Контактна особа" /></div>
          <div class="col-6"><q-input v-model="payerForm.phone" outlined dense label="Телефон" /></div>
          <div class="col-6"><q-input v-model="payerForm.email" outlined dense label="E-mail" /></div>
          <div class="col-4"><q-toggle v-model="payerForm.requiresPolicy" label="Потрібен поліс" color="positive" /></div>
          <div class="col-4"><q-toggle v-model="payerForm.requiresAuthorization" label="Потрібен гарантійний лист" color="positive" /></div>
          <div class="col-4"><q-toggle v-model="payerForm.isActive" label="Активний" color="positive" /></div>
          <div v-if="formError" class="col-12 text-negative">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="payerSave" @click="savePayer" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- реквізити прайс-листа -->
    <q-dialog v-model="listOpen" persistent>
      <q-card style="min-width: 520px; max-width: 96vw" data-testid="listForm">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">{{ listForm.id ? 'Реквізити прайс-листа' : 'Новий прайс-лист' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="row q-col-gutter-sm">
          <div class="col-4"><q-input v-model="listForm.code" outlined dense label="Код *" data-testid="lf-code" /></div>
          <div class="col-8"><q-input v-model="listForm.name" outlined dense label="Назва *" data-testid="lf-name" /></div>
          <div class="col-6"><q-input v-model="listForm.validFrom" outlined dense type="date" label="Діє з" /></div>
          <div class="col-6"><q-input v-model="listForm.validTo" outlined dense type="date" label="Діє до" /></div>
          <div class="col-6"><q-toggle v-model="listForm.isDefault" label="Роздрібний (за замовчуванням)" color="positive" /></div>
          <div class="col-6"><q-toggle v-model="listForm.isActive" label="Активний" color="positive" /></div>
          <div v-if="formError" class="col-12 text-negative">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="listSave" @click="saveListForm" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- пакет послуг -->
    <q-dialog v-model="pkgOpen" persistent>
      <q-card style="min-width: 560px; max-width: 96vw" data-testid="pkgForm">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">{{ pkgForm.id ? 'Пакет послуг' : 'Новий пакет послуг' }} · {{ pkgList && pkgList.name }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="row q-col-gutter-sm">
          <div class="col-4"><q-input v-model="pkgForm.code" outlined dense label="Код *" data-testid="pk-code" /></div>
          <div class="col-8"><q-input v-model="pkgForm.name" outlined dense label="Назва *" data-testid="pk-name" /></div>
          <div class="col-12"><q-select v-model="pkgForm.members" outlined dense multiple use-chips use-input input-debounce="0" emit-value map-options label="Склад пакета * (щонайменше дві послуги)" :options="memberOptionsFiltered" @filter="(v, u) => u(() => { memberFilter = v || ''; })" data-testid="pk-members" /></div>
          <div class="col-6"><q-input v-model.number="pkgForm.price" outlined dense type="number" min="0" step="0.01" suffix="грн" label="Пакетна ціна *" data-testid="pk-price" /></div>
          <div class="col-6"><q-toggle v-model="pkgForm.isActive" label="Активний" color="positive" /></div>
          <div v-if="formError" class="col-12 text-negative">{{ formError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" icon="save" label="Зберегти" :loading="saving" data-testid="pkgSave" @click="savePackage" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- масове оновлення цін файлом -->
    <q-dialog v-model="importOpen">
      <q-card style="min-width: 620px; max-width: 96vw" data-testid="priceImportDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">Оновлення цін з файлу · {{ importList && importList.name }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div class="text-caption text-grey-8">Файл CSV із колонками <b>code</b> (код послуги або показника) та <b>price</b> (ціна), роздільник «;» або «,», до 5000 рядків. Існуючі позиції оновлюються, нові додаються, решта прайсу не змінюється.</div>
          <q-file v-model="importFile" outlined dense label="Файл CSV" accept=".csv,.txt" @input="importPreview = null" data-testid="priceImportFile"><template v-slot:prepend><q-icon name="attach_file" /></template></q-file>
          <div class="row q-gutter-sm">
            <q-btn outline color="primary" label="Попередній перегляд" :disable="!importFile" :loading="importing" data-testid="priceImportPreview" @click="runPriceImport(true)" />
            <q-btn color="primary" label="Застосувати" :disable="!importFile || !importPreview || !importPreview.dryRun || (importPreview.create + importPreview.update) === 0" :loading="importing" data-testid="priceImportRun" @click="runPriceImport(false)" />
          </div>
          <div v-if="importPreview">
            <div class="row q-gutter-xs q-mb-xs">
              <q-badge color="positive" :label="`Нових позицій: ${importPreview.create}`" /><q-badge color="primary" :label="`Змінених цін: ${importPreview.update}`" />
              <q-badge color="grey-7" :label="`Пропущено: ${importPreview.skip}`" /><q-badge :color="importPreview.error ? 'negative' : 'grey-5'" :label="`З помилками: ${importPreview.error}`" />
            </div>
            <q-markup-table dense flat bordered style="max-height: 260px; overflow: auto">
              <thead><tr><th class="text-left">Рядок</th><th class="text-left">Код</th><th class="text-left">Дія</th><th class="text-left">Пояснення</th></tr></thead>
              <tbody><tr v-for="(r, i) in importPreview.rows" :key="i"><td>{{ r.line }}</td><td>{{ r.key || '—' }}</td><td><q-badge :color="{ CREATE: 'positive', UPDATE: 'primary', ERROR: 'negative', SKIP: 'grey-6' }[r.action]" :label="{ CREATE: 'Додати', UPDATE: 'Змінити', ERROR: 'Помилка', SKIP: 'Пропустити' }[r.action]" /></td><td class="text-caption">{{ r.reason || '' }}</td></tr></tbody>
            </q-markup-table>
          </div>
        </q-card-section>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import roleMixin from '../../mixins/roleMixin';
import { ACTION_ROLES } from '../../utils/menuAccess';
import { formatDate } from '../../utils/format';

export default {
  name: 'PricingPage',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      tab: 'payers', payers: [], lists: [], dirty: {}, showArchive: false, saving: false, formError: '',
      payerOpen: false, payerForm: {}, listOpen: false, listForm: {}, pkgOpen: false, pkgForm: {}, pkgList: null, memberFilter: '',
      importOpen: false, importList: null, importFile: null, importPreview: null, importing: false,
      payerColumns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left' },
        { name: 'name', label: 'Платник / програма', field: 'name', align: 'left' },
        { name: 'kind', label: 'Тип', field: 'kindName', align: 'left' },
        { name: 'org', label: 'Юр. особа / ЄДРПОУ', field: r => [r.organizationName, r.edrpou].filter(Boolean).join(' · ') || '—', align: 'left' },
        { name: 'contract', label: 'Договір', field: r => r.contractNumber ? `${r.contractNumber}${r.contractValidTo ? ' до ' + formatDate(r.contractValidTo) : ''}` : '—', align: 'left' },
        { name: 'list', label: 'Прайс-лист', field: r => r.priceListName || (r.kind === 'INTERNAL' ? 'усі послуги' : '—'), align: 'left' },
        { name: 'terms', label: 'Умови', field: 'coveragePct', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ]
    };
  },
  computed: {
    profiles () { return this.$store.getters['dictionaries/items']('profiles'); },
    tests () { return this.$store.getters['dictionaries/items']('tests'); },
    canEditPrices () { return this.canDo(...ACTION_ROLES.priceEdit); },
    visiblePayers () { return this.payers.filter(p => this.showArchive || p.isActive !== false); },
    visibleLists () { return this.lists.filter(l => this.showArchive || l.isActive !== false); },
    kindOptions () { return [['INSURANCE', 'Страхова програма'], ['ENTERPRISE', 'Підприємство'], ['CLINIC', 'Клініка-партнер'], ['NSZU', 'НСЗУ'], ['INTERNAL', 'Внутрішнє'], ['PATIENT', 'Пацієнт']].map(([value, label]) => ({ value, label })); },
    listOptions () { return this.lists.filter(l => l.isActive !== false).map(l => ({ value: l.id, label: l.name })); },
    memberOptions () { return [...this.profiles.map(p => ({ value: p.code, label: `${p.code} — ${p.name}` })), ...this.tests.map(t => ({ value: t.code, label: `${t.code} — ${t.name}` }))]; },
    memberOptionsFiltered () { const f = this.memberFilter.toLowerCase(); return this.memberOptions.filter(o => !f || o.label.toLowerCase().includes(f)); },
    hasDirty () { return Object.keys(this.dirty).some(k => this.dirty[k]); }
  },
  created () { this.$store.dispatch('dictionaries/loadMany', ['profiles', 'tests']); this.load(); },
  // Прапорець «змінено» ставиться через this.$set (у Vue 2 нова властивість звичайного об'єкта нереактивна),
  // тому кнопка «Зберегти ціни» з'являється одразу після правки.
  beforeRouteLeave (to, from, next) {
    if (!this.hasDirty) return next();
    this.$q.dialog({ title: 'Незбережені зміни', message: 'Ціни змінено, але не збережено. Вийти без збереження?', cancel: 'Лишитися', ok: 'Вийти без збереження', persistent: true })
      .onOk(() => next()).onCancel(() => next(false));
  },
  methods: {
    kindColor (k) { return { PATIENT: 'grey-7', INSURANCE: 'purple-5', NSZU: 'teal-6', CLINIC: 'indigo-5', ENTERPRISE: 'brown-5', INTERNAL: 'blue-grey-5' }[k] || 'grey'; },
    itemRef (i) {
      const p = this.profiles.find(x => x.id === i.profileId);
      if (p) return p;
      return this.tests.find(x => x.id === i.testId) || null;
    },
    itemCode (i) { const r = this.itemRef(i); return r ? r.code : (i.profileId || i.testId); },
    itemTitle (i) { const r = this.itemRef(i); return r ? r.name : ''; },
    itemName (i) {
      const r = this.itemRef(i);
      return r ? `${r.name} · ${r.code}` : (i.profileId || i.testId);
    },
    markDirty (l) { this.$set(this.dirty, l.id, true); },
    /** Помилка ціни: порожньо, не число або від'ємне значення */
    priceError (i) {
      const v = i.price;
      if (v === '' || v === null || v === undefined) return 'Вкажіть ціну від 0 грн';
      const n = Number(v);
      if (!isFinite(n)) return 'Ціна має бути числом';
      if (n < 0) return 'Ціна не може бути від’ємною';
      return '';
    },
    listError (l) {
      const bad = l.items.filter(i => this.priceError(i)).length;
      return bad ? `Виправте ціни: помилок ${bad}` : '';
    },
    reload () {
      if (!this.hasDirty) return this.load();
      this.$q.dialog({ title: 'Незбережені зміни', message: 'Ціни змінено, але не збережено. Оновлення скасує ваші правки. Оновити?', cancel: 'Лишитися', ok: 'Оновити й скасувати правки', persistent: true })
        .onOk(() => this.load());
    },
    // ---------- платники ----------
    openPayer (row) {
      this.formError = '';
      const d = v => (v ? String(v).slice(0, 10) : null);
      this.payerForm = row
        ? { ...row, contractDate: d(row.contractDate), contractValidTo: d(row.contractValidTo) }
        : { id: null, code: '', name: '', kind: 'INSURANCE', priceListId: null, coveragePct: 100, franchiseAmount: 0, requiresPolicy: false, requiresAuthorization: false, isActive: true };
      this.payerOpen = true;
    },
    async savePayer () {
      const f = this.payerForm;
      if (!f.code || !f.name) { this.formError = 'Вкажіть код і назву платника'; return; }
      this.saving = true; this.formError = '';
      try {
        const body = { ...f }; const id = body.id; delete body.id; delete body.kindName; delete body.priceListName;
        await this.$api.savePayer(id, body);
        this.$q.notify({ type: 'positive', message: id ? 'Платника оновлено' : 'Платника створено' });
        this.payerOpen = false; this.load();
      } catch (e) { this.formError = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    },
    async restorePayer (row) {
      const res = await this.callApi(() => this.$api.savePayer(row.id, { ...row, isActive: true }), { success: 'Платника повернуто з архіву' });
      if (res !== undefined) this.load();
    },
    // ---------- прайс-листи ----------
    openList (l) {
      this.formError = '';
      const d = v => (v ? String(v).slice(0, 10) : null);
      this.listForm = l ? { id: l.id, code: l.code, name: l.name, validFrom: d(l.validFrom), validTo: d(l.validTo), isDefault: l.isDefault, isActive: l.isActive !== false }
        : { id: null, code: '', name: '', validFrom: null, validTo: null, isDefault: false, isActive: true };
      this.listOpen = true;
    },
    async saveListForm () {
      const f = this.listForm;
      if (!f.code || !f.name) { this.formError = 'Вкажіть код і назву прайс-листа'; return; }
      this.saving = true; this.formError = '';
      try {
        const id = f.id; const body = { code: f.code, name: f.name, validFrom: f.validFrom, validTo: f.validTo, isDefault: f.isDefault, isActive: f.isActive };
        await this.$api.savePriceList(id, body);
        this.$q.notify({ type: 'positive', message: id ? 'Прайс-лист оновлено' : 'Прайс-лист створено' });
        this.listOpen = false; this.load();
      } catch (e) { this.formError = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    },
    async restoreList (l) {
      const res = await this.callApi(() => this.$api.savePriceList(l.id, { code: l.code, name: l.name, validFrom: l.validFrom, validTo: l.validTo, isDefault: l.isDefault, isActive: true }), { success: 'Прайс-лист повернуто з архіву' });
      if (res !== undefined) this.load();
    },
    askRemove (kind, row) {
      const what = kind === 'payer' ? 'платника' : 'прайс-лист';
      this.$q.dialog({ title: `Прибрати ${what}?`, message: `«${row.name}»: якщо запис уже використовується (замовлення, рахунки, платники), він піде в архів; якщо ні — буде видалений остаточно.`, cancel: 'Скасувати', ok: 'Підтвердити' })
        .onOk(async () => {
          try {
            const res = kind === 'payer' ? await this.$api.deletePayer(row.id) : await this.$api.deletePriceList(row.id);
            this.$q.notify({ type: 'positive', message: res.message || 'Видалено' }); this.load();
          } catch (e) { this.notifyError(e); }
        });
    },
    // ---------- пакети ----------
    openPackage (l, p) {
      this.formError = ''; this.pkgList = l; this.memberFilter = '';
      this.pkgForm = p ? { id: p.id, code: p.code, name: p.name, price: p.price, members: (p.members || []).map(m => { const pr = this.profiles.find(x => x.id === m); const t = pr || this.tests.find(x => x.id === m); return t ? t.code : m; }), isActive: p.isActive !== false }
        : { id: null, code: '', name: '', price: 0, members: [], isActive: true };
      this.pkgOpen = true;
    },
    async savePackage () {
      const f = this.pkgForm;
      if (!f.code || !f.name) { this.formError = 'Вкажіть код і назву пакета'; return; }
      if ((f.members || []).length < 2) { this.formError = 'У пакеті має бути щонайменше дві послуги'; return; }
      if (!(Number(f.price) >= 0)) { this.formError = 'Вкажіть пакетну ціну від 0 грн'; return; }
      this.saving = true; this.formError = '';
      try {
        await this.$api.savePricePackage(this.pkgList.id, f.id, { code: f.code, name: f.name, price: Number(f.price), members: f.members, isActive: f.isActive });
        this.$q.notify({ type: 'positive', message: f.id ? 'Пакет оновлено' : 'Пакет створено' });
        this.pkgOpen = false; this.load();
      } catch (e) { this.formError = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    },
    removePackage (l, p) {
      this.$q.dialog({ title: 'Прибрати пакет?', message: `Пакет «${p.name}»: якщо його вже застосовано в замовленнях, він піде в архів; інакше буде видалений.`, cancel: 'Скасувати', ok: 'Підтвердити' })
        .onOk(async () => {
          try { const res = await this.$api.deletePricePackage(l.id, p.id); this.$q.notify({ type: 'positive', message: res.message || 'Пакет видалено' }); this.load(); } catch (e) { this.notifyError(e); }
        });
    },
    // ---------- масове оновлення цін ----------
    openImport (l) { this.importList = l; this.importFile = null; this.importPreview = null; this.importOpen = true; },
    async runPriceImport (dryRun) {
      this.importing = true;
      try {
        const text = await new Promise((resolve, reject) => { const r = new FileReader(); r.onload = () => resolve(String(r.result || '')); r.onerror = () => reject(new Error('Не вдалося прочитати файл')); r.readAsText(this.importFile, 'utf-8'); });
        const res = await this.$api.importPriceList(this.importList.id, text, dryRun);
        if (dryRun) this.importPreview = res;
        else { this.$q.notify({ type: 'positive', message: `Ціни оновлено: нових ${res.created}, змінених ${res.updated}` }); this.importOpen = false; this.importPreview = null; this.load(); }
      } catch (e) { this.notifyError(e); } finally { this.importing = false; }
    },
    async load () {
      await this.callApi(async () => { const [p, l] = await Promise.all([this.$api.payers(), this.$api.priceLists()]); this.payers = p; this.lists = l; this.dirty = {}; });
    },
    saveList (l) {
      const err = this.listError(l);
      if (err) { this.$q.notify({ type: 'negative', message: err }); return; }
      const changed = l.items.length;
      this.$q.dialog({ title: 'Зберегти ціни?', message: `Прайс-лист «${l.name}»: буде збережено ціни для ${changed} позицій.`, cancel: 'Скасувати', ok: 'Зберегти' })
        .onOk(async () => {
          const items = {};
          l.items.forEach(i => { items[i.profileId || i.testId] = Number(i.price); });
          const res = await this.callApi(() => this.$api.savePriceList(l.id, { code: l.code, name: l.name, validFrom: l.validFrom, validTo: l.validTo, isDefault: l.isDefault, isActive: l.isActive, items }), { success: 'Прайс збережено' });
          if (res !== undefined) this.load(); // при помилці правки лишаються на екрані
        });
    }
  }
};
</script>
