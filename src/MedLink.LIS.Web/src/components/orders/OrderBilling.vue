<template>
  <div class="medlink-card q-mb-md" data-testid="orderBilling">
    <div class="medlink-card__title">
      <span><q-icon name="payments" class="q-mr-xs" />Оплата</span>
      <span v-if="b" class="text-caption">
        <q-badge :color="b.payerKind === 'PATIENT' ? 'grey-7' : b.payerKind === 'NSZU' ? 'teal-6' : 'purple-5'" :label="b.payerName" />
        <span v-if="b.insurancePolicyNumber" class="q-ml-xs">поліс {{ b.insurancePolicyNumber }}</span>
        <span v-if="b.authorizationNumber" class="q-ml-xs">ГЛ {{ b.authorizationNumber }}</span>
      </span>
    </div>
    <div v-if="b" class="q-pa-sm">
      <q-markup-table dense flat bordered>
        <thead><tr><th class="text-left">Послуга</th><th class="text-right">Вартість</th><th class="text-right">Платник</th><th class="text-right">Пацієнт</th><th></th></tr></thead>
        <tbody>
          <tr v-for="c in b.charges" :key="c.id">
            <td><b>{{ c.code }}</b> {{ c.name }}
              <q-badge v-if="c.isPackage" color="primary" label="пакет" class="q-ml-xs" />
              <q-badge v-if="c.isNotCovered" color="deep-orange-5" label="не покривається" class="q-ml-xs" />
              <q-badge v-if="c.isPatientChoice" color="grey-6" label="сплачує сам" class="q-ml-xs" />
            </td>
            <td class="text-right">{{ c.amount | money }}</td>
            <td class="text-right">{{ c.payerAmount | money }}</td>
            <td class="text-right">{{ c.patientAmount | money }}</td>
            <td class="text-right">
              <q-btn v-if="b.payerKind !== 'PATIENT' && !c.isPackage && !c.isNotCovered" flat dense round size="sm" :icon="c.isPatientChoice ? 'undo' : 'person'" @click="toggleChoice(c)">
                <q-tooltip>{{ c.isPatientChoice ? 'Повернути на платника' : 'Пацієнт сплачує сам' }}</q-tooltip>
              </q-btn>
            </td>
          </tr>
          <tr class="text-weight-bold"><td>Разом</td><td class="text-right">{{ b.total | money }}</td><td class="text-right">{{ b.payerAmount | money }}</td><td class="text-right">{{ b.patientAmount | money }}</td><td></td></tr>
        </tbody>
      </q-markup-table>
      <div class="row items-center q-mt-sm q-gutter-sm">
        <div class="text-subtitle2">Сплачено {{ b.paidAmount | money }} · <span :class="b.due > 0 ? 'text-negative' : 'text-positive'">до сплати {{ b.due | money }}</span></div>
        <q-space />
        <q-btn v-if="b.due > 0" dense unelevated color="positive" icon="point_of_sale" label="Прийняти оплату" data-testid="payBtn" @click="pay" />
        <q-btn dense outline color="primary" icon="receipt_long" label="Квитанція" :href="$api.receiptUrl(orderId)" target="_blank" />
        <q-btn v-if="b.payerKind !== 'PATIENT' && !b.invoices.length" dense outline color="purple-6" icon="request_quote" label="Рахунок платнику" @click="invoice" />
        <q-btn v-for="i in b.invoices" :key="i.id" dense flat color="purple-6" icon="print" :label="i.number" :href="$api.invoicePrintUrl(i.id)" target="_blank" />
        <q-btn dense flat color="grey-8" icon="swap_horiz" label="Змінити платника" @click="changePayer" />
      </div>
      <div v-if="b.payments.length" class="text-caption text-grey-7 q-mt-xs">Оплати: <span v-for="p in b.payments" :key="p.id" class="q-mr-sm">{{ p.receiptNumber }} — {{ p.amount | money }} ({{ p.method }})</span></div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'OrderBilling',
  props: { orderId: { type: String, required: true } },
  data () { return { b: null, payers: [] }; },
  watch: { orderId: { immediate: true, handler () { this.load(); } } },
  methods: {
    async load () { try { this.b = await this.$api.orderBilling(this.orderId); } catch (e) { this.b = null; } },
    async run (fn, msg) {
      try { this.b = await fn(); if (msg) this.$q.notify({ type: 'positive', message: msg }); this.$emit('changed'); } catch (e) { this.$q.notify({ type: 'negative', message: e.userMessage || 'Помилка' }); }
    },
    pay () {
      this.$q.dialog({ title: 'Спосіб оплати', options: { type: 'radio', model: 'CASH', items: [{ label: 'Готівка', value: 'CASH' }, { label: 'Картка', value: 'CARD' }, { label: 'Переказ', value: 'TRANSFER' }] }, cancel: true })
        .onOk(method => {
          this.$q.dialog({ title: 'Сума оплати, грн', message: `До сплати: ${this.b.due}`, prompt: { model: String(this.b.due), type: 'text' }, cancel: true })
            .onOk(v => this.run(() => this.$api.addPayment(this.orderId, { amount: Number(String(v).replace(',', '.')), method }), 'Оплату прийнято'));
        });
    },
    toggleChoice (c) { this.run(() => this.$api.setChargePatientChoice(this.orderId, c.id, !c.isPatientChoice)); },
    async invoice () {
      try { const inv = await this.$api.issueInvoice(this.orderId); this.$q.notify({ type: 'positive', message: `Рахунок ${inv.number} на ${inv.amount} грн` }); this.load(); } catch (e) { this.$q.notify({ type: 'negative', message: e.userMessage || 'Помилка' }); }
    },
    async changePayer () {
      if (!this.payers.length) this.payers = await this.$api.payers();
      this.$q.dialog({
        title: 'Платник замовлення', options: { type: 'radio', model: this.b.payerId, items: this.payers.filter(p => p.isActive).map(p => ({ label: `${p.name} · ${p.kindName}`, value: p.id })) }, cancel: true
      }).onOk(id => {
        const p = this.payers.find(x => x.id === id);
        const ask = p && p.requiresAuthorization
          ? new Promise(resolve => this.$q.dialog({ title: 'Гарантійний лист', prompt: { model: '', type: 'text' }, cancel: true }).onOk(resolve))
          : Promise.resolve(null);
        ask.then(auth => this.run(() => this.$api.setOrderPayer(this.orderId, { payerId: id, authorizationNumber: auth }), 'Платника змінено'));
      });
    }
  }
};
</script>
