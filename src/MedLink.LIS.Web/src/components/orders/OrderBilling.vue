<template>
  <div class="medlink-card q-mb-md" data-testid="orderBilling">
    <div class="medlink-card__title">
      <span><q-icon name="payments" class="q-mr-xs" />Оплата</span>
      <span v-if="b" class="text-caption">
        <q-badge :color="b.payerKind === 'PATIENT' ? 'grey-7' : b.payerKind === 'NSZU' ? 'teal-6' : 'purple-5'" :label="b.payerKind === 'PATIENT' ? 'Оплата: пацієнт' : b.payerName" />
        <span v-if="b.insurancePolicyNumber" class="q-ml-xs">поліс {{ b.insurancePolicyNumber }}</span>
        <span v-if="b.authorizationNumber" class="q-ml-xs">гар. лист {{ b.authorizationNumber }}<q-tooltip>Гарантійний лист страхової компанії</q-tooltip></span>
      </span>
    </div>
    <div v-if="b" class="q-pa-sm">
      <q-markup-table dense flat bordered>
        <thead><tr><th class="text-left">Послуга</th><th class="text-right">Вартість</th><th class="text-right">Страховик / клієнт</th><th class="text-right">Пацієнт</th><th></th></tr></thead>
        <tbody>
          <tr v-for="c in b.charges" :key="c.id">
            <td>{{ c.name }}
              <q-badge v-if="c.isPackage" color="primary" label="пакет" class="q-ml-xs" />
              <q-badge v-if="c.isNotCovered" color="deep-orange-5" label="не покривається" class="q-ml-xs" />
              <q-badge v-if="c.isPatientChoice" color="grey-6" label="сплачує сам" class="q-ml-xs" />
            </td>
            <td class="text-right">{{ c.amount | money }}</td>
            <td class="text-right">{{ c.payerAmount | money }}</td>
            <td class="text-right">{{ c.patientAmount | money }}</td>
            <td class="text-right">
              <q-btn v-if="canChangePayer && b.payerKind !== 'PATIENT' && !c.isPackage && !c.isNotCovered" flat dense round size="sm" :icon="c.isPatientChoice ? 'undo' : 'person'" @click="toggleChoice(c)">
                <q-tooltip>{{ c.isPatientChoice ? 'Повернути на платника' : 'Пацієнт сплачує сам' }}</q-tooltip>
              </q-btn>
            </td>
          </tr>
          <tr class="text-weight-bold"><td>Разом</td><td class="text-right">{{ b.total | money }}</td><td class="text-right">{{ b.payerAmount | money }}</td><td class="text-right">{{ b.patientAmount | money }}</td><td></td></tr>
        </tbody>
      </q-markup-table>
      <div class="row items-center q-mt-sm q-gutter-sm">
        <div class="text-subtitle2">Сплачено {{ b.paidAmount | money }} · <span v-if="b.refundDue > 0" class="text-deep-orange-8">до повернення {{ b.refundDue | money }}</span><span v-else :class="b.due > 0 ? 'text-negative' : 'text-positive'">до сплати {{ b.due | money }}</span></div>
        <q-space />
        <q-btn v-if="canTakePayment && b.due > 0" dense unelevated color="positive" icon="point_of_sale" label="Прийняти оплату" data-testid="payBtn" @click="openPay" />
        <q-btn v-if="canTakePayment && b.refundDue > 0" dense unelevated color="deep-orange-6" icon="undo" label="Повернення коштів" @click="openRefund" />
        <q-btn dense outline color="primary" icon="receipt_long" label="Квитанція" :href="$api.receiptUrl(orderId)" target="_blank">
          <q-tooltip>Відкриється в новій вкладці</q-tooltip>
        </q-btn>
        <q-btn v-if="canInvoice && b.payerKind !== 'PATIENT' && !b.invoices.length && b.payerAmount > 0" dense outline color="purple-6" icon="request_quote" label="Рахунок платнику" @click="invoice">
          <q-tooltip>Виставити рахунок платнику на {{ b.payerAmount | money }}</q-tooltip>
        </q-btn>
        <q-btn v-for="i in b.invoices" :key="i.id" dense flat color="purple-6" icon="print" :label="i.number" :href="$api.invoicePrintUrl(i.id)" target="_blank">
          <q-tooltip>Рахунок відкриється в новій вкладці</q-tooltip>
        </q-btn>
        <q-btn v-if="canChangePayer" dense flat color="grey-8" icon="swap_horiz" label="Змінити платника" @click="openPayer" />
      </div>
      <div v-if="!canTakePayment" class="text-caption text-grey-7 q-mt-xs">Оплату приймає реєстратор або медсестра пункту забору.</div>
      <div v-if="b.payments.length" class="text-caption text-grey-7 q-mt-xs">Оплати: <span v-for="p in b.payments" :key="p.id" class="q-mr-sm">{{ p.receiptNumber }} — {{ p.amount | money }} ({{ methodLabel(p.method) }})</span></div>
    </div>

    <!-- Один діалог оплати: спосіб + сума (числове поле, не більше суми до сплати) -->
    <q-dialog v-model="payDlg" persistent>
      <q-card style="width: 380px" data-testid="payDialog">
        <q-card-section class="text-subtitle1 text-weight-bold">Прийняти оплату</q-card-section>
        <q-card-section class="q-pt-none">
          <q-option-group v-model="payMethod" type="radio" :options="methodOptions" />
          <q-input
            v-model="payAmount"
            class="q-mt-md"
            dense
            outlined
            autofocus
            inputmode="decimal"
            label="Сума оплати"
            suffix="грн"
            :hint="b ? `До сплати: ${$options.filters.money(b.due)}` : ''"
            :error="!!payError"
            :error-message="payError"
            data-testid="payAmount"
            @keyup.enter="confirmPay"
          />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="positive" label="Прийняти" :disable="!!payError" data-testid="payConfirm" @click="confirmPay" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="refundDlg" persistent>
      <q-card style="width: 380px">
        <q-card-section class="text-subtitle1 text-weight-bold">Повернення коштів</q-card-section>
        <q-card-section class="q-pt-none">
          <q-input
            v-model="refundAmount"
            dense
            outlined
            autofocus
            inputmode="decimal"
            label="Сума повернення"
            suffix="грн"
            :hint="b ? `До повернення: ${$options.filters.money(b.refundDue)}` : ''"
            :error="!!refundError"
            :error-message="refundError"
            @keyup.enter="confirmRefund"
          />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="deep-orange-6" label="Повернути" :disable="!!refundError" @click="confirmRefund" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Платник: вибір і (за потреби) № гарантійного листа в одному вікні -->
    <q-dialog v-model="payerDlg" persistent>
      <q-card style="width: 460px">
        <q-card-section class="text-subtitle1 text-weight-bold">Платник направлення</q-card-section>
        <q-card-section class="q-pt-none">
          <q-option-group v-model="payerId" type="radio" :options="payerOptions" />
          <q-input
            v-if="selectedPayer && selectedPayer.requiresAuthorization"
            v-model="authNumber"
            class="q-mt-md"
            dense
            outlined
            label="№ гарантійного листа *"
            :error="authTouched && !authNumber.trim()"
            error-message="Вкажіть номер гарантійного листа"
            data-testid="authNumber"
            @input="authTouched = true"
          />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Змінити платника" :disable="!payerId" @click="confirmPayer" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import roleMixin from '../../mixins/roleMixin';
