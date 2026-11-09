<template>
  <div class="containers-panel" data-testid="containersPanel">
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <div class="medlink-card q-pa-sm q-mb-md row items-center q-col-gutter-sm">
      <div class="col-12 col-md-3">
        <q-select v-model="statusFilter" dense outlined clearable label="Статус" :options="statusOptions" emit-value map-options data-testid="containerStatusFilter" @input="load" />
      </div>
      <div class="col-12 col-md-4">
        <q-input v-model="search" dense outlined clearable label="Штрихкод контейнера або пробірки" data-testid="containerSearch" @keyup.enter="load" @clear="load">
          <template v-slot:append><q-icon name="search" class="cursor-pointer" @click="load" /></template>
        </q-input>
      </div>
      <div class="col-12 col-md-5 text-right">
        <q-btn flat dense color="primary" icon="settings" label="Типи тари" data-testid="containerTypesBtn" @click="openTypes" />
        <q-btn outline color="cyan-7" icon="inbox" label="Прийняти контейнер" class="q-ml-sm" data-testid="receiveContainerBtn" @click="openReceiveByScan" />
        <q-btn unelevated color="primary" icon="add" label="Новий контейнер" class="q-ml-sm" data-testid="createContainerBtn" @click="openCreate" />
      </div>
    </div>

    <div class="medlink-card">
      <q-table :data="containers" :columns="columns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[15, 50]" no-data-label="Контейнерів ще немає. Натисніть «Новий контейнер»." data-testid="containersTable">
        <template v-slot:body="props">
          <q-tr :props="props" :class="{ 'bg-red-1': props.row.isColdChainViolated, 'bg-orange-1': !props.row.isColdChainViolated && props.row.hasDiscrepancies }" class="cursor-pointer" :data-testid="'containerRow-' + props.row.barcode" @click="openCard(props.row)">
            <q-td key="barcode" :props="props"><span class="mono text-weight-bold">{{ props.row.barcode }}</span></q-td>
            <q-td key="type" :props="props">{{ props.row.typeName }}</q-td>
            <q-td key="route" :props="props">{{ props.row.originDepartmentName || depName(props.row.originDepartmentId) }} → {{ props.row.destinationDepartmentName || depName(props.row.destinationDepartmentId) }}</q-td>
            <q-td key="items" :props="props" class="text-center">{{ props.row.itemsCount }} / {{ props.row.capacity }}</q-td>
            <q-td key="temp" :props="props" class="text-center">
              <span>{{ fmtTemp(props.row.temperatureSeal) }}</span>
              <span class="text-grey-6"> → </span>
              <span :class="props.row.isColdChainViolated ? 'text-negative text-weight-bold' : ''">{{ fmtTemp(props.row.temperatureReceipt) }}</span>
              <q-icon v-if="props.row.isColdChainViolated" name="warning" color="negative" class="q-ml-xs"><q-tooltip>Порушення холодового ланцюга</q-tooltip></q-icon>
              <q-icon v-else-if="props.row.hasDiscrepancies" name="rule" color="orange-8" class="q-ml-xs"><q-tooltip>Є розбіжності при прийомі</q-tooltip></q-icon>
            </q-td>
            <q-td key="status" :props="props"><status-chip :value="props.row.status" type="container" icon /></q-td>
            <q-td key="createdOn" :props="props">{{ props.row.createdOn | datetime }}</q-td>
          </q-tr>
        </template>
      </q-table>
    </div>

    <!-- Новий контейнер -->
    <q-dialog v-model="createOpen" persistent>
      <q-card style="min-width: 480px; max-width: 96vw" data-testid="containerCreateDialog">
        <q-card-section class="row items-center bg-primary text-white q-py-sm">
          <q-icon name="inventory_2" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">Новий контейнер</div>
          <q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-select v-model="createForm.typeId" outlined dense label="Тип тари *" :options="typeOptions" emit-value map-options data-testid="containerTypeSelect" />
          <q-select v-model="createForm.originDepartmentId" outlined dense label="Звідки (пункт відправлення) *" :options="depOptions" emit-value map-options data-testid="containerOrigin" />
          <q-select v-model="createForm.destinationDepartmentId" outlined dense label="Куди (пункт призначення) *" :options="destinationOptions" emit-value map-options data-testid="containerDestination" />
          <div v-if="!createValid" class="text-caption text-grey-7">Не заповнено: {{ createMissing.join(', ') }}</div>
          <div v-if="createError" class="text-negative">{{ createError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="primary" icon="add" label="Створити" :loading="saving" :disable="!createValid" data-testid="containerCreateConfirm" @click="doCreate" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Картка контейнера: укладка -->
    <q-dialog v-model="cardOpen" @hide="onCardHide">
      <q-card style="width: 860px; max-width: 96vw" data-testid="containerCard">
        <q-card-section v-if="active" class="row items-center bg-grey-8 text-white q-py-sm">
          <q-icon name="inventory_2" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold mono" data-testid="containerCardBarcode">{{ active.barcode }}</div>
          <div class="q-ml-sm text-caption">{{ active.typeName }} · {{ active.itemsCount }} / {{ active.capacity }}</div>
          <q-space /><status-chip :value="active.status" type="container" /><q-btn flat round dense icon="close" v-close-popup class="q-ml-sm" />
        </q-card-section>
        <q-card-section v-if="active" class="q-gutter-y-sm">
          <q-banner v-if="active.isColdChainViolated" dense rounded class="bg-red-1 text-negative" data-testid="containerColdBanner">
            <q-icon name="warning" /> Порушення холодового ланцюга: запечатування {{ fmtTemp(active.temperatureSeal) }}, прийом {{ fmtTemp(active.temperatureReceipt) }} (режим {{ regime(active) }})
          </q-banner>
          <q-banner v-if="active.hasDiscrepancies" dense rounded class="bg-orange-1 text-orange-10" data-testid="containerDiscrepancyBanner">
            <q-icon name="rule" /> Розбіжності при прийомі:
            <span v-if="active.discrepancies.missing.length">відсутні — {{ active.discrepancies.missing.join(', ') }}; </span>
            <span v-if="active.discrepancies.damaged.length">пошкоджені — {{ active.discrepancies.damaged.join(', ') }}; </span>
            <span v-if="active.discrepancies.extra.length">зайві — {{ active.discrepancies.extra.join(', ') }}</span>
          </q-banner>

          <div class="row q-col-gutter-sm text-body2">
            <div class="col-6">Маршрут: <b>{{ active.originDepartmentName }} → {{ active.destinationDepartmentName }}</b></div>
            <div class="col-6">Температурний режим: <b>{{ regime(active) }}</b></div>
            <div class="col-6" v-if="active.sealedAt">Запечатано: {{ active.sealedAt | datetime }} · {{ fmtTemp(active.temperatureSeal) }}</div>
            <div class="col-6" v-if="active.manifestNumber">Маршрутний лист: <b class="mono">{{ active.manifestNumber }}</b></div>
            <div class="col-6" v-if="active.receivedAt">Прийнято: {{ active.receivedAt | datetime }} · {{ fmtTemp(active.temperatureReceipt) }}</div>
            <div class="col-6" v-if="active.returnedAt">Тару повернуто: {{ active.returnedAt | datetime }}</div>
          </div>

          <!-- Скан проби в місце -->
          <div v-if="canDo('PACK')" class="row items-start q-col-gutter-sm">
            <div class="col">
              <q-input ref="scan" v-model="scanValue" outlined dense autofocus label="Скануйте штрихкод пробірки" :disable="scanBusy || active.freeSlots === 0" data-testid="containerScanInput" @keyup.enter="doPack">
                <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
              </q-input>
              <div v-if="scanError" class="text-negative text-caption q-mt-xs" data-testid="containerScanError">{{ scanError }}</div>
              <div v-else-if="active.freeSlots === 0" class="text-orange-9 text-caption q-mt-xs">Контейнер заповнений</div>
            </div>
          </div>

          <!-- Сітка місць -->
          <div class="slots-grid" data-testid="containerSlots" :style="{ gridTemplateColumns: 'repeat(' + gridCols + ', minmax(0, 1fr))' }">
            <div
              v-for="s in slots" :key="s.position"
              class="slot" :class="slotClass(s)"
              :data-testid="'containerSlot-' + s.position"
              @click="onSlotClick(s)"
            >
              <div class="slot-no">{{ s.position }}</div>
              <div class="slot-bc mono">{{ s.item ? s.item.barcode : '' }}</div>
              <q-tooltip v-if="s.item">{{ [s.item.barcode, s.item.patientName, s.item.tubeName].filter(Boolean).join(' · ') }}<span v-if="canDo('UNPACK')"> — клік зніме пробірку з місця</span></q-tooltip>
            </div>
          </div>

          <q-expansion-item dense expand-separator icon="history" label="Історія" data-testid="containerEvents" @show="loadEvents">
            <div v-if="!events.length" class="text-grey-6 q-pa-sm">Подій немає</div>
            <q-list dense separator>
              <q-item v-for="e in events" :key="e.id">
                <q-item-section>
                  <q-item-label>{{ eventLabel(e.eventType) }}<span v-if="e.sampleBarcode" class="mono"> · {{ e.sampleBarcode }}<span v-if="e.position"> (місце {{ e.position }})</span></span><span v-if="e.temperature !== null && e.temperature !== undefined"> · {{ fmtTemp(e.temperature) }}</span></q-item-label>
                  <q-item-label v-if="e.details" caption>{{ e.details }}</q-item-label>
                </q-item-section>
                <q-item-section side>{{ e.at | datetime }}</q-item-section>
              </q-item>
            </q-list>
          </q-expansion-item>
        </q-card-section>
        <q-card-actions v-if="active" align="right" class="bg-grey-1">
          <q-btn v-if="canDo('DELETE')" flat color="grey-8" icon="delete" label="Видалити" data-testid="containerDeleteBtn" @click="deleteOpen = true" />
          <q-space />
          <q-btn v-if="canDo('SEAL')" unelevated color="deep-purple-5" icon="lock" label="Запечатати" data-testid="containerSealBtn" @click="openSeal" />
          <q-btn v-if="canDo('DISPATCH')" unelevated color="indigo-5" icon="local_shipping" label="Відправити" data-testid="containerDispatchBtn" @click="openDispatch" />
          <q-btn v-if="canDo('RECEIVE')" unelevated color="cyan-7" icon="inbox" label="Прийняти контейнер" data-testid="containerReceiveBtn" @click="openReceive(active)" />
          <q-btn v-if="canDo('RETURN')" unelevated color="blue-grey-6" icon="keyboard_return" label="Повернути тару" data-testid="containerReturnBtn" @click="returnOpen = true" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Запечатування -->
    <q-dialog v-model="sealOpen" persistent>
      <q-card style="min-width: 420px; max-width: 96vw" data-testid="sealDialog">
        <q-card-section class="row items-center bg-deep-purple-5 text-white q-py-sm">
          <q-icon name="lock" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">Запечатування {{ active && active.barcode }}</div>
          <q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-input v-model="sealTemp" outlined dense autofocus label="Температура в контейнері, °C *" inputmode="decimal" data-testid="sealTemperature" :hint="'Режим: ' + (active ? regime(active) : '')" @keyup.enter="doSeal" />
          <q-banner v-if="sealOut" dense rounded class="bg-orange-1 text-deep-orange-9"><template v-slot:avatar><q-icon name="warning" color="deep-orange" /></template>Поза режимом — буде зафіксовано порушення холодового ланцюга.</q-banner>
          <div class="text-caption text-grey-7">Після запечатування проби не можна змінити; буде створено маршрутний лист.</div>
          <div v-if="sealError" class="text-negative">{{ sealError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="deep-purple-5" icon="lock" label="Запечатати" :loading="saving" :disable="parseTemp(sealTemp) === null" data-testid="sealConfirm" @click="doSeal" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Відправка -->
    <q-dialog v-model="dispatchOpen" persistent>
      <q-card style="min-width: 420px; max-width: 96vw" data-testid="dispatchContainerDialog">
        <q-card-section class="row items-center bg-indigo-5 text-white q-py-sm">
          <q-icon name="local_shipping" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">Відправка {{ active && active.barcode }}</div>
          <q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <q-input v-model="courier.name" outlined dense autofocus label="Кур’єр *" data-testid="containerCourierName" />
          <q-input v-model="courier.phone" outlined dense label="Телефон" data-testid="containerCourierPhone" @keyup.enter="doDispatch" />
          <div v-if="dispatchError" class="text-negative">{{ dispatchError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn color="indigo-5" icon="local_shipping" label="Відправити" :loading="saving" :disable="!courier.name" data-testid="dispatchConfirm" @click="doDispatch"><q-tooltip v-if="!courier.name">Вкажіть кур’єра</q-tooltip></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Прийом контейнера: розкомплектування -->
    <q-dialog v-model="receiveOpen" persistent>
      <q-card style="width: 900px; max-width: 96vw" data-testid="receiveContainerDialog">
        <q-card-section class="row items-center bg-cyan-7 text-white q-py-sm">
          <q-icon name="inbox" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">Прийом контейнера <span v-if="rcv" class="mono">{{ rcv.barcode }}</span></div>
          <q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>

        <!-- Крок 1: скан контейнера -->
        <q-card-section v-if="!rcv">
          <q-input v-model="rcvContainerScan" outlined dense autofocus label="1. Скануйте штрихкод контейнера" data-testid="receiveContainerScan" @keyup.enter="findContainer">
            <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
          </q-input>
          <div v-if="rcvError" class="text-negative text-caption q-mt-xs" data-testid="receiveContainerError">{{ rcvError }}</div>
        </q-card-section>

        <!-- Крок 2: скан проб -->
        <q-card-section v-else class="q-gutter-y-sm">
          <div class="text-body2">{{ rcv.originDepartmentName }} → {{ rcv.destinationDepartmentName }} · {{ rcv.typeName }} · режим <b>{{ regime(rcv) }}</b></div>
          <q-input ref="rcvScan" v-model="rcvSampleScan" outlined dense autofocus label="2. Скануйте пробірки з контейнера" data-testid="receiveSampleScan" @keyup.enter="scanSample">
            <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
          </q-input>
          <div v-if="rcvError" class="text-negative text-caption" data-testid="receiveSampleError">{{ rcvError }}</div>

          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-7">
              <div class="section-title">Очікується ({{ rcv.items.length }})</div>
              <div class="expected-list" data-testid="receiveExpectedList">
                <div v-for="i in rcv.items" :key="i.id" class="expected-row row items-center no-wrap" :class="rowClass(i.barcode)" :data-testid="'receiveRow-' + i.barcode">
                  <div class="col-auto text-grey-7 q-mr-sm" style="width: 28px">{{ i.position }}</div>
                  <div class="col mono">{{ i.barcode }}</div>
                  <div class="col-auto text-caption q-mr-sm">{{ rowLabel(i.barcode) }}</div>
                  <q-btn v-if="scannedSet[i.barcode]" flat dense round size="sm" icon="broken_image" :color="damagedSet[i.barcode] ? 'negative' : 'grey-6'" :data-testid="'receiveDamaged-' + i.barcode" @click="toggleDamaged(i.barcode)">
                    <q-tooltip>{{ damagedSet[i.barcode] ? 'Зняти позначку «пошкоджено»' : 'Позначити пошкодженою' }}</q-tooltip>
                  </q-btn>
                </div>
              </div>
            </div>
            <div class="col-12 col-md-5">
              <div class="section-title">Зайві ({{ extraList.length }})</div>
              <div class="expected-list" data-testid="receiveExtraList">
                <div v-if="!extraList.length" class="text-grey-6 q-pa-xs">—</div>
                <div v-for="b in extraList" :key="b" class="expected-row row items-center no-wrap bg-orange-1">
                  <div class="col mono">{{ b }}</div>
                  <q-btn flat dense round size="sm" icon="close" color="grey-7" @click="removeExtra(b)" />
                </div>
              </div>
            </div>
          </div>

          <div class="text-body2" :class="rcvHasDiscrepancies ? 'text-orange-9' : 'text-positive'" data-testid="receiveSummary">
            Відскановано {{ scannedExpectedCount }} з {{ rcv.items.length }}
            <span v-if="missingList.length"> · відсутні: {{ missingList.length }}</span>
            <span v-if="damagedList.length"> · пошкоджені: {{ damagedList.length }}</span>
            <span v-if="extraList.length"> · зайві: {{ extraList.length }}</span>
          </div>

          <div class="row q-col-gutter-sm">
            <div class="col-12 col-md-5">
              <q-input v-model="rcvTemp" outlined dense label="3. Температура при прийомі, °C *" inputmode="decimal" data-testid="receiveTemperature" :hint="'Режим: ' + regime(rcv)" />
              <q-banner v-if="rcvTempOut" dense rounded class="bg-orange-1 text-deep-orange-9 q-mt-xs"><template v-slot:avatar><q-icon name="warning" color="deep-orange" /></template>Поза режимом — буде зафіксовано порушення холодового ланцюга.</q-banner>
            </div>
            <div class="col-12 col-md-7">
              <q-input v-model="rcvNotes" outlined dense autogrow label="Примітки" data-testid="receiveNotes" />
            </div>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn v-if="rcv" :color="rcvHasDiscrepancies ? 'orange-8' : 'cyan-7'" icon="check" :label="rcvHasDiscrepancies ? 'Прийняти з розбіжностями' : 'Прийняти'" :loading="saving" :disable="parseTemp(rcvTemp) === null || scannedExpectedCount + extraList.length === 0" data-testid="receiveContainerConfirm" @click="doReceive" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Типи тари -->
    <q-dialog v-model="typesOpen">
      <q-card style="width: 820px; max-width: 96vw" data-testid="containerTypesDialog">
        <q-card-section class="row items-center bg-grey-8 text-white q-py-sm">
          <q-icon name="settings" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">Типи транспортної тари</div>
          <q-space /><q-btn flat round dense icon="close" v-close-popup />
        </q-card-section>
        <q-card-section>
          <q-table :data="allTypes" :columns="typeColumns" row-key="id" dense flat hide-pagination :pagination="{ rowsPerPage: 0 }" no-data-label="Типів немає" data-testid="containerTypesTable">
            <template v-slot:body="props">
              <q-tr :props="props" :class="{ 'text-grey-6': !props.row.isActive }" class="cursor-pointer" @click="editType(props.row)">
                <q-td key="code" :props="props"><span class="mono">{{ props.row.code }}</span></q-td>
                <q-td key="name" :props="props">{{ props.row.name }}</q-td>
                <q-td key="capacity" :props="props" class="text-center">{{ props.row.capacity }}</q-td>
                <q-td key="regime" :props="props" class="text-center">{{ props.row.tempMinC !== null && props.row.tempMinC !== undefined ? props.row.tempMinC + '…' + props.row.tempMaxC + ' °C' : 'за налаштуваннями' }}</q-td>
                <q-td key="reusable" :props="props" class="text-center">{{ props.row.isReusable ? 'так' : 'ні' }}</q-td>
                <q-td key="active" :props="props" class="text-center">{{ props.row.isActive ? 'так' : 'ні' }}</q-td>
              </q-tr>
            </template>
          </q-table>
          <div class="text-caption text-grey-7 q-mt-xs">Клік по рядку — редагувати тип.</div>
          <div class="section-title q-mt-md">{{ typeForm.id ? 'Редагування типу' : 'Новий тип' }}</div>
          <div class="row q-col-gutter-sm">
            <div class="col-4 col-md-2"><q-input v-model="typeForm.code" outlined dense label="Код *" data-testid="typeCode" /></div>
            <div class="col-8 col-md-4"><q-input v-model="typeForm.name" outlined dense label="Назва *" data-testid="typeName" /></div>
            <div class="col-4 col-md-2"><q-input v-model="typeForm.capacity" outlined dense label="Місць *" inputmode="numeric" data-testid="typeCapacity" /></div>
            <div class="col-4 col-md-2"><q-input v-model="typeForm.tempMinC" outlined dense label="Мін. температура, °C" inputmode="decimal" data-testid="typeTempMin" /></div>
            <div class="col-4 col-md-2"><q-input v-model="typeForm.tempMaxC" outlined dense label="Макс. температура, °C" inputmode="decimal" data-testid="typeTempMax" /></div>
            <div class="col-6"><q-toggle v-model="typeForm.isReusable" :label="typeForm.isReusable ? 'Багаторазова: так' : 'Багаторазова: ні'" data-testid="typeReusable" /></div>
            <div class="col-6"><q-toggle v-model="typeForm.isActive" :label="typeForm.isActive ? 'Активна: так' : 'Активна: ні'" data-testid="typeActive" /></div>
          </div>
          <div v-if="typeError" class="text-negative q-mt-xs">{{ typeError }}</div>
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat label="Закрити" v-close-popup />
          <q-btn v-if="typeForm.id" flat label="Новий тип" @click="resetTypeForm" />
          <q-btn color="primary" icon="save" label="Зберегти" :loading="saving" :disable="!typeForm.code || !typeForm.name || !typeForm.capacity" data-testid="typeSave" @click="saveType" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <confirm-dialog v-model="returnOpen" title="Повернення тари" message="Контейнер буде позначено як повернений відправнику." ok-label="Повернути" color="blue-grey-6" icon="keyboard_return" @confirm="doReturn" />
    <confirm-dialog v-model="deleteOpen" title="Видалити контейнер" message="Порожній контейнер буде видалено." ok-label="Видалити" color="negative" icon="delete" @confirm="doDelete" />
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import { CONTAINER_STATUS, toOptions } from '../../utils/statuses';
import { formatNumber, parseDecimal } from '../../utils/format';

const EVENT_LABELS = {
  CREATED: 'Контейнер створено', PACKED: 'Пробірку покладено', UNPACKED: 'Пробірку знято', SEALED: 'Запечатано', DISPATCHED: 'Відправлено',
  RECEIVED: 'Прийнято', RETURNED: 'Тару повернуто', LOST: 'Позначено втраченим'
};
const emptyType = () => ({ id: null, code: '', name: '', capacity: '', tempMinC: '', tempMaxC: '', isReusable: true, isActive: true });

export default {
  name: 'TransportContainers',
  mixins: [apiMixin],
  data () {
    return {
      containers: [], types: [], allTypes: [],
      statusFilter: null, search: '',
      statusOptions: toOptions(CONTAINER_STATUS),
      pagination: { rowsPerPage: 15 },
      saving: false,
      createOpen: false, createForm: { typeId: null, originDepartmentId: null, destinationDepartmentId: null }, createError: '',
      cardOpen: false, active: null, events: [],
      scanValue: '', scanError: '', scanBusy: false,
      sealOpen: false, sealTemp: '', sealError: '',
      dispatchOpen: false, courier: { name: '', phone: '' }, dispatchError: '',
      returnOpen: false, deleteOpen: false,
      receiveOpen: false, rcv: null, rcvContainerScan: '', rcvSampleScan: '', rcvError: '', rcvTemp: '', rcvNotes: '',
      scannedSet: {}, damagedSet: {}, extraList: [],
      typesOpen: false, typeForm: emptyType(), typeError: '',
      columns: [
        { name: 'barcode', label: 'Штрихкод', align: 'left' },
        { name: 'type', label: 'Тип тари', align: 'left' },
        { name: 'route', label: 'Маршрут', align: 'left' },
        { name: 'items', label: 'Проби', align: 'center' },
        { name: 'temp', label: 't° запечатування → прийому', align: 'center' },
        { name: 'status', label: 'Статус', align: 'left' },
        { name: 'createdOn', label: 'Створено', align: 'left' }
      ],
      typeColumns: [
        { name: 'code', label: 'Код', align: 'left' },
        { name: 'name', label: 'Назва', align: 'left' },
        { name: 'capacity', label: 'Місць', align: 'center' },
        { name: 'regime', label: 'Режим', align: 'center' },
        { name: 'reusable', label: 'Багаторазова', align: 'center' },
        { name: 'active', label: 'Активна', align: 'center' }
      ]
    };
  },
  computed: {
    depOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    typeOptions () { return this.types.map(t => ({ value: t.id, label: t.name })); },
    destinationOptions () { return this.depOptions.filter(o => o.value !== this.createForm.originDepartmentId); },
    createMissing () {
      const m = [];
      if (!this.createForm.typeId) m.push('тип тари');
      if (!this.createForm.originDepartmentId) m.push('звідки');
      if (!this.createForm.destinationDepartmentId) m.push('куди');
      return m;
    },
    createValid () { return !this.createMissing.length; },
    gridCols () {
      const cap = (this.active && this.active.capacity) || 24;
      if (cap <= 6) return cap;
      if (cap <= 24) return 6;
      if (cap <= 48) return 8;
      return 10;
    },
    slots () {
      if (!this.active) return [];
      const byPos = {};
      (this.active.items || []).forEach(i => { byPos[i.position] = i; });
      return Array.from({ length: this.active.capacity }, (_, k) => ({ position: k + 1, item: byPos[k + 1] || null }));
    },
    sealOut () { return this.outOfRange(this.active, this.sealTemp); },
    rcvTempOut () { return this.outOfRange(this.rcv, this.rcvTemp); },
    scannedExpectedCount () { return this.rcv ? this.rcv.items.filter(i => this.scannedSet[i.barcode]).length : 0; },
    missingList () { return this.rcv ? this.rcv.items.filter(i => !this.scannedSet[i.barcode]).map(i => i.barcode) : []; },
    damagedList () { return this.rcv ? this.rcv.items.filter(i => this.damagedSet[i.barcode]).map(i => i.barcode) : []; },
    rcvHasDiscrepancies () { return this.missingList.length > 0 || this.damagedList.length > 0 || this.extraList.length > 0; }
  },
  created () {
    this.$store.dispatch('dictionaries/load', 'departments');
    this.load();
    this.loadTypes();
  },
  methods: {
    parseTemp: parseDecimal,
    fmtTemp (t) { return t !== null && t !== undefined ? formatNumber(t) + ' °C' : '—'; },
    depName (id) { const d = this.$store.getters['dictionaries/byId']('departments', id); return d ? d.name : (id || '—'); },
    eventLabel (t) { return EVENT_LABELS[t] || t; },
    canDo (action) { return !!this.active && (this.active.allowedActions || []).some(a => a.action === action); },
    regime (c) {
      return c && c.tempMinC !== null && c.tempMinC !== undefined ? `${c.tempMinC}…${c.tempMaxC} °C` : '+2…+8 °C';
    },
    outOfRange (c, text) {
      const t = parseDecimal(text);
      if (t === null || !c) return false;
      const min = c.tempMinC !== null && c.tempMinC !== undefined ? c.tempMinC : 2;
      const max = c.tempMaxC !== null && c.tempMaxC !== undefined ? c.tempMaxC : 8;
      return t < min || t > max;
    },
    async load () {
      const res = await this.callApi(() => this.$api.getContainers({ status: this.statusFilter, search: this.search }));
      if (res !== undefined) this.containers = this.asList(res);
    },
    async loadTypes () {
      try { this.types = this.asList(await this.$api.getContainerTypes(false)); } catch (e) { /* довідник недоступний — список типів порожній */ }
    },
    /** Оновити відкриту картку та рядок у журналі з відповіді API. */
    applyContainer (c) {
      this.active = c;
      const k = this.containers.findIndex(x => x.id === c.id);
      if (k >= 0) this.$set(this.containers, k, c); else this.containers.unshift(c);
    },
    focusScan () { this.$nextTick(() => { const r = this.$refs.scan; if (r) r.focus(); }); },

    // ---------- створення ----------
    openCreate () {
      this.createForm = { typeId: null, originDepartmentId: null, destinationDepartmentId: null };
      this.createError = ''; this.createOpen = true;
      this.loadTypes();
    },
    async doCreate () {
      this.saving = true; this.createError = '';
      try {
        const c = await this.$api.createContainer(this.createForm);
        this.createOpen = false;
        this.notifyOk('Контейнер ' + c.barcode + ' створено');
        this.applyContainer(c);
        this.openCard(c);
      } catch (e) { this.createError = e.userMessage || 'Помилка створення'; } finally { this.saving = false; }
    },

    // ---------- картка та укладка ----------
    openCard (c) {
      this.active = c; this.events = []; this.scanValue = ''; this.scanError = '';
      this.cardOpen = true;
      this.focusScan();
    },
    onCardHide () { this.load(); },
    async loadEvents () {
      if (!this.active) return;
      try { this.events = this.asList(await this.$api.getContainerEvents(this.active.id)); } catch (e) { this.notifyError(e); }
    },
    async doPack () {
      const barcode = (this.scanValue || '').trim();
      if (!barcode || this.scanBusy) return;
      this.scanBusy = true; this.scanError = '';
      try {
        this.applyContainer(await this.$api.packContainer(this.active.id, { barcode }));
        this.scanValue = '';
        this.events = [];
      } catch (e) { this.scanError = e.userMessage || 'Не вдалося покласти пробірку'; } finally {
        this.scanBusy = false;
        this.focusScan();
      }
    },
    slotClass (s) {
      if (!s.item) return 'slot--free';
      if (s.item.status === 'RECEIVED') return 'slot--ok';
      if (s.item.status === 'DAMAGED') return 'slot--bad';
      if (s.item.status === 'MISSING') return 'slot--missing';
      return this.canDo('UNPACK') ? 'slot--packed slot--clickable' : 'slot--packed';
    },
    async onSlotClick (s) {
      if (!s.item || !this.canDo('UNPACK')) return;
      this.scanError = '';
      try {
        this.applyContainer(await this.$api.unpackContainer(this.active.id, s.item.barcode));
        this.events = [];
      } catch (e) { this.scanError = e.userMessage || 'Не вдалося зняти пробірку'; }
      this.focusScan();
    },

    // ---------- запечатування / відправка ----------
    openSeal () { this.sealTemp = ''; this.sealError = ''; this.sealOpen = true; },
    async doSeal () {
      const t = parseDecimal(this.sealTemp);
      if (t === null) return;
      this.saving = true; this.sealError = '';
      try {
        const c = await this.$api.sealContainer(this.active.id, { temperatureSeal: t });
        this.sealOpen = false;
        if (c.isColdChainViolated) this.$q.notify({ type: 'warning', message: 'Контейнер запечатано з порушенням температурного режиму', timeout: 5000 });
        else this.notifyOk('Контейнер запечатано, сформовано маршрутний лист ' + c.manifestNumber);
        this.applyContainer(c);
      } catch (e) { this.sealError = e.userMessage || 'Помилка запечатування'; } finally { this.saving = false; }
    },
    openDispatch () {
      this.courier = { name: '', phone: '' };
      const me = this.$store.getters['context/currentEmployee'];
      if (me && this.$store.getters['context/currentRole'] === 'LOGISTICS_COURIER') this.courier = { name: me.fullName || me.name || '', phone: me.phone || '' }; this.dispatchError = ''; this.dispatchOpen = true;
    },
    async doDispatch () {
      this.saving = true; this.dispatchError = '';
      try {
        const c = await this.$api.dispatchContainer(this.active.id, { courierName: this.courier.name || null, courierPhone: this.courier.phone || null });
        this.dispatchOpen = false;
        this.notifyOk('Контейнер відправлено, проби у дорозі');
        this.applyContainer(c);
      } catch (e) { this.dispatchError = e.userMessage || 'Помилка відправки'; } finally { this.saving = false; }
    },

    // ---------- прийом (розкомплектування) ----------
    resetReceive () {
      this.rcv = null; this.rcvContainerScan = ''; this.rcvSampleScan = ''; this.rcvError = ''; this.rcvTemp = ''; this.rcvNotes = '';
      this.scannedSet = {}; this.damagedSet = {}; this.extraList = [];
    },
    openReceiveByScan () { this.resetReceive(); this.receiveOpen = true; },
    openReceive (c) {
      this.resetReceive();
      this.cardOpen = false;
      this.rcv = c;
      this.receiveOpen = true;
    },
    async findContainer () {
      const barcode = (this.rcvContainerScan || '').trim();
      if (!barcode) return;
      this.rcvError = '';
      try {
        const c = await this.$api.getContainerByBarcode(barcode);
        if (c.status !== 'IN_TRANSIT') { this.rcvError = `Контейнер ${c.barcode} у статусі «${(CONTAINER_STATUS[c.status] || {}).label || c.status}» — приймати можна лише контейнер «У дорозі»`; return; }
        this.rcv = c;
      } catch (e) { this.rcvError = e.userMessage || 'Контейнер не знайдено'; }
    },
    scanSample () {
      const bc = (this.rcvSampleScan || '').trim();
      this.rcvSampleScan = '';
      if (!bc) return;
      this.rcvError = '';
      const expected = this.rcv.items.some(i => i.barcode === bc);
      if (expected) {
        if (this.scannedSet[bc]) { this.rcvError = `Пробірку ${bc} уже відскановано`; return; }
        this.$set(this.scannedSet, bc, true);
      } else if (this.extraList.includes(bc)) {
        this.rcvError = `Штрихкод ${bc} уже відскановано`;
      } else {
        this.extraList.push(bc);
        this.rcvError = `Пробірки ${bc} не було в контейнері — додано до зайвих`;
      }
    },
    toggleDamaged (bc) { this.$set(this.damagedSet, bc, !this.damagedSet[bc]); },
    removeExtra (bc) { this.extraList = this.extraList.filter(x => x !== bc); },
    rowClass (bc) {
      if (this.damagedSet[bc]) return 'bg-red-1';
      if (this.scannedSet[bc]) return 'bg-green-1';
      return '';
    },
    rowLabel (bc) {
      if (this.damagedSet[bc]) return 'пошкоджено';
      if (this.scannedSet[bc]) return 'є';
      return 'очікується';
    },
    async doReceive () {
      const t = parseDecimal(this.rcvTemp);
      if (t === null) return;
      this.saving = true; this.rcvError = '';
      const scanned = Object.keys(this.scannedSet).filter(b => this.scannedSet[b]).concat(this.extraList);
      try {
        const c = await this.$api.receiveContainer(this.rcv.id, { temperatureReceipt: t, scannedBarcodes: scanned, damagedBarcodes: this.damagedList, notes: this.rcvNotes || null });
        this.receiveOpen = false;
        if (c.isColdChainViolated) this.$q.notify({ type: 'warning', message: 'Контейнер прийнято з порушенням холодового ланцюга', timeout: 5000, position: 'bottom' });
        else if (c.hasDiscrepancies) this.$q.notify({ type: 'warning', message: 'Контейнер прийнято з розбіжностями', timeout: 5000 });
        else this.notifyOk('Контейнер прийнято, проби прийняті в лабораторії');
        this.applyContainer(c);
        this.openCard(c);
      } catch (e) { this.rcvError = e.userMessage || 'Помилка прийому'; } finally { this.saving = false; }
    },

    // ---------- повернення тари, видалення ----------
    async doReturn () {
      try {
        this.applyContainer(await this.$api.returnContainer(this.active.id, {}));
        this.notifyOk('Тару повернуто');
        this.events = [];
      } catch (e) { this.notifyError(e); }
    },
    async doDelete () {
      try {
        await this.$api.deleteContainer(this.active.id);
        this.cardOpen = false;
        this.notifyOk('Контейнер видалено');
        this.load();
      } catch (e) { this.notifyError(e); }
    },

    // ---------- довідник типів ----------
    async openTypes () {
      this.resetTypeForm();
      this.typesOpen = true;
      await this.reloadTypes();
    },
    async reloadTypes () {
      try { this.allTypes = this.asList(await this.$api.getContainerTypes(true)); } catch (e) { this.notifyError(e); }
      this.loadTypes();
    },
    resetTypeForm () { this.typeForm = emptyType(); this.typeError = ''; },
    editType (t) {
      this.typeForm = {
        id: t.id, code: t.code, name: t.name, capacity: String(t.capacity),
        tempMinC: t.tempMinC === null || t.tempMinC === undefined ? '' : String(t.tempMinC),
        tempMaxC: t.tempMaxC === null || t.tempMaxC === undefined ? '' : String(t.tempMaxC),
        isReusable: t.isReusable, isActive: t.isActive
      };
      this.typeError = '';
    },
    async saveType () {
      this.saving = true; this.typeError = '';
      const f = this.typeForm;
      const body = { code: f.code, name: f.name, capacity: parseInt(f.capacity, 10), tempMinC: parseDecimal(f.tempMinC), tempMaxC: parseDecimal(f.tempMaxC), isReusable: f.isReusable, isActive: f.isActive };
      try {
        if (f.id) await this.$api.updateContainerType(f.id, body); else await this.$api.createContainerType(body);
        this.notifyOk(f.id ? 'Тип тари оновлено' : 'Тип тари створено');
        this.resetTypeForm();
        await this.reloadTypes();
      } catch (e) { this.typeError = e.userMessage || 'Помилка збереження'; } finally { this.saving = false; }
    }
  }
};
</script>

<style scoped>
.slots-grid { display: grid; gap: 6px; }
.slot { border: 1px solid #90a4ae; border-radius: 4px; padding: 4px 6px; min-height: 46px; background: #fff; }
.slot-no { font-size: 10px; color: #78909c; }
.slot-bc { font-size: 12px; font-weight: 600; min-height: 16px; }
.slot--free { background: #eceff1; border-style: dashed; border-color: #78909c; }
.slot--packed { background: #bbdefb; border-color: #1e88e5; }
.slot--clickable { cursor: pointer; }
.slot--clickable:hover { background: #bbdefb; }
.slot--ok { background: #e8f5e9; border-color: #81c784; }
.slot--bad { background: #ffebee; border-color: #e57373; }
.slot--missing { background: #fff3e0; border-color: #ffb74d; }
.expected-list { max-height: 280px; overflow-y: auto; border: 1px solid #eceff1; border-radius: 4px; }
.expected-row { padding: 2px 8px; border-bottom: 1px solid #f5f5f5; min-height: 32px; }
</style>
