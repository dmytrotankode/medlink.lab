<template>
  <div class="analyzers-page" data-testid="analyzersPage">
    <page-header title="Аналізатори та коннектори" icon="fas fa-network-wired" subtitle="Підключення приладів до ЛІС" :breadcrumbs="[{ label: 'Аналізатори' }]">
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
      <span class="inline-block">
        <q-btn outline dense color="primary" icon="router" label="Новий коннектор" data-testid="connectorCreate" :disable="!isAdmin" @click="connectorOpen = true" />
        <q-tooltip v-if="!isAdmin">{{ adminHint }}</q-tooltip>
      </span>
      <span class="inline-block">
        <q-btn unelevated dense color="primary" icon="add" label="Новий аналізатор" data-testid="analyzerCreate" :disable="!isAdmin" @click="openAnalyzer(null)" />
        <q-tooltip v-if="!isAdmin">{{ adminHint }}</q-tooltip>
      </span>
    </page-header>

    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <!-- Коннектори -->
    <div class="medlink-card q-mb-md">
      <div class="medlink-card__title"><span><q-icon name="router" class="q-mr-xs" />Інсталяції коннектора ({{ connectors.length }})</span><span class="text-caption text-grey-6">Стан перевіряється кожні 30 с; результати, отримані без зв’язку, зберігаються в буфері</span></div>
      <q-table :data="connectors" :columns="connectorColumns" row-key="id" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" :loading="loading" no-data-label="Коннекторів ще немає — створіть інсталяцію та отримайте ключ встановлення" data-testid="connectorsTable">
        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <status-chip :value="props.row.status" type="connector" icon />
            <div v-if="props.row.status === 'PENDING' && !props.row.lastHeartbeatAt" class="text-caption text-grey-7">Ще не підключався до сервера</div>
          </q-td>
        </template>
        <template v-slot:body-cell-lastHeartbeatAt="props"><q-td :props="props">{{ props.row.lastHeartbeatAt | datetime }}</q-td></template>
        <template v-slot:body-cell-analyzers="props"><q-td :props="props">{{ analyzers.filter(a => a.connectorId === props.row.id).map(a => a.name).join(', ') || '—' }}</q-td></template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap">
            <q-btn flat dense round size="sm" icon="visibility" color="grey-8" aria-label="Картка та журнал" @click="openConnectorCard(props.row)"><q-tooltip>Картка та журнал</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="download" color="primary" aria-label="Завантажити налаштування" type="a" :href="$api.connectorDownloadUrl(props.row.id)" target="_blank"><q-tooltip>Завантажити налаштування (ZIP з інструкцією)</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="vpn_key" color="orange-8" aria-label="Перевипустити ключ" :disable="!isAdmin" @click="rotateKey(props.row)"><q-tooltip>{{ isAdmin ? 'Перевипустити ключ' : adminHint }}</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="edit" color="primary" aria-label="Змінити коннектор" :disable="!isAdmin" @click="editConnector(props.row)"><q-tooltip>{{ isAdmin ? 'Змінити' : adminHint }}</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="power_off" color="negative" aria-label="Вимкнути коннектор" :disable="props.row.status === 'DISABLED' || !isAdmin" @click="disableConnector(props.row)"><q-tooltip>{{ isAdmin ? 'Вимкнути' : adminHint }}</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити коннектор" :disable="!isAdmin" @click="askDeleteConnector(props.row)"><q-tooltip>{{ isAdmin ? 'Видалити' : adminHint }}</q-tooltip></q-btn>
          </q-td>
        </template>
      </q-table>
    </div>

    <!-- Аналізатори -->
    <div class="medlink-card">
      <div class="medlink-card__title"><span><q-icon name="memory" class="q-mr-xs" />Аналізатори ({{ analyzers.length }})</span><router-link :to="{ name: 'lab-analyzer-messages' }" class="text-primary text-caption">Журнал обміну →</router-link></div>
      <q-table :data="analyzers" :columns="analyzerColumns" row-key="id" dense flat :pagination="{ rowsPerPage: 25 }" :loading="loading" no-data-label="Аналізаторів ще немає" data-testid="analyzersTable" @row-click="(e, row) => openAnalyzerCard(row)" class="cursor-pointer">
        <template v-slot:body-cell-online="props">
          <q-td :props="props">
            <span v-if="isManual(props.row)" class="text-grey-6">—</span>
            <q-badge v-else :color="onlineState(props.row).color" :label="onlineState(props.row).label" />
            <q-icon v-if="lockedAnalyzers.has(props.row.id)" name="lock" color="purple-6" class="q-ml-xs"><q-tooltip>Заблоковано контролем якості</q-tooltip></q-icon>
          </q-td>
        </template>
        <template v-slot:body-cell-type="props"><q-td :props="props">{{ typeName(props.row.analyzerTypeId) }} <q-badge color="teal-7" :label="protocolOf(props.row)" class="q-ml-xs" /></q-td></template>
        <template v-slot:body-cell-connection="props"><q-td :props="props" class="text-caption">{{ connectionText(props.row) }}</q-td></template>
        <template v-slot:body-cell-connector="props"><q-td :props="props">{{ connectorName(props.row.connectorId) }}</q-td></template>
        <template v-slot:body-cell-lastMessageAt="props"><q-td :props="props">{{ props.row.lastMessageAt | datetime }}<div v-if="props.row.lastError" class="text-caption text-negative ellipsis" style="max-width: 200px">{{ props.row.lastError }}<q-tooltip max-width="320px">{{ props.row.lastError }}</q-tooltip></div></q-td></template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="text-right no-wrap" @click.stop>
            <q-btn flat dense round size="sm" icon="science" color="teal-7" aria-label="Симулятор повідомлення" @click="openSimulator(props.row)"><q-tooltip>Симулятор повідомлення</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="receipt_long" color="grey-8" aria-label="Перегляд замовлення для приладу" @click="openPreview(props.row)"><q-tooltip>Перегляд замовлення для приладу</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="list_alt" color="grey-8" aria-label="Журнал обміну" :to="{ name: 'lab-analyzer-messages', query: { analyzerId: props.row.id } }"><q-tooltip>Журнал обміну</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="edit" color="primary" aria-label="Змінити аналізатор" :disable="!isAdmin" :data-testid="`anEdit-${props.row.code}`" @click="openAnalyzer(props.row)"><q-tooltip>{{ isAdmin ? 'Змінити' : adminHint }}</q-tooltip></q-btn>
            <q-btn flat dense round size="sm" icon="delete" color="grey-7" aria-label="Видалити аналізатор" :disable="!isAdmin" @click="askDeleteAnalyzer(props.row)"><q-tooltip>{{ isAdmin ? 'Видалити' : adminHint }}</q-tooltip></q-btn>
          </q-td>
        </template>
      </q-table>
    </div>

    <!-- Створення коннектора -->
    <q-dialog v-model="connectorOpen" no-backdrop-dismiss>
      <q-card style="min-width: 600px; max-width: 96vw" data-testid="connectorDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><q-icon name="router" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">{{ created ? 'Коннектор створено' : 'Нова інсталяція коннектора' }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup @click="created = null" /></q-card-section>
        <q-card-section v-if="!created" class="q-gutter-y-sm">
          <q-input v-model="newConnectorName" outlined dense label="Назва (наприклад, «Лаб ПК 1 — гематологія»)" autofocus data-testid="connectorName" @keyup.enter="createConnector" />
          <div class="text-caption text-grey-7">Після створення буде показано одноразовий ключ встановлення. Збережіть його — повторно ключ не відображається.</div>
          <div v-if="connectorError" class="text-negative">{{ connectorError }}</div>
        </q-card-section>
        <q-card-section v-else class="q-gutter-y-sm" data-testid="connectorCreated">
          <q-banner dense rounded class="bg-orange-1 text-orange-10"><q-icon name="warning" /> Ключ показується лише один раз — збережіть його.</q-banner>
          <div class="text-caption text-grey-7">Ключ встановлення</div>
          <div class="row items-center no-wrap"><div class="mono text-h6 q-mr-sm" data-testid="installKey">{{ created.installKey }}</div><q-btn flat dense round icon="content_copy" @click="copy(created.installKey)" /></div>
          <div class="text-caption text-grey-7">Для технічного фахівця: команда налаштування на ПК лабораторії</div>
          <div class="row items-center no-wrap bg-grey-2 q-pa-sm rounded-borders"><code class="mono col" style="font-size: 12px; word-break: break-all">{{ created.setupCommand || `MedLink.LabConnector setup --server ${origin} --install-key ${created.installKey} --name "${newConnectorName}"` }}</code><q-btn flat dense round icon="content_copy" @click="copy(created.setupCommand || `MedLink.LabConnector setup --server ${origin} --install-key ${created.installKey}`)" /></div>
          <q-btn color="primary" icon="download" label="Завантажити налаштування (ZIP з інструкцією)" type="a" :href="$api.connectorDownloadUrl(created.id)" target="_blank" class="full-width" />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn v-if="!created" flat label="Скасувати" v-close-popup />
          <q-btn v-if="!created" color="primary" icon="add" label="Створити" :loading="saving" :disable="!newConnectorName" data-testid="connectorSave" @click="createConnector" />
          <q-btn v-else color="primary" label="Готово" v-close-popup @click="created = null" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Редагування коннектора -->
    <q-dialog v-model="connectorEditOpen" no-backdrop-dismiss>
      <q-card style="min-width: 480px">
        <q-card-section class="row items-center bg-primary text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">Коннектор</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-input v-model="connectorForm.name" outlined dense label="Назва" />
          <q-select v-model="connectorForm.status" outlined dense label="Статус" :options="connectorStatusOptions" emit-value map-options />
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat label="Скасувати" v-close-popup /><q-btn color="primary" label="Зберегти" :loading="saving" @click="saveConnector" /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Картка коннектора -->
    <q-dialog v-model="connectorCardOpen">
      <q-card style="min-width: 720px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">{{ activeConnector && activeConnector.name }}</div><q-space /><status-chip v-if="activeConnector" :value="activeConnector.status" type="connector" /><q-btn flat round dense icon="close" v-close-popup class="q-ml-sm" /></q-card-section>
        <q-card-section v-if="activeConnector">
          <div class="row q-col-gutter-sm text-body2">
            <div class="col-6">Хост: <b>{{ activeConnector.hostName || '—' }}</b> · версія {{ activeConnector.version || '—' }}<q-badge v-if="activeConnector.versionOutdated" color="negative" class="q-ml-sm" label="Застаріла версія" /></div>
            <div v-if="activeConnector.versionOutdated" class="col-12 text-negative">{{ activeConnector.versionWarning }}</div>
            <div class="col-6">Останній сигнал: {{ activeConnector.lastHeartbeatAt | datetime }} · у буфері: <b>{{ activeConnector.bufferedCount || 0 }}</b></div>
            <div class="col-12">Аналізатори: {{ analyzers.filter(a => a.connectorId === activeConnector.id).map(a => a.name).join(', ') || '—' }}</div>
          </div>
          <div class="section-title q-mt-md">Журнал коннектора</div>
          <q-table :data="connectorLogs" :columns="logColumns" dense flat row-key="id" :pagination="{ rowsPerPage: 10 }" no-data-label="Записів немає">
            <template v-slot:body-cell-at="props"><q-td :props="props">{{ props.row.at | datetime }}</q-td></template>
            <template v-slot:body-cell-level="props"><q-td :props="props"><q-badge :color="{ ERROR: 'negative', WARN: 'warning', WARNING: 'warning', INFO: 'primary' }[String(props.row.level).toUpperCase()] || 'grey-6'" :label="props.row.level" /></q-td></template>
          </q-table>
        </q-card-section>
      </q-card>
    </q-dialog>

    <!-- Картка аналізатора -->
    <q-dialog v-model="analyzerCardOpen">
      <q-card style="min-width: 640px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm"><div class="text-subtitle1 text-weight-bold">{{ activeAnalyzer && activeAnalyzer.name }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section v-if="activeAnalyzer">
          <q-markup-table dense flat>
            <tbody>
              <tr><td class="text-grey-7">Модель</td><td>{{ typeName(activeAnalyzer.analyzerTypeId) }} · {{ protocolOf(activeAnalyzer) }}</td></tr>
              <tr><td class="text-grey-7">Підключення</td><td>{{ connectionText(activeAnalyzer) }}</td></tr>
              <tr><td class="text-grey-7">Коннектор</td><td>{{ connectorName(activeAnalyzer.connectorId) }}</td></tr>
              <tr><td class="text-grey-7">Стан</td><td><q-badge :color="onlineState(activeAnalyzer).color" :label="onlineState(activeAnalyzer).label" /> <template v-if="activeAnalyzer.lastMessageAt">останнє повідомлення {{ activeAnalyzer.lastMessageAt | datetime }}</template></td></tr>
              <tr v-if="activeAnalyzer.lastError"><td class="text-grey-7">Помилка</td><td class="text-negative">{{ activeAnalyzer.lastError }}</td></tr>
              <tr><td class="text-grey-7">Карта параметрів</td><td><q-chip v-for="p in (activeAnalyzer.parameterMap || [])" :key="p.analyzerCode" dense size="sm" color="blue-1" text-color="primary">{{ p.analyzerCode }} → {{ p.testCode }}</q-chip><span v-if="!(activeAnalyzer.parameterMap || []).length" class="text-grey-6">—</span></td></tr>
            </tbody>
          </q-markup-table>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1"><q-btn flat color="teal-7" icon="science" label="Симулятор" @click="analyzerCardOpen = false; openSimulator(activeAnalyzer)" /><q-btn flat color="primary" icon="edit" label="Редагувати" :disable="!isAdmin" @click="analyzerCardOpen = false; openAnalyzer(activeAnalyzer)"><q-tooltip v-if="!isAdmin">{{ adminHint }}</q-tooltip></q-btn><q-btn flat label="Закрити" v-close-popup /></q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Симулятор -->
    <q-dialog v-model="simulatorOpen">
      <q-card style="min-width: 860px; max-width: 98vw" data-testid="simulatorDialog">
        <q-card-section class="row items-center bg-teal-7 text-white q-py-sm"><q-icon name="science" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Симулятор: {{ activeAnalyzer && activeAnalyzer.name }} ({{ activeAnalyzer && protocolOf(activeAnalyzer) }})</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section>
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6">
              <q-input v-model="rawMessage" type="textarea" outlined input-class="mono" rows="14" label="Необроблене повідомлення приладу" data-testid="rawMessage" />
              <div class="row q-gutter-sm q-mt-xs">
                <q-btn dense flat color="grey-8" label="Приклад ASTM" @click="rawMessage = sampleAstm" />
                <q-btn dense flat color="grey-8" label="Приклад HL7" @click="rawMessage = sampleHl7" />
                <q-space />
                <q-btn color="teal-7" icon="play_arrow" label="Розібрати" :loading="saving" :disable="!rawMessage" data-testid="simulateRun" @click="simulate" />
              </div>
            </div>
            <div class="col-12 col-md-6">
              <div v-if="simResult">
                <div class="section-title">Розпізнані результати ({{ (simResult.parsedResults || []).length }})</div>
                <q-markup-table dense flat bordered>
                  <thead><tr><th class="text-left">Штрихкод</th><th class="text-left">Код приладу</th><th class="text-left">Показник ЛІС</th><th>Значення</th><th class="text-left">Од. вим.</th><th>Прапорці</th></tr></thead>
                  <tbody>
                    <tr v-for="(r, i) in (simResult.parsedResults || [])" :key="i">
                      <td class="mono">{{ r.barcode }}</td><td class="mono">{{ r.analyzerCode }}</td><td>{{ r.testCode || '—' }}</td><td class="text-right text-weight-bold">{{ r.value }}</td><td>{{ r.unit }}</td><td class="text-center">{{ r.flags }}</td>
                    </tr>
                    <tr v-if="!(simResult.parsedResults || []).length"><td colspan="6" class="text-grey-6 text-center">Результатів не розпізнано</td></tr>
                  </tbody>
                </q-markup-table>
                <div v-if="(simResult.warnings || []).length" class="q-mt-sm">
                  <div class="section-title">Попередження</div>
                  <q-banner v-for="(w, i) in simResult.warnings" :key="i" dense class="bg-orange-1 text-orange-10 q-mb-xs">{{ w }}</q-banner>
                </div>
                <div v-if="simResult.kind || simResult.messageKind" class="text-caption q-mt-sm">Тип повідомлення: <b>{{ simResult.kind || simResult.messageKind }}</b> <span v-if="simResult.barcode">· штрихкод запиту {{ simResult.barcode }}</span></div>
              </div>
              <empty-state v-else title="Результат розбору" icon="science" hint="Повідомлення розбирається без збереження результатів" />
            </div>
          </div>
        </q-card-section>
      </q-card>
    </q-dialog>

    <!-- Order preview -->
    <q-dialog v-model="previewOpen">
      <q-card style="min-width: 640px; max-width: 96vw">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm"><q-icon name="receipt_long" class="q-mr-sm" /><div class="text-subtitle1 text-weight-bold">Замовлення для приладу: {{ activeAnalyzer && activeAnalyzer.name }}</div><q-space /><q-btn flat round dense icon="close" v-close-popup /></q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-input v-model="previewBarcode" outlined dense label="Штрихкод проби" data-testid="previewBarcode" @keyup.enter="loadPreview"><template v-slot:append><q-btn flat dense icon="search" :loading="saving" @click="loadPreview" /></template></q-input>
          <pre v-if="previewText" class="mono bg-grey-2 q-pa-sm rounded-borders" style="white-space: pre-wrap; font-size: 12px; max-height: 50vh; overflow: auto" data-testid="previewText">{{ previewText }}</pre>
          <div v-if="previewError" class="text-negative">{{ previewError }}</div>
        </q-card-section>
      </q-card>
    </q-dialog>

    <analyzer-form-dialog v-model="analyzerOpen" :analyzer="activeAnalyzer" :connectors="connectors" @saved="load" />
    <confirm-dialog v-model="deleteAnalyzerOpen" title="Видалити аналізатор?" message="Аналізатор буде прибрано зі списку. Карта параметрів та історія обміну збережуться для аудиту." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteAnalyzer" />
    <confirm-dialog v-model="deleteConnectorOpen" title="Видалити коннектор?" message="Інсталяцію коннектора буде видалено, її ключ стане недійсним." ok-label="Видалити" color="negative" icon="delete" @confirm="doDeleteConnector" />
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';
import { CONNECTOR_STATUS, toOptions } from '../../../utils/statuses';
import AnalyzerFormDialog from './AnalyzerFormDialog.vue';
import { copyToClipboard } from '../../../utils/format';

export default {
  name: 'AnalyzersPage',
  mixins: [apiMixin, roleMixin],
  components: { AnalyzerFormDialog },
  data () {
    return {
      connectors: [], analyzers: [], connectorLogs: [],
      connectorOpen: false, connectorEditOpen: false, connectorCardOpen: false, analyzerOpen: false, analyzerCardOpen: false, simulatorOpen: false, previewOpen: false, deleteAnalyzerOpen: false, deleteConnectorOpen: false,
      newConnectorName: '', connectorError: '', created: null, connectorForm: {}, saving: false,
      activeConnector: null, activeAnalyzer: null,
      rawMessage: '', simResult: null, previewBarcode: '', previewText: '', previewError: '',
      sampleAstm: 'H|\\^&|||Sysmex XN-1000^00-01|||||||P|1394-97|20261007081500\rP|1\rO|1|10260048||^^^^WBC\\^^^^HGB|R||||||N\rR|1|^^^^WBC|7.45|10*9/L|4.0^9.0|N||F||||20261007081400\rR|2|^^^^HGB|148|g/L|130^160|N||F||||20261007081400\rL|1|N\r',
      sampleHl7: 'MSH|^~\\&|BS-240|Mindray|LIS|MedLink|20261007081500||ORU^R01|MSG0001|P|2.3.1\rPID|1||PAT001||Коваленко^Олена||19850412|F\rOBR|1||10260048|^^^GLU\rOBX|1|NM|GLU^Глюкоза||5.4|mmol/L|4.1-5.9|N|||F\rOBX|2|NM|ALT^АЛТ||68.5|U/L|0-41|H|||F\r',
      connectorColumns: [
        { name: 'name', label: 'Назва', field: 'name', align: 'left' },
        { name: 'hostName', label: 'Хост', field: r => r.hostName || '—', align: 'left' },
        { name: 'version', label: 'Версія', field: r => (r.version || '—') + (r.versionOutdated ? ' (застаріла, потрібна ' + r.minVersion + '+)' : ''), align: 'left', classes: r => r.versionOutdated ? 'text-negative' : '' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'lastHeartbeatAt', label: 'Останній сигнал', field: 'lastHeartbeatAt', align: 'left' },
        { name: 'bufferedCount', label: 'Буфер', field: r => r.bufferedCount || 0, align: 'center' },
        { name: 'analyzers', label: 'Аналізатори', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ],
      analyzerColumns: [
        { name: 'online', label: 'Стан', align: 'left' },
        { name: 'code', label: 'Код', field: 'code', align: 'left', sortable: true },
        { name: 'name', label: 'Назва', field: 'name', align: 'left', sortable: true },
        { name: 'type', label: 'Модель / протокол', align: 'left' },
        { name: 'connection', label: 'Підключення', align: 'left' },
        { name: 'connector', label: 'Коннектор', align: 'left' },
        { name: 'lastMessageAt', label: 'Останнє повідомлення', align: 'left' },
        { name: 'actions', label: '', align: 'right' }
      ],
      logColumns: [
        { name: 'at', label: 'Час', field: 'at', align: 'left' },
        { name: 'level', label: 'Рівень', field: 'level', align: 'left' },
        { name: 'message', label: 'Повідомлення', field: 'message', align: 'left' },
        { name: 'analyzerId', label: 'Аналізатор', field: r => this.analyzerName(r.analyzerId), align: 'left' }
      ]
    };
  },
  computed: {
    isAdmin () { return this.canDo(); }, // створення, зміна й видалення приладів та коннекторів — лише адміністратор лабораторії
    adminHint () { return 'Недоступно для вашої ролі. Доступно: адміністратор лабораторії'; },
    connectorStatusOptions () { return toOptions(CONNECTOR_STATUS); },
    types () { return this.$store.getters['dictionaries/items']('analyzer-types'); },
    lockedAnalyzers () { return new Set(this.$store.state.laboratory.activeLockouts.map(l => l.analyzerId)); },
    origin () { return window.location.origin; }
  },
  created () { this.$store.dispatch('dictionaries/load', 'analyzer-types'); this.load(); },
  methods: {
    // Модель з довідника типів; якщо типу немає — «Не вказано» (числовий ідентифікатор типу на екран не виводимо)
    typeName (id) { const t = this.types.find(x => x.id === id); return t && t.name ? t.name : 'Не вказано'; },
    // Протокол без внутрішніх суфіксів: ASTM_ASK → ASTM
    protocolOf (a) { const p = a.protocol || (this.types.find(x => x.id === a.analyzerTypeId) || {}).exchType; return p ? String(p).split('_')[0] : '—'; },
    // «Ручна постановка» — віртуальний прилад без підключення
    isManual (a) { return a.code === 'MANUAL' || /^ручна/i.test(a.name || ''); },
    // Стан узгоджено з шапкою «Шлюз аналізаторів»: «Онлайн» лише для налаштованого підключення з активним коннектором
    onlineState (a) {
      if (this.connectionText(a) === 'Підключення не налаштовано') return { label: 'Не налаштовано', color: 'grey-5' };
      const c = a.connectorId ? this.connectors.find(x => x.id === a.connectorId) : null;
      if (c && c.status !== 'ACTIVE') return { label: 'Коннектор не підключено', color: 'orange-7' };
      return a.isOnline ? { label: 'Онлайн', color: 'positive' } : { label: 'Не в мережі', color: 'grey-6' };
    },
    connectorName (id) { const c = this.connectors.find(x => x.id === id); return c ? c.name : '—'; },
    analyzerName (id) { const a = this.analyzers.find(x => x.id === id); return a ? a.name : (id || ''); },
    connectionText (a) {
      if (this.isManual(a)) return '—';
      if (a.connectionMode === 'TCP') {
        if (a.isTcpServer) return a.tcpPort ? `Очікує підключення, порт ${a.tcpPort}` : 'Підключення не налаштовано';
        const host = a.tcpHost && a.tcpHost !== '0.0.0.0' ? a.tcpHost : '';
        return host && a.tcpPort ? `TCP/IP: ${host} : ${a.tcpPort}` : 'Підключення не налаштовано';
      }
      if (a.connectionMode === 'COM') return a.comPort ? `COM-порт ${a.comPort}, ${a.baudRate} бод` : 'Підключення не налаштовано';
      return a.filePath ? `Файл: ${a.filePath}` : 'Підключення не налаштовано';
    },
    async load () {
      const results = await Promise.allSettled([this.$api.getConnectors(), this.$api.getAnalyzers()]);
      if (results[0].status === 'fulfilled') this.connectors = this.asList(results[0].value);
      if (results[1].status === 'fulfilled') this.analyzers = this.asList(results[1].value);
      const failed = results.find(r => r.status === 'rejected');
      this.apiError = failed && !this.apiOffline ? failed.reason.userMessage : null;
      this.$store.dispatch('laboratory/refreshStatus');
    },
    copy (t) { copyToClipboard(t).then(() => this.notifyOk('Скопійовано')); },
    async createConnector () {
      this.saving = true; this.connectorError = '';
      try { this.created = await this.$api.createConnector(this.newConnectorName); this.load(); } catch (e) { this.connectorError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    },
    editConnector (c) { this.activeConnector = c; this.connectorForm = { id: c.id, name: c.name, status: c.status }; this.connectorEditOpen = true; },
    async saveConnector () { this.saving = true; try { await this.$api.updateConnector(this.connectorForm.id, { name: this.connectorForm.name, status: this.connectorForm.status }); this.notifyOk('Коннектор оновлено'); this.connectorEditOpen = false; this.load(); } catch (e) { this.notifyError(e); } finally { this.saving = false; } },
    async openConnectorCard (c) {
      this.activeConnector = c; this.connectorCardOpen = true; this.connectorLogs = [];
      try { const full = await this.$api.getConnector(c.id); this.activeConnector = { ...c, ...full }; } catch (e) { /* ignore */ }
      try { this.connectorLogs = this.asList(await this.$api.connectorLogs(c.id, { pageSize: 50 })); } catch (e) { /* ignore */ }
    },
    rotateKey (c) {
      this.$q.dialog({ title: 'Перевипустити ключ?', message: `Старий ключ коннектора «${c.name}» перестане діяти. Коннектор потрібно буде налаштувати заново з новим ключем.`, cancel: { label: 'Скасувати', flat: true }, ok: { label: 'Перевипустити', color: 'orange-8' }, focus: 'cancel' })
        .onOk(async () => {
          try { const res = await this.$api.rotateConnectorKey(c.id); this.created = { ...res, id: c.id, installKey: res.installKey || res.apiKey }; this.newConnectorName = c.name; this.connectorOpen = true; } catch (e) { this.notifyError(e); }
        });
    },
    async disableConnector (c) { try { await this.$api.disableConnector(c.id); this.notifyOk('Коннектор вимкнено'); this.load(); } catch (e) { this.notifyError(e); } },
    askDeleteConnector (c) { this.activeConnector = c; this.deleteConnectorOpen = true; },
    async doDeleteConnector () { try { await this.$api.deleteConnector(this.activeConnector.id); this.notifyOk('Коннектор видалено'); this.load(); } catch (e) { this.notifyError(e); } },
    openAnalyzer (a) { this.activeAnalyzer = a; this.analyzerOpen = true; },
    openAnalyzerCard (a) { this.activeAnalyzer = a; this.analyzerCardOpen = true; },
    askDeleteAnalyzer (a) { this.activeAnalyzer = a; this.deleteAnalyzerOpen = true; },
    async doDeleteAnalyzer () { try { await this.$api.deleteAnalyzer(this.activeAnalyzer.id); this.notifyOk('Аналізатор видалено'); this.load(); } catch (e) { this.notifyError(e); } },
    openSimulator (a) { this.activeAnalyzer = a; this.simResult = null; this.simulatorOpen = true; },
    async simulate () { this.saving = true; try { this.simResult = await this.$api.simulateAnalyzer(this.activeAnalyzer.id, this.rawMessage); } catch (e) { this.notifyError(e); } finally { this.saving = false; } },
    openPreview (a) { this.activeAnalyzer = a; this.previewText = ''; this.previewError = ''; this.previewOpen = true; },
    async loadPreview () {
      if (!this.previewBarcode) return;
      this.saving = true; this.previewError = ''; this.previewText = '';
      try { const res = await this.$api.analyzerOrderPreview(this.activeAnalyzer.id, this.previewBarcode.trim()); this.previewText = typeof res === 'string' ? res : JSON.stringify(res, null, 2); } catch (e) { this.previewError = e.userMessage || 'Помилка'; } finally { this.saving = false; }
    }
  }
};
</script>