import { ACTION_ROLES } from '../../utils/menuAccess';

const METHOD_LABELS = { CASH: 'Готівка', CARD: 'Картка', TRANSFER: 'Переказ', REFUND: 'Повернення' };

/** Розбір суми: «550», «550,5», «550.50»; інакше null */
function parseMoney (v) {
  const t = String(v === null || v === undefined ? '' : v).trim().replace(/\s/g, '').replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null;
  return Number(t);
}

export default {
  name: 'OrderBilling',
  mixins: [roleMixin],
  props: { orderId: { type: String, required: true } },
  data () {
    return {
      b: null, payers: [],
      payDlg: false, payMethod: 'CASH', payAmount: '',
      refundDlg: false, refundAmount: '',
      payerDlg: false, payerId: null, authNumber: '', authTouched: false,
      methodOptions: [{ label: 'Готівка', value: 'CASH' }, { label: 'Картка', value: 'CARD' }, { label: 'Переказ', value: 'TRANSFER' }]
    };
  },
  computed: {
    canTakePayment () { return this.canDo(...ACTION_ROLES.takePayment); },
    canChangePayer () { return this.canDo(...ACTION_ROLES.changePayer); },
    canInvoice () { return this.canDo(...ACTION_ROLES.issueInvoice); },
    payError () {
      const n = parseMoney(this.payAmount);
      if (n === null) return 'Введіть суму цифрами, наприклад 550 або 550,50';
      if (n <= 0) return 'Сума має бути більшою за 0';
      if (this.b && n > Number(this.b.due) + 0.0001) return `Сума більша за борг (${this.$options.filters.money(this.b.due)})`;
      return '';
    },
    refundError () {
      const n = parseMoney(this.refundAmount);
      if (n === null) return 'Введіть суму цифрами, наприклад 120 або 120,50';
      if (n <= 0) return 'Сума має бути більшою за 0';
      if (this.b && n > Number(this.b.refundDue) + 0.0001) return `Сума більша за суму до повернення (${this.$options.filters.money(this.b.refundDue)})`;
      return '';
    },
    payerOptions () { return this.payers.filter(p => p.isActive).map(p => ({ label: `${p.name} · ${p.kindName}`, value: p.id })); },
    selectedPayer () { return this.payers.find(x => x.id === this.payerId) || null; }
  },
  watch: { orderId: { immediate: true, handler () { this.load(); } } },
  methods: {
    methodLabel (m) { return METHOD_LABELS[m] || m; },
    async load () { try { this.b = await this.$api.orderBilling(this.orderId); } catch (e) { this.b = null; } },
    async run (fn, msg, failMsg) {
      try { this.b = await fn(); if (msg) this.$q.notify({ type: 'positive', message: msg }); this.$emit('changed'); return true; } catch (e) {
        // англійські технічні тексти сервера не показуємо
        const text = e && e.userMessage && /[А-Яа-яІіЇїЄєҐґ]/.test(e.userMessage) ? e.userMessage : (failMsg || 'Не вдалося виконати дію. Перевірте введені дані.');
        this.$q.notify({ type: 'negative', message: text, timeout: 5000 });
        return false;
      }
    },
    openPay () {
      this.payMethod = 'CASH';
      this.payAmount = String(this.b.due).replace('.', ',');
      this.payDlg = true;
    },
    async confirmPay () {
      if (this.payError) return;
      const amount = parseMoney(this.payAmount);
      if (await this.run(() => this.$api.addPayment(this.orderId, { amount, method: this.payMethod }), 'Оплату прийнято', 'Не вдалося прийняти оплату. Перевірте суму.')) this.payDlg = false;
    },
    openRefund () {
      this.refundAmount = String(this.b.refundDue).replace('.', ',');
      this.refundDlg = true;
    },
    async confirmRefund () {
      if (this.refundError) return;
      const amount = parseMoney(this.refundAmount);
      if (await this.run(() => this.$api.addPayment(this.orderId, { amount, method: 'REFUND' }), 'Повернення проведено', 'Не вдалося провести повернення. Перевірте суму.')) this.refundDlg = false;
    },
    toggleChoice (c) { this.run(() => this.$api.setChargePatientChoice(this.orderId, c.id, !c.isPatientChoice)); },
    async invoice () {
      try { const inv = await this.$api.issueInvoice(this.orderId); this.$q.notify({ type: 'positive', message: `Рахунок ${inv.number} на ${inv.amount} грн` }); this.load(); } catch (e) { this.$q.notify({ type: 'negative', message: e.userMessage || 'Не вдалося виставити рахунок' }); }
    },
    async openPayer () {
      try { if (!this.payers.length) this.payers = await this.$api.payers(); } catch (e) { this.$q.notify({ type: 'negative', message: e.userMessage || 'Не вдалося завантажити платників' }); return; }
      this.payerId = this.b.payerId;
      this.authNumber = '';
      this.authTouched = false;
      this.payerDlg = true;
    },
    async confirmPayer () {
      const p = this.selectedPayer;
      if (p && p.requiresAuthorization && !this.authNumber.trim()) { this.authTouched = true; return; }
      const auth = p && p.requiresAuthorization ? this.authNumber.trim() : null;
      if (await this.run(() => this.$api.setOrderPayer(this.orderId, { payerId: this.payerId, authorizationNumber: auth }), 'Платника змінено')) this.payerDlg = false;
    }
  }
};
</script>
