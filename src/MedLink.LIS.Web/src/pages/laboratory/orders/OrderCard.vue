<template>
  <div class="order-card" data-testid="orderCard">
    <page-header
      :title="order ? `Направлення ${order.orderNumber}` : 'Картка направлення'"
      icon="fas fa-file-medical-alt"
      :subtitle="order ? `створено ${formatDateTime(order.orderDatetime)}` : ''"
      :breadcrumbs="[{ label: 'Реєстрація направлень', to: { name: 'lab-orders' } }, { label: order ? order.orderNumber : '…' }]"
    >
      <template v-slot:title-after>
        <status-chip v-if="order" :value="order.status" type="order" icon data-testid="orderStatus" />
        <q-badge v-if="order && order.isUrgentCito" color="deep-orange-6" label="CITO" />
      </template>
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load"><q-tooltip>Оновити</q-tooltip></q-btn>
      <q-btn unelevated dense color="primary" icon="print" label="Друк етикетки" :disable="!order" data-testid="printLabelsAgent" @click="printOrderLabels(order.id, { patientName, orderNumber: order.orderNumber })" />
      <q-btn-dropdown outline dense color="primary" icon="description" :label="`Бланк: ${variantLabel}`" :disable="!order" data-testid="reportVariant">
        <q-list dense>
          <q-item v-for="v in reportVariants" :key="v.value" clickable v-close-popup :active="reportVariant === v.value" @click="reportVariant = v.value; openReport()">
            <q-item-section avatar><q-icon :name="v.icon" /></q-item-section>
            <q-item-section><q-item-label>{{ v.label }}</q-item-label><q-item-label caption>{{ v.caption }}</q-item-label></q-item-section>
          </q-item>
          <q-separator />
          <q-item clickable v-close-popup tag="a" :href="order ? $api.orderReportPdfUrl(order.id, reportVariant) : '#'" target="_blank">
            <q-item-section avatar><q-icon name="picture_as_pdf" /></q-item-section>
            <q-item-section><q-item-label>Завантажити PDF</q-item-label><q-item-label caption>бланк «{{ variantLabel }}»</q-item-label></q-item-section>
          </q-item>
        </q-list>
      </q-btn-dropdown>
      <q-btn-dropdown outline dense color="grey-8" icon="more_horiz" :disable="!order">
        <q-list dense>
          <q-item clickable v-close-popup @click="openLabels"><q-item-section avatar><q-icon name="label" /></q-item-section><q-item-section>Переглянути етикетки</q-item-section></q-item>
          <q-item v-if="canDo('REGISTRAR', 'LAB_DOCTOR')" clickable v-close-popup @click="editOpen = true" data-testid="orderEdit"><q-item-section avatar><q-icon name="edit" /></q-item-section><q-item-section>Редагувати направлення</q-item-section></q-item>
          <q-item v-if="canDo('LAB_DOCTOR', 'LAB_TECHNICIAN') && movableTests.length" clickable v-close-popup @click="openAnalyzerReroute(null)" data-testid="orderReroute"><q-item-section avatar><q-icon name="alt_route" color="purple-6" /></q-item-section><q-item-section>Перенаправити на інший прилад</q-item-section></q-item>
          <q-item clickable v-close-popup @click="goWorkstation"><q-item-section avatar><q-icon name="fas fa-microscope" /></q-item-section><q-item-section>Відкрити на робочому столі</q-item-section></q-item>
          <q-item clickable v-close-popup @click="goPortal" :disable="!order || !order.patientId"><q-item-section avatar><q-icon name="person" /></q-item-section><q-item-section>Кабінет пацієнта</q-item-section></q-item>
          <template v-if="canDo('REGISTRAR')">
            <q-separator />
            <q-item clickable v-close-popup class="text-negative" @click="deleteOpen = true" data-testid="orderDelete"><q-item-section avatar><q-icon name="delete" color="negative" /></q-item-section><q-item-section>Видалити направлення</q-item-section></q-item>
          </template>
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
            <div v-if="patientLatin" class="text-caption text-grey-7" title="Транслітерація (обчислюється сервером, лише читання)"><q-icon name="translate" size="12px" /> Латиницею: {{ patientLatin }}</div>
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
            <div class="text-caption text-grey-7">Направлення</div>
            <div data-testid="orderReferral"><b>{{ order.referralTypeName || 'Самозвернення' }}</b><span v-if="order.referralNumber">: <span class="mono">№ {{ order.referralNumber }}</span></span></div>
            <div v-if="order.referralStatus" class="text-caption text-grey-7">Статус в ЕСОЗ: {{ order.referralStatus }}</div>
            <div v-if="order.referrerOrganizationName || order.referrerDoctorName" class="text-caption text-grey-7">{{ [order.referrerOrganizationName, order.referrerDoctorName].filter(Boolean).join(' · ') }}</div>
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

      <!-- Призначення МІС (ТЗ 21, 23) -->
      <order-items-panel v-if="order.items && order.items.length" :order="order" />

      <!-- Оплата (FR-GAP-060) -->
      <order-billing :order-id="order.id" />

      <!-- ЕСОЗ: підпис і відправка звіту (ТЗ 21.13) -->
      <ehealth-submit-panel v-if="order.status === 'RELEASED' || (order.ehealthStatus && order.ehealthStatus !== 'NOT_SENT')" :key="`eh-${order.id}-${order.status}`" :order-id="order.id" />

      <!-- Процес + дії -->
      <div class="medlink-card q-mb-md">
        <div class="medlink-card__title">
          <span><q-icon name="route" class="q-mr-xs" />Процес направлення</span>
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
        <q-tab name="journal" icon="book" :label="journalEntries.length ? `Журнал відділення (${journalEntries.length})` : 'Журнал відділення'" data-testid="tab-journal" />
        <q-tab name="process" icon="history" :label="audit.length ? `Історія (${audit.length})` : 'Історія'" data-testid="tab-process" />
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
                  <q-btn flat dense round size="sm" icon="print" color="primary" @click="printSampleLabel(props.row.barcode, { patientName, orderNumber: order.orderNumber })"><q-tooltip>Друк етикетки</q-tooltip></q-btn>
                  <q-btn v-if="sampleCan(props.row, 'COLLECT', 'PHLEBOTOMIST', 'LAB_TECHNICIAN')" flat dense round size="sm" icon="colorize" color="teal-6" @click="collectSample(props.row)"><q-tooltip>Забрати пробу</q-tooltip></q-btn>
                  <q-btn v-if="sampleCan(props.row, 'RECEIVE', 'LAB_TECHNICIAN', 'LAB_DOCTOR', 'LOGISTICS_COURIER')" flat dense round size="sm" icon="inbox" color="cyan-7" @click="receiveSamples([props.row])"><q-tooltip>Прийняти в лабораторії</q-tooltip></q-btn>
                  <q-btn-dropdown flat dense round size="sm" dropdown-icon="more_vert" no-icon-animation color="grey-8">
                    <q-list dense>
                      <q-item v-close-popup clickable @click="previewLabel(props.row)"><q-item-section avatar><q-icon name="label" /></q-item-section><q-item-section>Переглянути етикетку</q-item-section></q-item>
                      <q-item v-close-popup clickable :to="{ name: 'lab-sample-processing', params: { barcode: props.row.barcode } }"><q-item-section avatar><q-icon name="biotech" /></q-item-section><q-item-section>Обробка зразка (аліквоти, етапи)</q-item-section></q-item>
                      <q-item v-close-popup clickable @click="editSample(props.row)"><q-item-section avatar><q-icon name="edit" /></q-item-section><q-item-section>Картка проби</q-item-section></q-item>
                      <q-item v-if="sampleCan(props.row, 'REJECT', 'LAB_TECHNICIAN', 'LAB_DOCTOR')" v-close-popup clickable @click="rejectSamples([props.row])"><q-item-section avatar><q-icon name="block" color="negative" /></q-item-section><q-item-section>Відхилити пробу</q-item-section></q-item>
                      <q-item v-if="sampleCan(props.row, 'DELETE', 'REGISTRAR', 'PHLEBOTOMIST')" v-close-popup clickable class="text-negative" @click="askDeleteSample(props.row)"><q-item-section avatar><q-icon name="delete" color="negative" /></q-item-section><q-item-section>Видалити пробу</q-item-section></q-item>
                    </q-list>
                  </q-btn-dropdown>
                </q-td>
              </template>
            </q-table>
          </div>
        </q-tab-panel>

        <!-- Показники -->
        <q-tab-panel name="tests" class="q-pa-none">
          <div class="medlink-card">
            <div class="row items-center q-pa-sm q-gutter-sm">
              <q-btn dense outline color="green-8" icon="done_all" v-if="canDo('LAB_DOCTOR')" label="Верифікувати всі з результатом" :disable="!resultedIds.length" @click="verifyBatch" data-testid="verifyBatch" />
              <q-btn v-if="canDo('LAB_DOCTOR', 'LAB_TECHNICIAN')" dense outline color="teal-7" icon="verified" label="Автоверифікація" :disable="!resultedIds.length" @click="autoverify" />
              <q-space />
              <span v-if="canDo('LAB_DOCTOR', 'LAB_TECHNICIAN')" class="text-caption text-grey-7">Подвійний клік по рядку — редагування результату</span>
            </div>
            <q-table :data="tests" :columns="testColumns" row-key="id" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="Показників немає">
              <template v-slot:body="props">
                <q-tr :props="props" :class="{ 'bg-red-1': isCritical(resultOf(props.row).flag) }" @dblclick="testCan(props.row, 'EDIT_RESULT', 'LAB_DOCTOR', 'LAB_TECHNICIAN') && editResult(props.row)" :data-testid="`test-row-${props.row.testCode}`">
                  <q-td key="testName" :props="props"><span class="text-weight-bold">{{ title(props.row.testName) }}</span>&nbsp;<span class="text-caption text-grey-7">({{ props.row.testCode }})</span> <q-badge v-if="props.row.isReflex" color="purple-5" label="додатковий" class="q-ml-xs" /> <q-badge v-if="props.row.performerName" color="purple-5" :label="`→ ${props.row.performerName}`" data-testid="performerBadge"><q-tooltip>Виконує зовнішня лабораторія</q-tooltip></q-badge></q-td>
                  <q-td key="sample" :props="props" class="mono">{{ sampleBarcode(props.row.sampleId) }}</q-td>
                  <q-td key="value" :props="props" class="text-right">
                    <span :class="flagCss(resultOf(props.row).flag)" class="text-weight-bold">{{ valueOf(props.row) }}</span>
                    <span class="text-caption text-grey-7 q-ml-xs">{{ resultOf(props.row).unit }}</span>
                  </q-td>
                  <q-td key="reference" :props="props">{{ referenceDisplay(resultOf(props.row)) || '—' }}</q-td>
                  <q-td key="flag" :props="props" class="text-center"><flag-marker :flag="resultOf(props.row).flag || 'NONE'" /></q-td>
                  <q-td key="delta" :props="props" class="text-right" :class="resultOf(props.row).deltaAlert ? 'text-orange-9 text-weight-bold' : ''"><template v-if="resultOf(props.row).previousValue != null">{{ resultOf(props.row).deltaPercent | pct }}</template><template v-else>—</template></q-td>
                  <q-td key="status" :props="props"><status-chip :value="props.row.status" type="test" /></q-td>
                  <q-td key="actions" :props="props" class="text-right no-wrap">
                    <q-btn v-if="testCan(props.row, 'EDIT_RESULT', 'LAB_DOCTOR', 'LAB_TECHNICIAN') || testCan(props.row, 'ENTER_RESULT', 'LAB_DOCTOR', 'LAB_TECHNICIAN')" flat dense round size="sm" icon="edit_note" color="primary" @click="editResult(props.row)"><q-tooltip>Ввести / змінити результат</q-tooltip></q-btn>
                    <q-btn v-if="testCan(props.row, 'VERIFY', 'LAB_DOCTOR')" flat dense round size="sm" icon="how_to_reg" color="green-8" @click="verifyResult(props.row)"><q-tooltip>Верифікувати</q-tooltip></q-btn>
                    <q-btn-dropdown flat dense round size="sm" dropdown-icon="more_vert" no-icon-animation color="grey-8">
                      <q-list dense>
                        <q-item v-if="canDo('LAB_DOCTOR', 'LAB_TECHNICIAN') && ['PENDING', 'RERUN'].includes(props.row.status)" v-close-popup clickable data-testid="rerouteTest" @click="openAnalyzerReroute(props.row)"><q-item-section avatar><q-icon name="alt_route" color="purple-6" /></q-item-section><q-item-section>Перенаправити на інший прилад</q-item-section></q-item>
                        <q-item v-if="canRoute(props.row)" v-close-popup clickable data-testid="routeTest" @click="openRoute(props.row)"><q-item-section avatar><q-icon name="share" color="purple-6" /></q-item-section><q-item-section>Виконати в іншій лабораторії</q-item-section></q-item>
                        <q-item v-if="testCan(props.row, 'RERUN', 'LAB_DOCTOR', 'LAB_TECHNICIAN')" v-close-popup clickable @click="rerun(props.row)"><q-item-section avatar><q-icon name="replay" color="purple-6" /></q-item-section><q-item-section>Повторити дослідження</q-item-section></q-item>
                        <q-item v-if="testCan(props.row, 'REJECT', 'LAB_DOCTOR', 'LAB_TECHNICIAN')" v-close-popup clickable @click="rejectResult(props.row)"><q-item-section avatar><q-icon name="block" color="negative" /></q-item-section><q-item-section>Відхилити результат</q-item-section></q-item>
                        <q-item v-close-popup clickable @click="showHistory(props.row)"><q-item-section avatar><q-icon name="history" /></q-item-section><q-item-section>Історія версій</q-item-section></q-item>
                        <q-item v-if="testCan(props.row, 'DELETE_RESULT', 'LAB_DOCTOR')" v-close-popup clickable class="text-negative" @click="askDeleteResult(props.row)"><q-item-section avatar><q-icon name="delete" color="negative" /></q-item-section><q-item-section>Видалити результат</q-item-section></q-item>
                      </q-list>
                    </q-btn-dropdown>
                  </q-td>
                </q-tr>
              </template>
            </q-table>
          </div>
        </q-tab-panel>

        <!-- Журнальні записи по підрозділах -->
        <q-tab-panel name="journal" class="q-pa-none">
          <div class="medlink-card">
            <q-table :data="journalEntries" :columns="journalColumns" row-key="id" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="У журналі відділення записів немає" data-testid="journalEntries">
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
    <empty-state v-else title="Направлення не знайдено" icon="search_off" />

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
    <confirm-dialog v-model="cancelOpen" title="Скасувати направлення" :message="`Направлення ${order && order.orderNumber} буде скасовано. Вкажіть причину.`" ok-label="Скасувати направлення" color="grey-8" icon="cancel" with-reason reason-required @confirm="doCancel" />
    <confirm-dialog v-model="deleteOpen" title="Видалити направлення" message="Направлення буде видалено. Відновити його неможливо." ok-label="Видалити" color="negative" icon="delete_forever" @confirm="doDelete" />
    <confirm-dialog v-model="deleteSampleOpen" title="Видалити пробу" :message="deleteSampleMessage" ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteSample" />
    <confirm-dialog v-model="deleteResultOpen" title="Видалити результат" :message="`Запис результату ${activeRow.testCode} буде видалено; показник повернеться до статусу «Очікує».`" ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteResult" />
    <confirm-dialog v-model="rejectResultOpen" title="Відхилити результат" :message="`Результат ${activeRow.testCode} буде відхилено.`" ok-label="Відхилити" color="negative" icon="block" with-reason reason-required @confirm="doRejectResult" />
    <confirm-dialog v-model="releaseOpen" title="Видати результати" message="Усі показники мають бути верифіковані. Пацієнт отримає сповіщення, бланк отримає QR верифікації." ok-label="Видати" color="positive" icon="verified" @confirm="doRelease" />

    <q-dialog v-model="routeOpen">
      <q-card style="width: 460px; max-width: 96vw" data-testid="routeDialog">
        <q-card-section class="row items-center bg-purple-6 text-white q-py-sm">
          <div class="text-subtitle1">Хто виконує «{{ routeTest && routeTest.testName }}»</div><q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-select v-model="routePerformerId" outlined dense emit-value map-options :options="routeOptions" label="Лабораторія *" :loading="routeLoading" data-testid="routePerformer" />
          <div v-if="!routeLoading && routeOptions.length <= 1 && !(routeTest && routeTest.performerId)" class="text-caption text-grey-7">Жодна зовнішня лабораторія не виконує цей показник. Додайте його до прайсу лабораторії в розділі «Зовнішні лабораторії».</div>
          <q-input v-model="routeReason" outlined dense autogrow label="Причина (необов’язково)" />
          <div class="text-caption text-grey-7">Показник потрапить до черги «Зовнішні лабораторії → До відправки».</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Зберегти" :loading="acting" :disable="routePerformerId === undefined" data-testid="routeSave" @click="doRoute" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="arOpen">
      <q-card style="width: 480px; max-width: 96vw" data-testid="rerouteDialog">
        <q-card-section class="row items-center bg-purple-6 text-white q-py-sm">
          <div class="text-subtitle1">Перенаправити на інший прилад</div><q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-sm">
          <div class="text-caption text-grey-7">{{ arTest ? `Показник: ${arTest.testName || arTest.testCode}` : 'Усі показники замовлення, що чекають на виконання' }}</div>
          <q-select v-model="arAnalyzerId" outlined dense emit-value map-options :options="arOptions" label="Прилад *" :loading="arLoading" data-testid="rerouteAnalyzer" />
          <q-input v-model="arReason" outlined dense autogrow label="Причина *" data-testid="rerouteReason" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Перенаправити" :loading="acting" :disable="!arAnalyzerId || !arReason.trim()" data-testid="rerouteSave" @click="doAnalyzerReroute" />
        </q-card-actions>
      </q-card>
    </q-dialog>

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
import OrderBilling from '../../../components/orders/OrderBilling.vue';
import OrderItemsPanel from '../../../components/orders/OrderItemsPanel.vue';
import EhealthSubmitPanel from '../../../components/orders/EhealthSubmitPanel.vue';
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
import roleMixin from '../../../mixins/roleMixin';
import { resolveActions } from '../../../utils/orderActions';
import { isCritical, flagMeta, ROLE } from '../../../utils/statuses';
import { formatDateTime, formatNumber, patientDisplay, genderLabel, ageFromBirthDate, referenceDisplay, maskTaxId, testTitle, tubeTitle } from '../../../utils/format';

