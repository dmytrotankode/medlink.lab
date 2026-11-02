<!--
  ТЗ 21.6, 21.8, 23.3: блок «Призначення» картки замовлення з направлення МІС — позиції (інтервенції ЕСОЗ), пакет,
  обрані / не обрані лікарем / автододані показники, хто обрав; стан розподілу по аналізаторах.
-->
<template>
  <div class="medlink-card q-mb-md" data-testid="orderItemsPanel">
    <div class="medlink-card__title">
      <span><q-icon name="assignment" class="q-mr-xs" />Призначення МІС<span v-if="order.misReferralNumber"> · {{ order.misReferralNumber }}</span></span>
      <span>
        <q-badge v-if="order.packageCode" color="purple-5" class="q-mr-xs" :label="`Пакет: ${order.packageName || order.packageCode}${order.packageChangedByDoctor ? ' (змінено лікарем)' : ''}`" />
        <q-badge :color="distColor" :label="distLabel" />
      </span>
    </div>
    <div class="q-pa-sm">
      <div class="text-caption text-grey-7 q-mb-xs">
        {{ order.referralContext === 'INPATIENT' ? 'Стаціонар' : 'Амбулаторія' }}<span v-if="order.requestingDepartmentName"> · {{ order.requestingDepartmentName }}</span>
        <span v-if="bed"> · {{ bed }}</span><span v-if="order.plannedCollectionAt"> · планове взяття {{ order.plannedCollectionAt | datetime }}</span>
      </div>
      <q-markup-table dense flat bordered>
        <thead><tr><th class="text-left">№</th><th class="text-left">Послуга ЕСОЗ</th><th class="text-left">Обрані показники</th><th class="text-left">Не обрані</th><th class="text-left">Хто обрав</th><th class="text-left">Стан</th></tr></thead>
        <tbody>
          <tr v-for="(i, n) in order.items" :key="i.id">
            <td>{{ i.packageItemNo || n + 1 }}</td>
            <td><b>{{ i.interventionName }}</b><div class="text-caption text-grey-7">{{ i.interventionCode }}</div></td>
            <td>{{ (i.selected || []).join(', ') || '—' }}<div v-if="i.autoAdded && i.autoAdded.length" class="text-caption text-orange-9">автододано: {{ i.autoAdded.join(', ') }}</div></td>
            <td class="text-grey-6">{{ (i.notOrdered || []).join(', ') }}</td>
            <td class="text-caption">{{ sourceLabel(i.selectionSource) }}</td>
            <td><q-badge :color="itemColor(i.status)" :label="itemLabel(i.status)" /></td>
          </tr>
        </tbody>
      </q-markup-table>
      <div v-if="order.distributionStatus && order.distributionStatus !== 'CONFIRMED' && order.distributionStatus !== 'NONE'" class="row justify-end q-mt-sm">
        <q-btn flat dense color="primary" icon="call_split" label="Розподіл по аналізаторах" :to="{ name: 'lab-incoming-referrals', query: { order: order.id } }" />
      </div>
    </div>
  </div>
</template>

<script>
const ITEM = { ACTIVE: ['В роботі', 'primary'], DONE: ['Виконано', 'positive'], CANCELLED: ['Скасовано', 'grey-6'], UNMAPPED: ['Не співставлено', 'orange-8'], NEEDS_SELECTION: ['Потрібен вибір', 'negative'] };
const DIST = { PROPOSED: ['Розподіл: пропозиція', 'orange-7'], CONFIRMED: ['Розподіл підтверджено', 'positive'], NEEDS_SELECTION: ['Потрібен вибір показників', 'negative'], NONE: ['Розподіл автоматичний', 'grey-6'] };

export default {
  name: 'OrderItemsPanel',
  props: { order: { type: Object, required: true } },
  computed: {
    bed () { return [this.order.ward && `пал. ${this.order.ward}`, this.order.room && `к. ${this.order.room}`, this.order.bed && `ліжко ${this.order.bed}`].filter(Boolean).join(', '); },
    distLabel () { return (DIST[this.order.distributionStatus] || [this.order.distributionStatus])[0]; },
    distColor () { return (DIST[this.order.distributionStatus] || [null, 'grey-6'])[1]; }
  },
  methods: {
    itemLabel (s) { return (ITEM[s] || [s])[0]; },
    itemColor (s) { return (ITEM[s] || [s, 'grey-6'])[1]; },
    sourceLabel (s) { return { FIXED: 'фіксований склад', REFERRING_DOCTOR: 'лікар, що направив', REGISTRAR: 'реєстратор', LAB_DOCTOR: 'лікар-лаборант', PACKAGE: 'пакет' }[s] || s || '—'; }
  }
};
</script>
