<template>
  <div class="order-card" data-testid="orderCard">
    <page-header
      :title="order ? `Замовлення ${order.orderNumber}` : 'Картка замовлення'"
      icon="fas fa-file-medical-alt"
      :subtitle="order ? `створено ${formatDateTime(order.orderDatetime)}` : ''"
      :breadcrumbs="[{ label: 'Замовлення', to: { name: 'lab-orders' } }, { label: order ? order.orderNumber : '…' }]"
    >
      <template v-slot:title-after>
        <status-chip v-if="order" :value="order.status" type="order" icon data-testid="orderStatus" />
        <q-badge v-if="order && order.isUrgentCito" color="deep-orange-6" label="CITO" />
      </template>
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load"><q-tooltip>Оновити</q-tooltip></q-btn>
      <q-btn outline dense color="primary" icon="label" label="Етикетки" :disable="!order" @click="openLabels" />
      <q-btn unelevated dense color="primary" icon="print" label="Друк етикетки" :disable="!order" data-testid="printLabelsAgent" @click="printOrderLabels(order.id, { patientName, orderNumber: order.orderNumber })" />
      <q-btn-dropdown outline dense color="primary" icon="description" :label="`Бланк: ${variantLabel}`" :disable="!order" data-testid="reportVariant">
        <q-list dense>
          <q-item v-for="v in reportVariants" :key="v.value" clickable v-close-popup :active="reportVariant === v.value" @click="reportVariant = v.value; openReport()">
            <q-item-section avatar><q-icon :name="v.icon" /></q-item-section>
            <q-item-section><q-item-label>{{ v.label }}</q-item-label><q-item-label caption>{{ v.caption }}</q-item-label></q-item-section>
          </q-item>
        </q-list>
      </q-btn-dropdown>
      <q-btn outline dense color="primary" icon="picture_as_pdf" label="PDF" :disable="!order" type="a" :href="order ? $api.orderReportPdfUrl(order.id, reportVariant) : '#'" target="_blank" />
      <q-btn-dropdown outline dense color="grey-8" icon="more_horiz" :disable="!order">
        <q-list dense>
          <q-item clickable v-close-popup @click="editOpen = true" data-testid="orderEdit"><q-item-section avatar><q-icon name="edit" /></q-item-section><q-item-section>Редагувати замовлення</q-item-section></q-item>
          <q-item clickable v-close-popup @click="goWorkstation"><q-item-section avatar><q-icon name="fas fa-microscope" /></q-item-section><q-item-section>Відкрити на робочому столі</q-item-section></q-item>
          <q-item clickable v-close-popup @click="goPortal" :disable="!order || !order.patientId"><q-item-section avatar><q-icon name="person" /></q-item-section><q-item-section>Кабінет пацієнта</q-item-section></q-item>
          <q-separator />
          <q-item clickable v-close-popup class="text-negative" @click="deleteOpen = true" data-testid="orderDelete"><q-item-section avatar><q-icon name="delete" color="negative" /></q-item-section><q-item-section>Видалити замовлення</q-item-section></q-item>
        </q-list>
      </q-btn-dropdown>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div v-if="order">
      <!-- Пацієнт -->
      <div class="medlink-card q-pa-sm q-mb-md" style="border-left: 4px solid #4274A7">
        <div class="row items-center q-col-gutter-md">
          <div class="col-12 col-md-4">
            <div class="text-caption text-grey-7">Пацієнт</div>
            <div class="text-subtitle1 text-weight-bold">{{ patientName }}</div>
            <div v-if="patientLatin" class="text-caption text-grey-7" title="Транслітерація (обчислюється сервером, лише читання)"><q-icon name="translate" size="12px" /> {{ patientLatin }}</div>
            <div class="text-caption">{{ patientMeta }}</div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">Лікар</div>
            <div>{{ employeeName(order.doctorId) }}</div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">Відділення</div>
            <div>{{ departmentName(order.departmentId) }}</div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">Сума</div>
            <div class="text-weight-bold">{{ order.totalPrice | money }}</div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">е-Направлення</div>
            <div class="mono">{{ order.ehealthReferralId || '—' }}</div>
          </div>
          <div v-if="order.clinicalNotes" class="col-12 text-caption"><q-icon name="notes" /> {{ order.clinicalNotes }}</div>
        </div>
      </div>

      <!-- Бланки зовнішніх лабораторій -->
      <div v-if="attachments.length" class="medlink-card q-mb-md" data-testid="attachments">
        <div class="medlink-card__title"><span><q-icon name="attach_file" class="q-mr-xs" />Бланки та файли ({{ attachments.length }})</span></div>
        <q-list dense separator>
          <q-item v-for="a in attachments" :key="a.id" clickable tag="a" :href="$api.attachmentUrl(a.id)" target="_blank">
            <q-item-section avatar><q-icon name="picture_as_pdf" color="red-6" /></q-item-section>
            <q-item-section><q-item-label>{{ a.fileName }}</q-item-label><q-item-label caption>{{ a.performerName || 'Файл' }} · {{ formatDateTime(a.uploadedAt) }} · SHA-256 {{ a.sha256.slice(0, 12) }}…</q-item-label></q-item-section>
          </q-item>
        </q-list>
      </div>

      <!-- Процес + дії -->
      <div class="medlink-card q-mb-md">
        <div class="medlink-card__title">
          <span><q-icon name="route" class="q-mr-xs" />Процес замовлення</span>
          <span class="text-caption text-grey-6">роль: {{ roleLabel }}</span>
        </div>
        <div class="q-pa-sm">
          <order-stepper :order="order" :audit="audit" />
          <q-separator class="q-my-sm" />
          <order-actions-bar :allowed="actions" :source="actionsSource" :status="order.status" :busy="acting" @action="onAction" />
        </div>
      </div>

      <q-tabs v-model="tab" dense align="left" class="text-grey-8 bg-white medlink-card" active-color="primary" indicator-color="primary" narrow-indicator>
        <q-tab name="samples" icon="science" :label="`Проби (${samples.length})`" data-testid="tab-samples" />
        <q-tab name="tests" icon="biotech" :label="`Показники (${tests.length})`" data-testid="tab-tests" />
        <q-tab name="journal" icon="book" :label="`Журнал (${journalEntries.length})`" data-testid="tab-journal" />
        <q-tab name="process" icon="history" :label="`Історія (${audit.length})`" data-testid="tab-process" />
      </q-tabs>

      <q-tab-panels v-model="tab" animated class="bg-transparent q-mt-sm">
        <!-- Проби -->
        <q-tab-panel name="samples" class="q-pa-none">
          <div class="medlink-card">
            <q-table :data="samples" :columns="sampleColumns" row-key="id" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="Проб немає">
              <template v-slot:body-cell-barcode="props">
                <q-td :props="props">
                  <span class="mono text-weight-bold">{{ props.row.barcode }}</span>
                </q-td>
              </template>
              <template v-slot:body-cell-tube="props">
                <q-td :props="props">
                  <div class="row items-center no-wrap q-gutter-xs">
                    <div :style="{ background: tubeColor(props.row), width: '12px', height: '20px', borderRadius: '2px 2px 6px 6px', border: '1px solid #999' }" />
                    <span>{{ tubeName(props.row) }}</span>
                  </div>
                </q-td>
              </template>
              <template v-slot:body-cell-status="props">
                <q-td :props="props"><status-chip :value="props.row.status" type="sample" icon /></q-td>
              </template>
              <template v-slot:body-cell-quality="props">
                <q-td :props="props">
                  <q-badge v-if="props.row.isHemolyzed" color="negative" label="Гемоліз" class="q-mr-xs" />
                  <q-badge v-if="props.row.isLipemic" color="warning" text-color="dark" label="Ліпемія" class="q-mr-xs" />
                  <q-badge v-if="props.row.isIcteric" color="warning" text-color="dark" label="Іктер." class="q-mr-xs" />
                  <q-badge v-if="props.row.isClotted" color="negative" label="Згусток" class="q-mr-xs" />
                  <q-badge v-if="props.row.isInsufficientVolume" color="negative" label="Мало" class="q-mr-xs" />
                  <span v-if="props.row.rejectReason" class="text-negative text-caption">{{ props.row.rejectReason }}</span>
                </q-td>
              </template>
              <template v-slot:body-cell-actions="props">
                <q-td :props="props" class="text-right no-wrap">
                  <q-btn flat dense round size="sm" icon="label" color="grey-8" @click="previewLabel(props.row)"><q-tooltip>Етикетка (перегляд)</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="print" color="primary" @click="printSampleLabel(props.row.barcode, { patientName, orderNumber: order.orderNumber })"><q-tooltip>Друк етикетки</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="colorize" color="teal-6" :disable="props.row.status !== 'PENDING'" @click="collectSample(props.row)"><q-tooltip>Забір</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="inbox" color="cyan-7" :disable="!['COLLECTED', 'IN_TRANSIT'].includes(props.row.status)" @click="receiveSamples([props.row])"><q-tooltip>Прийняти</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="block" color="negative" :disable="['REJECTED', 'DISPOSED'].includes(props.row.status)" @click="rejectSamples([props.row])"><q-tooltip>Відхилити</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="biotech" color="teal-7" :to="{ name: 'lab-sample-processing', params: { barcode: props.row.barcode } }"><q-tooltip>Обробка зразка (аліквоти, етапи)</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="edit" color="primary" @click="editSample(props.row)"><q-tooltip>Картка проби</q-tooltip></q-btn>
                  <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeleteSample(props.row)"><q-tooltip>Видалити пробу</q-tooltip></q-btn>
                </q-td>
              </template>
            </q-table>
          </div>
        </q-tab-panel>

        <!-- Показники -->
        <q-tab-panel name="tests" class="q-pa-none">
          <div class="medlink-card">
            <div class="row items-center q-pa-sm q-gutter-sm">
              <q-btn dense outline color="green-8" icon="done_all" label="Верифікувати всі RESULTED" :disable="!resultedIds.length" @click="verifyBatch" data-testid="verifyBatch" />
              <q-btn dense outline color="teal-7" icon="verified" label="Автоверифікація" :disable="!resultedIds.length" @click="autoverify" />
              <q-space />
              <span class="text-caption text-grey-7">Подвійний клік по рядку — редагування результату</span>
            </div>
            <q-table :data="tests" :columns="testColumns" row-key="id" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="Показників немає">
              <template v-slot:body="props">
                <q-tr :props="props" :class="{ 'bg-red-1': isCritical(resultOf(props.row).flag) }" @dblclick="editResult(props.row)" :data-testid="`test-row-${props.row.testCode}`">
                  <q-td key="testCode" :props="props">
                    <span class="text-weight-bold">{{ props.row.testCode }}</span>
                    <q-badge v-if="props.row.isReflex" color="purple-5" label="reflex" class="q-ml-xs" />
                  </q-td>
                  <q-td key="testName" :props="props">{{ props.row.testName }} <q-badge v-if="props.row.performerName" color="purple-5" :label="`→ ${props.row.performerName}`" data-testid="performerBadge"><q-tooltip>Виконує зовнішня лабораторія (send-out)</q-tooltip></q-badge></q-td>
                  <q-td key="sample" :props="props" class="mono">{{ sampleBarcode(props.row.sampleId) }}</q-td>
                  <q-td key="value" :props="props" class="text-right">
                    <span :class="flagCss(resultOf(props.row).flag)" class="text-weight-bold">{{ valueOf(props.row) }}</span>
                    <span class="text-caption text-grey-7 q-ml-xs">{{ resultOf(props.row).unit }}</span>
                  </q-td>
                  <q-td key="reference" :props="props">{{ referenceDisplay(resultOf(props.row)) || '—' }}</q-td>
                  <q-td key="flag" :props="props" class="text-center"><flag-marker :flag="resultOf(props.row).flag || 'NONE'" /></q-td>
                  <q-td key="delta" :props="props" class="text-right" :class="resultOf(props.row).deltaAlert ? 'text-orange-9 text-weight-bold' : ''">{{ resultOf(props.row).deltaPercent | pct }}</q-td>
                  <q-td key="status" :props="props"><status-chip :value="props.row.status" type="test" /></q-td>
                  <q-td key="actions" :props="props" class="text-right no-wrap">
                    <q-btn flat dense round size="sm" icon="edit_note" color="primary" @click="editResult(props.row)"><q-tooltip>Ввести / змінити результат</q-tooltip></q-btn>
                    <q-btn flat dense round size="sm" icon="how_to_reg" color="green-8" :disable="!['RESULTED', 'NEEDS_REVIEW'].includes(props.row.status)" @click="verifyResult(props.row)"><q-tooltip>Верифікувати</q-tooltip></q-btn>
                    <q-btn flat dense round size="sm" icon="replay" color="purple-6" @click="rerun(props.row)"><q-tooltip>Повтор (rerun)</q-tooltip></q-btn>
                    <q-btn flat dense round size="sm" icon="block" color="negative" @click="rejectResult(props.row)"><q-tooltip>Відхилити результат</q-tooltip></q-btn>
                    <q-btn flat dense round size="sm" icon="history" color="grey-8" @click="showHistory(props.row)"><q-tooltip>Історія версій</q-tooltip></q-btn>
                    <q-btn flat dense round size="sm" icon="delete" color="grey-7" @click="askDeleteResult(props.row)"><q-tooltip>Видалити результат</q-tooltip></q-btn>
                  </q-td>
                </q-tr>
              </template>
            </q-table>
          </div>
        </q-tab-panel>

        <!-- Журнальні записи по підрозділах -->
        <q-tab-panel name="journal" class="q-pa-none">
          <div class="medlink-card">
            <q-table :data="journalEntries" :columns="journalColumns" row-key="id" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="Журнальних записів немає (ендпоінт /orders/{id}/journal-entries)" data-testid="journalEntries">
              <template v-slot:body-cell-journalNumber="props"><q-td :props="props"><b class="mono">{{ props.row.journalNumber }}</b></q-td></template>
              <template v-slot:body-cell-registeredAt="props"><q-td :props="props">{{ props.row.registeredAt || props.row.createdOn || props.row.at | datetime }}</q-td></template>
              <template v-slot:body-cell-section="props"><q-td :props="props"><router-link :to="{ name: 'lab-section-journal', query: { sectionId: props.row.sectionId } }" class="text-primary">{{ props.row.sectionName || props.row.sectionCode || props.row.sectionId }}</router-link></q-td></template>
              <template v-slot:body-cell-tests="props"><q-td :props="props" class="text-caption">{{ (props.row.tests || props.row.testCodes || []).map(t => t.testCode || t).join(', ') }}</q-td></template>
              <template v-slot:body-cell-status="props"><q-td :props="props"><status-chip :value="props.row.status" type="test" /></q-td></template>
            </q-table>
          </div>
        </q-tab-panel>

        <!-- Історія -->
        <q-tab-panel name="process" class="q-pa-none">
          <div class="medlink-card q-pa-sm">
            <audit-timeline :events="audit" :loading="auditLoading" />
          </div>
        </q-tab-panel>
      </q-tab-panels>
    </div>
    <div v-else-if="loading" class="text-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>
    <empty-state v-else title="Замовлення не знайдено" icon="search_off" />

    <!-- Діалоги -->
    <labels-dialog v-model="labelsOpen" :labels="labels" :loading="labelsLoading" />
    <html-preview-dialog v-model="reportOpen" :title="order ? `Бланк результатів ${order.orderNumber} — ${variantLabel}` : 'Бланк'" :html="reportHtml" :url="order ? $api.orderReportUrl(order.id, reportVariant) : ''" :pdf-url="order ? $api.orderReportPdfUrl(order.id, reportVariant) : ''" :loading="reportLoading" :error="reportError" @retry="openReport" />
    <collect-sample-dialog v-model="collectOpen" :sample="activeSample" :patient-name="patientName" @collected="load" />
    <receive-sample-dialog v-model="receiveOpen" :samples="dialogSamples" @received="load" />
    <reject-sample-dialog v-model="rejectOpen" :samples="dialogSamples" @rejected="load" />
    <sample-edit-dialog v-model="sampleEditOpen" :sample="activeSample || {}" @saved="load" />
    <result-editor-dialog v-model="resultOpen" :row="activeRow" @saved="load" />
    <verify-result-dialog v-model="verifyOpen" :row="activeRow" @verified="load" />
    <edit-order-dialog v-model="editOpen" :order="order || {}" @saved="load" />
    <confirm-dialog v-model="cancelOpen" title="Скасувати замовлення" :message="`Замовлення ${order && order.orderNumber} буде скасовано. Вкажіть причину.`" ok-label="Скасувати замовлення" color="grey-8" icon="cancel" with-reason reason-required @confirm="doCancel" />
    <confirm-dialog v-model="deleteOpen" title="Видалити замовлення" message="Замовлення буде видалено (soft delete з аудитом). Дію неможливо скасувати через UI." ok-label="Видалити" color="negative" icon="delete_forever" @confirm="doDelete" />
    <confirm-dialog v-model="deleteSampleOpen" title="Видалити пробу" :message="`Пробу ${activeSample && activeSample.barcode} буде видалено.`" ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteSample" />
    <confirm-dialog v-model="deleteResultOpen" title="Видалити результат" :message="`Запис результату ${activeRow.testCode} буде видалено; тест повернеться у статус PENDING.`" ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteResult" />
    <confirm-dialog v-model="rejectResultOpen" title="Відхилити результат" :message="`Результат ${activeRow.testCode} буде відхилено.`" ok-label="Відхилити" color="negative" icon="block" with-reason reason-required @confirm="doRejectResult" />
    <confirm-dialog v-model="releaseOpen" title="Видати результати" message="Усі тести мають бути VERIFIED/AUTO_VERIFIED. Пацієнт отримає сповіщення, бланк отримає QR верифікації." ok-label="Видати" color="positive" icon="verified" @confirm="doRelease" />

    <q-dialog v-model="historyOpen">
      <q-card style="min-width: 640px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm">
          <div class="text-subtitle1">Історія версій результату {{ activeRow.testCode }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section>
          <q-table :data="history" :columns="historyColumns" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" row-key="version" no-data-label="Історії немає">
            <template v-slot:body-cell-enteredAt="props"><q-td :props="props">{{ props.row.enteredAt || props.row.at | datetime }}</q-td></template>
          </q-table>
        </q-card-section>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import LabelsDialog from '../../../components/common/LabelsDialog.vue';
import HtmlPreviewDialog from '../../../components/common/HtmlPreviewDialog.vue';
import OrderStepper from '../../../components/orders/OrderStepper.vue';
import OrderActionsBar from '../../../components/orders/OrderActionsBar.vue';
import AuditTimeline from '../../../components/orders/AuditTimeline.vue';
import CollectSampleDialog from '../../../components/samples/CollectSampleDialog.vue';
import ReceiveSampleDialog from '../../../components/samples/ReceiveSampleDialog.vue';
import RejectSampleDialog from '../../../components/samples/RejectSampleDialog.vue';
import SampleEditDialog from '../../../components/samples/SampleEditDialog.vue';
import ResultEditorDialog from '../../../components/results/ResultEditorDialog.vue';
import VerifyResultDialog from '../../../components/results/VerifyResultDialog.vue';
import EditOrderDialog from './EditOrderDialog.vue';
import labelPrintMixin from '../../../mixins/labelPrintMixin';
import { resolveActions } from '../../../utils/orderActions';
import { isCritical, flagMeta, ROLE } from '../../../utils/statuses';
import { formatDateTime, formatNumber, patientDisplay, genderLabel, ageFromBirthDate, referenceDisplay } from '../../../utils/format';

export default {
  name: 'OrderCard',
  mixins: [apiMixin, labelPrintMixin],
  components: { LabelsDialog, HtmlPreviewDialog, OrderStepper, OrderActionsBar, AuditTimeline, CollectSampleDialog, ReceiveSampleDialog, RejectSampleDialog, SampleEditDialog, ResultEditorDialog, VerifyResultDialog, EditOrderDialog },
  props: { id: { type: String, required: true } },
  data () {
    return {
      order: null,
      attachments: [],
      transitions: null,
      reportVariant: 'final',
      reportVariants: [
        { value: 'final', label: 'Остаточний', caption: 'підписаний бланк із QR верифікації', icon: 'verified' },
        { value: 'preliminary', label: 'Попередній', caption: 'з водяним знаком «ПОПЕРЕДНІЙ»', icon: 'hourglass_top' },
        { value: 'cito', label: 'CITO', caption: 'термінова форма', icon: 'bolt' }
      ],
      audit: [],
      auditLoading: false,
      journalEntries: [],
      journalColumns: [
        { name: 'journalNumber', label: '№ журналу', field: 'journalNumber', align: 'left' },
        { name: 'dayNumber', label: '№ за день', field: r => r.dayNumber || r.daySeq || '—', align: 'center' },
        { name: 'section', label: 'Підрозділ', align: 'left' },
        { name: 'registeredAt', label: 'Зареєстровано', align: 'left' },
        { name: 'barcode', label: 'Проба', field: 'barcode', align: 'left' },
        { name: 'tests', label: 'Дослідження', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' }
      ],
      tab: 'tests',
      acting: false,
      labelsOpen: false, labels: [], labelsLoading: false,
      reportOpen: false, reportHtml: '', reportLoading: false, reportError: '',
      collectOpen: false, receiveOpen: false, rejectOpen: false, sampleEditOpen: false,
      resultOpen: false, verifyOpen: false, editOpen: false, cancelOpen: false, deleteOpen: false,
      deleteSampleOpen: false, deleteResultOpen: false, rejectResultOpen: false, releaseOpen: false,
      historyOpen: false, history: [],
      activeSample: null, activeRow: {}, dialogSamples: [],
      sampleColumns: [
        { name: 'barcode', label: 'Штрихкод', field: 'barcode', align: 'left' },
        { name: 'tube', label: 'Пробірка', align: 'left' },
        { name: 'biomaterial', label: 'Біоматеріал', field: r => this.biomaterialName(r), align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'collectedAt', label: 'Забрано', field: r => formatDateTime(r.collectedAt), align: 'left' },
        { name: 'receivedAt', label: 'Прийнято', field: r => formatDateTime(r.receivedAt), align: 'left' },
        { name: 'quality', label: 'Якість', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ],
      testColumns: [
        { name: 'testCode', label: 'Код', align: 'left' },
        { name: 'testName', label: 'Показник', align: 'left' },
        { name: 'sample', label: 'Проба', align: 'left' },
        { name: 'value', label: 'Результат', align: 'right' },
        { name: 'reference', label: 'Норма', align: 'left' },
        { name: 'flag', label: 'Прапорець', align: 'center' },
        { name: 'delta', label: 'Δ%', align: 'right' },
        { name: 'status', label: 'Статус', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ],
      historyColumns: [
        { name: 'version', label: 'Версія', field: 'version', align: 'left' },
        { name: 'value', label: 'Значення', field: r => r.numericValue !== null && r.numericValue !== undefined ? formatNumber(r.numericValue) : (r.stringValue || r.value || '—'), align: 'right' },
        { name: 'flag', label: 'Прапорець', field: 'flag', align: 'left' },
        { name: 'enteredAt', label: 'Коли', align: 'left' },
        { name: 'by', label: 'Хто', field: r => r.enteredByName || r.enteredById || r.userId || '—', align: 'left' },
        { name: 'comment', label: 'Коментар', field: r => r.operatorComment || r.verificationComment || r.comment || '', align: 'left' }
      ]
    };
  },
  computed: {
    samples () { return (this.order && this.order.samples) || []; },
    tests () { return (this.order && this.order.tests) || []; },
    patientName () { return this.order ? (patientDisplay(this.order.patient) !== '—' ? patientDisplay(this.order.patient) : (this.order.patientName || '—')) : ''; },
    patientLatin () { const p = (this.order && this.order.patient) || {}; return [p.lastNameLatin, p.firstNameLatin].filter(Boolean).join(' '); },
    variantLabel () { return (this.reportVariants.find(v => v.value === this.reportVariant) || {}).label; },
    patientMeta () {
      const p = (this.order && this.order.patient) || {};
      const age = ageFromBirthDate(p.birthDate);
      return [p.birthDate ? `${formatDateTime(p.birthDate).slice(0, 10)}` : null, age !== null ? `${age} р.` : null, genderLabel(p.gender) !== '—' ? genderLabel(p.gender) : null, p.phone, p.taxId ? `ІПН ${p.taxId}` : null].filter(Boolean).join(' · ');
    },
    actionsResolved () { return resolveActions(this.order, this.transitions); },
    actions () { return this.actionsResolved.actions; },
    actionsSource () { return this.actionsResolved.source; },
    roleLabel () { return ROLE[this.$store.getters['context/currentRole']] || 'не визначено'; },
    resultedIds () { return this.tests.filter(t => ['RESULTED', 'NEEDS_REVIEW'].includes(t.status)).map(t => t.id); },
    currentEmployeeId () { return this.$store.state.context.employeeId; }
  },
  watch: {
    currentEmployeeId () { this.loadTransitions(); }
  },
  created () {
    this.$store.dispatch('dictionaries/loadMany', ['tube-types', 'biomaterials', 'employees', 'departments', 'tests']);
    this.load().then(() => {
      if (this.$route.query.labels) this.openLabels();
    });
  },
  methods: {
    formatDateTime, isCritical, referenceDisplay,
    flagCss (flag) { return flagMeta(flag || 'NONE').css; },
    async load () {
      const res = await this.callApi(() => this.$api.getOrder(this.id));
      if (res) this.order = res;
      this.loadTransitions();
      this.loadAudit();
      this.loadJournal();
      this.loadAttachments();
    },
    async loadAttachments () {
      if (!this.order) return;
      try { this.attachments = await this.$api.orderAttachments(this.order.id); } catch (e) { this.attachments = []; }
    },
    async loadJournal () {
      if (!this.order) return;
      try { this.journalEntries = this.asList(await this.$api.orderJournalEntries(this.order.id)); } catch (e) { this.journalEntries = []; }
    },
    async loadTransitions () {
      if (!this.order) return;
      try {
        this.transitions = await this.$api.orderTransitions(this.order.id);
      } catch (e) {
        this.transitions = null; // ендпоінт може бути відсутнім — fallback на allowedActions DTO / локальні правила
      }
    },
    async loadAudit () {
      if (!this.order) return;
      this.auditLoading = true;
      try {
        const res = await this.$api.audit({ entity: 'LabOrder', entityId: this.order.id, pageSize: 200 });
        this.audit = this.asList(res).sort((a, b) => new Date(b.at) - new Date(a.at));
      } catch (e) {
        this.audit = [];
      } finally {
        this.auditLoading = false;
      }
    },
    resultOf (t) { return t.result || t.lastResult || (t.results && t.results[0]) || {}; },
    valueOf (t) {
      const r = this.resultOf(t);
      if (r.numericValue !== null && r.numericValue !== undefined) return formatNumber(r.numericValue);
      if (r.stringValue) return r.stringValue;
      if (r.value !== undefined && r.value !== null) return isNaN(Number(r.value)) ? r.value : formatNumber(r.value);
      return '—';
    },
    rowForTest (t) {
      const r = this.resultOf(t);
      return { ...r, orderTestId: t.id, testCode: t.testCode, testName: t.testName, status: t.status, patientName: this.patientName, barcode: this.sampleBarcode(t.sampleId), value: r.numericValue !== undefined && r.numericValue !== null ? r.numericValue : r.stringValue };
    },
    sampleBarcode (sampleId) {
      const s = this.samples.find(x => x.id === sampleId);
      return s ? s.barcode : '—';
    },
    tube (s) { return (s.tube && typeof s.tube === 'object') ? s.tube : this.$store.getters['dictionaries/byId']('tube-types', s.tubeTypeId); },
    tubeName (s) { const t = this.tube(s); return (t && t.name) || s.tubeName || '—'; },
    tubeColor (s) { const t = this.tube(s); return (t && t.colorCode) || '#bbb'; },
    biomaterialName (s) { const b = (s.biomaterial && typeof s.biomaterial === 'object') ? s.biomaterial : this.$store.getters['dictionaries/byId']('biomaterials', s.biomaterialTypeId); return (b && b.name) || s.biomaterialName || '—'; },
    employeeName (id) { const e = this.$store.getters['dictionaries/byId']('employees', id); return e ? (e.fullName || e.name) : (id ? String(id).slice(0, 8) : '—'); },
    departmentName (id) { const d = this.$store.getters['dictionaries/byId']('departments', id); return d ? d.name : (id || '—'); },

    // ----- дії процесу -----
    async onAction (code) {
      const pending = this.samples.filter(s => s.status === 'PENDING');
      switch (code) {
        case 'COLLECT':
          if (pending.length) this.collectSample(pending[0]);
          else this.$q.notify({ type: 'info', message: 'Немає проб у статусі PENDING' });
          break;
        case 'RECEIVE':
          this.receiveSamples(this.samples.filter(s => ['COLLECTED', 'IN_TRANSIT'].includes(s.status)));
          break;
        case 'ENTER_RESULT':
          this.goWorkstation();
          break;
        case 'VERIFY':
          this.verifyBatch();
          break;
        case 'REJECT':
          this.rejectSamples(this.samples.filter(s => !['REJECTED', 'DISPOSED'].includes(s.status)));
          break;
        case 'RERUN': {
          const t = this.tests.find(x => ['RESULTED', 'NEEDS_REVIEW', 'VERIFIED', 'AUTO_VERIFIED'].includes(x.status)) || this.tests[0];
          if (t) this.rerun(t);
          break;
        }
        case 'RELEASE':
          this.releaseOpen = true;
          break;
        case 'CANCEL':
          this.cancelOpen = true;
          break;
        case 'REOPEN':
          await this.runAction(async () => {
            try { await this.$api.reopenOrder(this.order.id); } catch (e) {
              if (e.apiStatus === 404 || e.apiStatus === 405) await this.$api.orderTransition(this.order.id, 'REOPEN');
              else throw e;
            }
          }, 'Замовлення повернуто в роботу');
          break;
        default:
          await this.runAction(() => this.$api.orderTransition(this.order.id, code), `Дію ${code} виконано`);
      }
    },
    async runAction (fn, successMessage) {
      this.acting = true;
      try {
        await fn();
        if (successMessage) this.notifyOk(successMessage);
        await this.load();
      } catch (e) {
        this.notifyError(e);
      } finally {
        this.acting = false;
      }
    },
    collectSample (s) { this.activeSample = s; this.collectOpen = true; },
    receiveSamples (list) {
      if (!list.length) { this.$q.notify({ type: 'info', message: 'Немає проб для прийому' }); return; }
      this.dialogSamples = list; this.receiveOpen = true;
    },
    rejectSamples (list) {
      if (!list.length) { this.$q.notify({ type: 'info', message: 'Немає проб для відхилення' }); return; }
      this.dialogSamples = list; this.rejectOpen = true;
    },
    editSample (s) { this.activeSample = s; this.sampleEditOpen = true; },
    askDeleteSample (s) { this.activeSample = s; this.deleteSampleOpen = true; },
    doDeleteSample () { this.runAction(() => this.$api.deleteSample(this.activeSample.barcode), 'Пробу видалено'); },
    async previewLabel (s) {
      this.labelsOpen = true; this.labelsLoading = true; this.labels = [];
      try {
        const l = await this.$api.sampleLabel(s.barcode);
        this.labels = [{ patientName: this.patientName, orderNumber: this.order.orderNumber, ...l }];
      } catch (e) { this.notifyError(e); } finally { this.labelsLoading = false; }
    },
    async openLabels () {
      this.labelsOpen = true; this.labelsLoading = true; this.labels = [];
      try {
        const res = await this.$api.orderLabels(this.order.id);
        this.labels = this.asList(res).map(l => ({ patientName: this.patientName, orderNumber: this.order.orderNumber, ...l }));
      } catch (e) { this.notifyError(e); } finally { this.labelsLoading = false; }
    },
    async openReport () {
      this.reportOpen = true; this.reportLoading = true; this.reportError = ''; this.reportHtml = '';
      try {
        this.reportHtml = await this.$api.orderReportHtml(this.order.id, this.reportVariant);
      } catch (e) { this.reportError = e.userMessage || 'Не вдалося отримати бланк'; } finally { this.reportLoading = false; }
    },
    editResult (t) { this.activeRow = this.rowForTest(t); this.resultOpen = true; },
    verifyResult (t) { this.activeRow = this.rowForTest(t); this.verifyOpen = true; },
    rejectResult (t) { this.activeRow = this.rowForTest(t); this.rejectResultOpen = true; },
    doRejectResult (reason) { this.runAction(() => this.$api.rejectResult(this.activeRow.orderTestId, reason), 'Результат відхилено'); },
    rerun (t) { this.runAction(() => this.$api.rerunResult(t.id), `Тест ${t.testCode} поставлено на повтор`); },
    askDeleteResult (t) { this.activeRow = this.rowForTest(t); this.deleteResultOpen = true; },
    doDeleteResult () { this.runAction(() => this.$api.deleteResult(this.activeRow.orderTestId), 'Результат видалено'); },
    async showHistory (t) {
      this.activeRow = this.rowForTest(t); this.historyOpen = true; this.history = [];
      try { this.history = this.asList(await this.$api.resultHistory(t.id)); } catch (e) { this.notifyError(e); }
    },
    async verifyBatch () {
      if (!this.resultedIds.length) return;
      await this.runAction(async () => {
        const res = await this.$api.verifyBatch(this.resultedIds);
        const skipped = (res && res.skipped) || [];
        if (skipped.length) this.$q.notify({ type: 'warning', message: `Верифіковано: ${res.verified || 0}; пропущено: ${skipped.map(s => s.reason).join('; ')}`, timeout: 6000 });
        else this.notifyOk(`Верифіковано: ${(res && res.verified) || this.resultedIds.length}`);
      });
    },
    async autoverify () {
      await this.runAction(async () => {
        const res = await this.$api.autoverify(this.resultedIds);
        const blocked = (res && res.blocked) || [];
        this.$q.notify({ type: blocked.length ? 'warning' : 'positive', message: `Автоверифіковано: ${(res && res.verified) || 0}; заблоковано: ${blocked.length}` });
      });
    },
    doCancel (reason) { this.runAction(() => this.$api.cancelOrder(this.order.id, reason), 'Замовлення скасовано'); },
    doRelease () { this.runAction(() => this.$api.releaseOrder(this.order.id), 'Результати видано'); },
    async doDelete () {
      this.acting = true;
      try {
        await this.$api.deleteOrder(this.order.id);
        this.notifyOk('Замовлення видалено');
        this.$router.push({ name: 'lab-orders' });
      } catch (e) { this.notifyError(e); } finally { this.acting = false; }
    },
    goWorkstation () { this.$router.push({ name: 'lab-workstation', query: { search: this.order.orderNumber } }); },
    goPortal () { this.$router.push({ name: 'portal-orders', params: { patientId: this.order.patientId } }); }
  }
};
</script>
