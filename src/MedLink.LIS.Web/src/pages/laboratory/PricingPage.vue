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
        <div class="text-caption text-grey-7 q-mb-xs">Створення й зміна платників та прайс-листів на цьому екрані поки недоступні — {{ canEditPrices ? 'зверніться до служби підтримки MedLink.' : 'зверніться до адміністратора лабораторії.' }}</div>
        <q-table :data="payers" :columns="payerColumns" row-key="id" dense flat :pagination="{ rowsPerPage: 50 }">
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
        <q-list separator>
          <q-expansion-item v-for="l in lists" :key="l.id" group="pl">
            <template v-slot:header>
              <q-item-section avatar><q-icon :name="l.isDefault ? 'storefront' : 'description'" :color="l.isDefault ? 'positive' : 'primary'" /></q-item-section>
              <q-item-section>
                <q-item-label><b>{{ l.name }}</b> <span class="text-grey-6">· {{ l.code }}</span> <q-badge v-if="l.isDefault" color="positive" label="роздрібний" class="q-ml-xs" /> <q-badge v-if="dirty[l.id]" color="orange-8" label="не збережено" class="q-ml-xs" /></q-item-label>
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
                <q-icon name="inventory_2" color="primary" /> Пакет <b>{{ p.name }}</b> · {{ p.code }} — {{ p.price | money }}
                <ul class="q-my-none"><li v-for="m in p.members" :key="m">{{ itemName({ profileId: m, testId: m }) }}</li></ul>
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
      tab: 'payers', payers: [], lists: [], dirty: {},
      payerColumns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left' },
        { name: 'name', label: 'Платник / програма', field: 'name', align: 'left' },
        { name: 'kind', label: 'Тип', field: 'kindName', align: 'left' },
        { name: 'org', label: 'Юр. особа / ЄДРПОУ', field: r => [r.organizationName, r.edrpou].filter(Boolean).join(' · ') || '—', align: 'left' },
        { name: 'contract', label: 'Договір', field: r => r.contractNumber ? `${r.contractNumber}${r.contractValidTo ? ' до ' + formatDate(r.contractValidTo) : ''}` : '—', align: 'left' },
        { name: 'list', label: 'Прайс-лист', field: r => r.priceListName || (r.kind === 'INTERNAL' ? 'усі послуги' : '—'), align: 'left' },
        { name: 'terms', label: 'Умови', field: 'coveragePct', align: 'left' }
      ]
    };
  },
  computed: {
    profiles () { return this.$store.getters['dictionaries/items']('profiles'); },
    tests () { return this.$store.getters['dictionaries/items']('tests'); },
    canEditPrices () { return this.canDo(...ACTION_ROLES.priceEdit); },
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
