<template>
  <div data-testid="pricingPage">
    <page-header title="Прайси та платники" icon="fas fa-file-invoice-dollar" subtitle="Каса, страхові програми (покриття, франшиза, поліс, гарантійний лист), НСЗУ, клініки-партнери, внутрішнє; прайс-листи та пакети послуг" :breadcrumbs="[{ label: 'Прайси та платники' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />
    <div class="medlink-card">
      <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8">
        <q-tab name="payers" icon="account_balance" :label="`Платники (${payers.length})`" />
        <q-tab name="lists" icon="list_alt" :label="`Прайс-листи (${lists.length})`" />
      </q-tabs>
      <q-separator />
      <div v-show="tab === 'payers'" class="q-pa-sm">
        <q-table :data="payers" :columns="payerColumns" row-key="id" dense flat :pagination="{ rowsPerPage: 50 }">
          <template v-slot:body-cell-kind="props"><q-td :props="props"><q-badge :color="kindColor(props.row.kind)" :label="props.row.kindName" /></q-td></template>
          <template v-slot:body-cell-terms="props">
            <q-td :props="props" class="text-caption">
              покриття {{ props.row.coveragePct }}%<span v-if="props.row.franchiseAmount"> · франшиза {{ props.row.franchiseAmount | money }}</span>
              <span v-if="props.row.requiresPolicy"> · поліс</span><span v-if="props.row.requiresAuthorization"> · гарантійний лист</span>
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
                <q-item-label><b>{{ l.name }}</b> <span class="text-grey-7">({{ l.code }})</span> <q-badge v-if="l.isDefault" color="positive" label="роздрібний" /></q-item-label>
                <q-item-label caption>{{ l.validFrom | date }} — {{ l.validTo ? $options.filters.date(l.validTo) : 'безстроково' }} · позицій {{ l.itemsCount }} · пакетів {{ l.packages.length }}</q-item-label>
              </q-item-section>
            </template>
            <div class="q-pa-sm bg-grey-1">
              <div v-if="l.isDefault && !l.items.length" class="text-caption text-grey-7 q-mb-sm">Роздрібні ціни беруться з прайсу послуг MedLink (org_organization_service) та довідника показників; тут можна задати власні.</div>
              <q-markup-table v-if="l.items.length" dense flat bordered class="q-mb-sm">
                <thead><tr><th class="text-left">Послуга / показник</th><th class="text-right" style="width:140px">Ціна, грн</th></tr></thead>
                <tbody>
                  <tr v-for="i in l.items" :key="i.id">
                    <td>{{ itemName(i) }}</td>
                    <td class="text-right"><q-input v-model.number="i.price" dense borderless input-class="text-right" type="number" @change="dirty[l.id] = true" /></td>
                  </tr>
                </tbody>
              </q-markup-table>
              <div v-for="p in l.packages" :key="p.id" class="text-caption"><q-icon name="inventory_2" color="primary" /> Пакет <b>{{ p.name }}</b> ({{ p.code }}) — {{ p.price | money }}: {{ p.members.map(m => itemName({ profileId: m, testId: m })).join(' + ') }}</div>
              <q-btn v-if="dirty[l.id]" dense unelevated color="primary" icon="save" label="Зберегти ціни" class="q-mt-sm" @click="saveList(l)" />
            </div>
          </q-expansion-item>
        </q-list>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';

export default {
  name: 'PricingPage',
  mixins: [apiMixin],
  data () {
    return {
      tab: 'payers', payers: [], lists: [], dirty: {},
      payerColumns: [
        { name: 'code', label: 'Код', field: 'code', align: 'left' },
        { name: 'name', label: 'Платник / програма', field: 'name', align: 'left' },
        { name: 'kind', label: 'Тип', field: 'kindName', align: 'left' },
        { name: 'org', label: 'Юр. особа / ЄДРПОУ', field: r => [r.organizationName, r.edrpou].filter(Boolean).join(' · ') || '—', align: 'left' },
        { name: 'contract', label: 'Договір', field: r => r.contractNumber ? `${r.contractNumber}${r.contractValidTo ? ' до ' + r.contractValidTo.substring(0, 10) : ''}` : '—', align: 'left' },
        { name: 'list', label: 'Прайс-лист', field: r => r.priceListName || (r.kind === 'INTERNAL' ? 'усі послуги' : '—'), align: 'left' },
        { name: 'terms', label: 'Умови', field: 'coveragePct', align: 'left' }
      ]
    };
  },
  computed: {
    profiles () { return this.$store.getters['dictionaries/items']('profiles'); },
    tests () { return this.$store.getters['dictionaries/items']('tests'); }
  },
  created () { this.$store.dispatch('dictionaries/loadMany', ['profiles', 'tests']); this.load(); },
  methods: {
    kindColor (k) { return { PATIENT: 'grey-7', INSURANCE: 'purple-5', NSZU: 'teal-6', CLINIC: 'indigo-5', ENTERPRISE: 'brown-5', INTERNAL: 'blue-grey-5' }[k] || 'grey'; },
    itemName (i) {
      const p = this.profiles.find(x => x.id === i.profileId);
      if (p) return `${p.code} — ${p.name}`;
      const t = this.tests.find(x => x.id === i.testId);
      return t ? `${t.code} — ${t.name}` : (i.profileId || i.testId);
    },
    async load () {
      await this.callApi(async () => { const [p, l] = await Promise.all([this.$api.payers(), this.$api.priceLists()]); this.payers = p; this.lists = l; this.dirty = {}; });
    },
    async saveList (l) {
      const items = {};
      l.items.forEach(i => { items[i.profileId || i.testId] = Number(i.price) || 0; });
      await this.callApi(() => this.$api.savePriceList(l.id, { code: l.code, name: l.name, validFrom: l.validFrom, validTo: l.validTo, isDefault: l.isDefault, isActive: l.isActive, items }), { success: 'Прайс збережено' });
      this.load();
    }
  }
};
</script>