export default {
  name: 'OrderCard',
  mixins: [apiMixin, labelPrintMixin, roleMixin],
  components: { LabelsDialog, HtmlPreviewDialog, OrderStepper, OrderActionsBar, AuditTimeline, CollectSampleDialog, ReceiveSampleDialog, RejectSampleDialog, SampleEditDialog, ResultEditorDialog, VerifyResultDialog, EditOrderDialog, OrderBilling, OrderItemsPanel, EhealthSubmitPanel },
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
        { name: 'journalNumber', label: '№ у журналі', field: 'journalNumber', align: 'left' },
        { name: 'dayNumber', label: '№ за день', field: r => r.dayNumber || r.daySeq || '—', align: 'center' },
        { name: 'section', label: 'Підрозділ', align: 'left' },
        { name: 'registeredAt', label: 'Зареєстровано', align: 'left' },
        { name: 'barcode', label: 'Проба', field: 'barcode', align: 'left' },
        { name: 'tests', label: 'Дослідження', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' }
      ],
      tab: 'samples',
      acting: false,
      labelsOpen: false, labels: [], labelsLoading: false,
      reportOpen: false, reportHtml: '', reportLoading: false, reportError: '',
      collectOpen: false, receiveOpen: false, rejectOpen: false, sampleEditOpen: false,
      resultOpen: false, verifyOpen: false, editOpen: false, cancelOpen: false, deleteOpen: false,
      deleteSampleOpen: false, deleteResultOpen: false, rejectResultOpen: false, releaseOpen: false,
      historyOpen: false, history: [],
      arOpen: false, arTest: null, arAnalyzerId: null, arReason: '', arOptions: [], arLoading: false,
      routeOpen: false, routeTest: null, routePerformerId: undefined, routeReason: '', routeOptions: [], routeLoading: false,
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
                { name: 'testName', label: 'Показник', align: 'left' },
        { name: 'sample', label: 'Проба', align: 'left' },
        { name: 'value', label: 'Результат', align: 'right' },
        { name: 'reference', label: 'Норма', align: 'left' },
        { name: 'flag', label: 'Прапорець', align: 'center' },
        { name: 'delta', label: 'Зміна, %', align: 'right' },
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
      return [p.birthDate ? `${formatDateTime(p.birthDate).slice(0, 10)}` : null, age !== null ? `${age} р.` : null, genderLabel(p.gender) !== '—' ? genderLabel(p.gender) : null, p.phone, p.taxId ? `ІПН ${maskTaxId(p.taxId)}` : null].filter(Boolean).join(' · ');
    },
    actionsResolved () { return resolveActions(this.order, this.transitions); },
    actions () { return this.actionsResolved.actions; },
    actionsSource () { return this.actionsResolved.source; },
    roleLabel () { return ROLE[this.$store.getters['context/currentRole']] || 'не визначено'; },
    deleteSampleMessage () {
      const s = this.activeSample;
      if (!s || !this.order) return '';
      return `Пробу ${s.barcode} (${this.patientName}, направлення ${this.order.orderNumber}) буде видалено. Відновити її неможливо.`;
    },
    movableTests () { return this.tests.filter(t => ['PENDING', 'RERUN'].includes(t.status) && !t.performerId); },
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
    title: testTitle,
    formatDateTime, isCritical, referenceDisplay,
    /** Дія доступна за відповіддю сервера (статус + роль); якщо її немає — за роллю */
    allowedBy (item, action, roles) {
      if (item && Array.isArray(item.allowedActions)) return item.allowedActions.some(a => a.action === action);
      return this.canDo(...roles);
    },
    sampleCan (s, action, ...roles) { return this.allowedBy(s, action, roles); },
    testCan (t, action, ...roles) { return this.allowedBy(t, action, roles); },
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
    tubeName (s) { const t = this.tube(s); return tubeTitle((t && t.name) || s.tubeName || '—'); },
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
          else this.$q.notify({ type: 'info', message: 'Немає проб, що очікують забору' });
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
          }, 'Направлення повернуто в роботу');
          break;
        default:
          await this.runAction(() => this.$api.orderTransition(this.order.id, code), 'Дію виконано');
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
    rerun (t) { this.runAction(() => this.$api.rerunResult(t.id), `Показник ${t.testName || t.testCode} поставлено на повтор`); },
    askDeleteResult (t) { this.activeRow = this.rowForTest(t); this.deleteResultOpen = true; },
    doDeleteResult () { this.runAction(() => this.$api.deleteResult(this.activeRow.orderTestId), 'Результат видалено'); },
    // зміна виконавця доступна до відправки/виконання (серверна політика: адмін, лікар, лаборант, реєстратор)
    canRoute (t) { return ['PENDING', 'RERUN'].includes(t.status) && this.canDo('LAB_DOCTOR', 'LAB_TECHNICIAN', 'REGISTRAR'); },
    async openRoute (t) {
      this.routeTest = t; this.routePerformerId = undefined; this.routeReason = ''; this.routeOptions = []; this.routeOpen = true; this.routeLoading = true;
      try {
        const list = this.asList(await this.$api.performers());
        const ext = list.filter(p => p.kind === 'EXTERNAL' && p.isActive !== false && (p.tests || []).some(x => x.testId === t.testId && x.isActive !== false));
        this.routeOptions = [{ value: null, label: 'Наша лабораторія' }, ...ext.map(p => ({ value: p.id, label: p.name }))];
        this.routePerformerId = t.performerId || null;
      } catch (e) { this.notifyError(e); } finally { this.routeLoading = false; }
    },
    async doRoute () {
      const t = this.routeTest;
      await this.runAction(() => this.$api.routeOrderTest(t.id, this.routePerformerId, this.routeReason || null), this.routePerformerId ? `Показник ${t.testName} передано в зовнішню лабораторію` : `Показник ${t.testName} виконується в нашій лабораторії`);
      this.routeOpen = false;
    },
    // перенаправлення на інший прилад (S-56): кандидати з сервера, причина обов'язкова
    async openAnalyzerReroute (t) {
      this.arTest = t; this.arAnalyzerId = null; this.arReason = ''; this.arOptions = []; this.arOpen = true; this.arLoading = true;
      try {
        const probe = t || this.movableTests[0];
        const res = await this.$api.analyzerCandidates(probe.id);
        this.arOptions = ((res && res.candidates) || []).filter(c => !c.current).map(c => ({ value: c.analyzerId, label: `${c.name} · черга ${c.queue}${c.available ? '' : ' · недоступний: ' + c.reason}`, disable: !c.available }));
      } catch (e) { this.notifyError(e); } finally { this.arLoading = false; }
    },
    async doAnalyzerReroute () {
      const ids = this.arTest ? [this.arTest.id] : null;
      await this.runAction(async () => {
        const res = await this.$api.rerouteOrder(this.order.id, this.arAnalyzerId, this.arReason.trim(), ids);
        const sk = (res && res.skipped) || [];
        if (sk.length) this.$q.notify({ type: 'warning', message: `Перенаправлено: ${(res.moved || []).length}; не перенесено: ${sk.map(s => s.testCode + ' — ' + s.reason).join('; ')}`, timeout: 7000 });
        else this.notifyOk(`Перенаправлено на ${res.analyzer}: ${(res.moved || []).length}`);
      });
      this.arOpen = false;
    },
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
    doCancel (reason) { this.runAction(() => this.$api.cancelOrder(this.order.id, reason), 'Направлення скасовано'); },
    doRelease () { this.runAction(() => this.$api.releaseOrder(this.order.id), 'Результати видано'); },
    async doDelete () {
      this.acting = true;
      try {
        await this.$api.deleteOrder(this.order.id);
        this.notifyOk('Направлення видалено');
        this.$router.push({ name: 'lab-orders' });
      } catch (e) { this.notifyError(e); } finally { this.acting = false; }
    },
    goWorkstation () { this.$router.push({ name: 'lab-workstation', query: { search: this.order.orderNumber } }); },
    goPortal () { this.$router.push({ name: 'portal-orders', params: { patientId: this.order.patientId } }); }
  }
};
</script>
