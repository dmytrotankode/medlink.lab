<!--
  «Сповіщення»: черга сповіщень пацієнтам (фільтри, ручний повтор), події для МІС (доставка), стан каналів.
  Статуси чесні: «Не налаштовано» означає, що повідомлення не відправлялось; секрети підключення тут не показуються.
-->
<template>
  <div data-testid="notificationsPage">
    <page-header title="Сповіщення" icon="fas fa-bell" :breadcrumbs="[{ label: 'Сповіщення' }]">
      <q-btn v-if="canChannels" flat dense color="primary" icon="play_arrow" label="Обробити зараз" :loading="running" class="q-mr-sm" data-testid="notifyRunNow" @click="runNow">
        <q-tooltip>Запустити відправку черги зараз</q-tooltip>
      </q-btn>
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" data-testid="notifyRefresh" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

    <q-tabs v-model="tab" dense align="left" class="text-primary q-mb-sm" active-color="primary" indicator-color="primary" data-testid="notifyTabs">
      <q-tab name="patients" label="Пацієнтам" data-testid="notifyTabPatients" />
      <q-tab name="mis" label="Події для МІС" data-testid="notifyTabMis" />
      <q-tab name="channels" label="Канали" data-testid="notifyTabChannels" />
    </q-tabs>

    <!-- ===== Пацієнтам ===== -->
    <div v-show="tab === 'patients'">
      <div class="row q-col-gutter-sm q-mb-sm" data-testid="notifySummary">
        <div v-for="s in summary" :key="s.status" class="col-6 col-md-2">
          <q-card flat bordered class="cursor-pointer" @click="filters.status = filters.status === s.status ? null : s.status">
            <q-card-section class="q-pa-sm text-center">
              <div class="text-h6" :data-testid="`notifyCount-${s.status}`">{{ s.count }}</div>
              <div class="text-caption text-grey-7">{{ s.label }}</div>
            </q-card-section>
          </q-card>
        </div>
      </div>
      <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center" data-testid="notifyFilters">
        <div class="col-12 col-md-3"><q-select v-model="filters.status" dense outlined clearable emit-value map-options :options="statusOptions" label="Статус" data-testid="notifyStatusFilter" /></div>
        <div class="col-12 col-md-3"><q-select v-model="filters.channel" dense outlined clearable emit-value map-options :options="channelOptions" label="Канал" data-testid="notifyChannelFilter" /></div>
        <div class="col-6 col-md-2"><q-input v-model="filters.from" dense outlined type="date" stack-label label="Дата з" data-testid="notifyDateFrom" /></div>
        <div class="col-6 col-md-2"><q-input v-model="filters.to" dense outlined type="date" stack-label label="Дата по" data-testid="notifyDateTo" /></div>
        <div class="col-12 col-md-2"><q-input v-model="filters.search" dense outlined clearable debounce="400" label="Пацієнт або замовлення" data-testid="notifySearch" /></div>
      </div>
      <div class="medlink-card">
        <q-table :data="rows" :columns="columns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100]" no-data-label="Сповіщень немає" data-testid="notifyTable">
          <template v-slot:body-cell-order="props">
            <q-td :props="props">
              <router-link v-if="props.row.orderId" :to="{ name: 'lab-order-card', params: { id: props.row.orderId } }" class="text-primary">{{ props.row.orderNumber || 'Відкрити' }}</router-link>
            </q-td>
          </template>
          <template v-slot:body-cell-status="props">
            <q-td :props="props">
              <q-badge :color="color(props.row.status)" :label="props.row.statusLabel" :data-testid="`notifyStatus-${props.row.id}`" />
              <div v-if="props.row.reasonLabel" class="text-caption text-grey-7">{{ props.row.reasonLabel }}</div>
            </q-td>
          </template>
          <template v-slot:body-cell-error="props">
            <q-td :props="props" class="text-negative text-caption" style="max-width: 320px; white-space: normal">{{ props.row.status === 'FAILED' || props.row.status === 'QUEUED' ? props.row.lastError : '' }}</q-td>
          </template>
          <template v-slot:body-cell-actions="props">
            <q-td :props="props" class="text-right">
              <q-btn v-if="props.row.canRetry && canRetry" flat dense size="sm" color="primary" icon="replay" label="Повторити" :data-testid="`notifyRetry-${props.row.id}`" @click="retry(props.row)" />
            </q-td>
          </template>
        </q-table>
      </div>
    </div>

    <!-- ===== Події для МІС ===== -->
    <div v-show="tab === 'mis'">
      <q-banner v-if="mis && !mis.configured && !misDelivered" dense rounded class="bg-orange-1 text-orange-10 q-mb-sm" data-testid="misNotConfigured">
        <template v-slot:avatar><q-icon name="info" color="orange" /></template>
        Доставку подій у МІС не налаштовано: події накопичуються зі статусом «Не налаштовано» і підуть автоматично після підключення.
      </q-banner>
      <q-banner v-else-if="mis && mis.configured" dense rounded class="bg-green-1 text-green-10 q-mb-sm" data-testid="misReady">
        Доставка працює{{ mis.host ? ` · ${mis.host}` : '' }}{{ mis.signed ? ' · з підписом' : '' }}.
      </q-banner>
      <div class="medlink-card q-pa-sm q-mb-sm row q-col-gutter-sm items-center">
        <div class="col-12 col-md-3"><q-select v-model="misFilters.status" dense outlined clearable emit-value map-options :options="misStatusOptions" label="Статус" data-testid="misStatusFilter" /></div>
        <div class="col-6 col-md-2"><q-input v-model="misFilters.from" dense outlined type="date" stack-label label="Дата з" /></div>
        <div class="col-6 col-md-2"><q-input v-model="misFilters.to" dense outlined type="date" stack-label label="Дата по" /></div>
        <div class="col-12 col-md-5"><q-input v-model="misFilters.search" dense outlined clearable debounce="400" label="Номер замовлення або направлення" data-testid="misSearch" /></div>
      </div>
      <div class="medlink-card">
        <q-table :data="misRows" :columns="misColumns" row-key="id" dense flat :loading="loading" :pagination.sync="pagination" :rows-per-page-options="[25, 50, 100]" no-data-label="Подій немає" data-testid="misTable">
          <template v-slot:body-cell-status="props">
            <q-td :props="props"><q-badge :color="misColor(props.row.status)" :label="props.row.statusLabel" :data-testid="`misStatus-${props.row.id}`" /></q-td>
          </template>
          <template v-slot:body-cell-error="props">
            <q-td :props="props" class="text-negative text-caption" style="max-width: 320px; white-space: normal">{{ props.row.status === 'FAILED' || props.row.status === 'QUEUED' ? props.row.lastError : '' }}</q-td>
          </template>
          <template v-slot:body-cell-actions="props">
            <q-td :props="props" class="text-right">
              <q-btn v-if="props.row.canRetry && canRetry" flat dense size="sm" color="primary" icon="replay" label="Повторити" :data-testid="`misRetry-${props.row.id}`" @click="misRetry(props.row)" />
            </q-td>
          </template>
        </q-table>
      </div>
    </div>

    <!-- ===== Канали ===== -->
    <div v-show="tab === 'channels'">
      <div class="medlink-card q-pa-md" data-testid="notifyChannels">
        <div class="text-caption text-grey-7 q-mb-sm">Параметри підключення (адреси шлюзів, логіни, ключі) задає адміністратор сервера в конфігурації; тут вони не відображаються. Вимкнений канал не відправляє, а записи чекають зі статусом «Не налаштовано».</div>
        <q-list separator>
          <q-item v-for="c in channels" :key="c.channel" :data-testid="`channel-${c.channel}`">
            <q-item-section>
              <q-item-label>{{ c.label }}</q-item-label>
              <q-item-label caption>
                <span :class="c.state === 'READY' ? 'text-positive' : 'text-grey-7'">{{ c.stateLabel }}</span>
                <span v-if="c.waiting"> · очікує налаштування: {{ c.waiting }}</span>
              </q-item-label>
              <q-item-label v-if="c.hint" caption class="text-grey-6">{{ c.hint }}</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-toggle :value="c.enabled" :disable="!canChannels || !c.configured" color="primary" :data-testid="`channelToggle-${c.channel}`" @input="toggleChannel(c, $event)">
                <q-tooltip v-if="!c.configured">Спочатку канал треба налаштувати на сервері</q-tooltip>
              </q-toggle>
            </q-item-section>
          </q-item>
        </q-list>
      </div>
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';
import roleMixin from '../../mixins/roleMixin';
import { ACTION_ROLES } from '../../utils/menuAccess';

const COLORS = { QUEUED: 'primary', SENDING: 'primary', SENT: 'positive', FAILED: 'negative', SKIPPED: 'grey-6', NOT_CONFIGURED: 'orange-8' };
const MIS_COLORS = { QUEUED: 'primary', SENDING: 'primary', DELIVERED: 'positive', FAILED: 'negative', NOT_CONFIGURED: 'orange-8' };

export default {
  name: 'NotificationsPage',
  mixins: [apiMixin, roleMixin],
  data () {
    return {
      tab: 'patients', running: false, pagination: { rowsPerPage: 25 },
      rows: [], summary: [], channels: [], misRows: [], mis: null,
      filters: { status: null, channel: null, from: '', to: '', search: '' },
      misFilters: { status: null, from: '', to: '', search: '' },
      statusOptions: [
        { value: 'QUEUED', label: 'У черзі' }, { value: 'SENDING', label: 'Відправляється' }, { value: 'SENT', label: 'Надіслано' },
        { value: 'FAILED', label: 'Помилка' }, { value: 'SKIPPED', label: 'Пропущено' }, { value: 'NOT_CONFIGURED', label: 'Не налаштовано' }
      ],
      misStatusOptions: [
        { value: 'QUEUED', label: 'У черзі' }, { value: 'SENDING', label: 'Відправляється' }, { value: 'DELIVERED', label: 'Доставлено' },
        { value: 'FAILED', label: 'Помилка' }, { value: 'NOT_CONFIGURED', label: 'Не налаштовано' }
      ],
      channelOptions: [{ value: 'EMAIL', label: 'Електронна пошта' }, { value: 'SMS', label: 'SMS' }, { value: 'VIBER', label: 'Viber' }, { value: 'TELEGRAM', label: 'Telegram' }],
      columns: [
        { name: 'created', label: 'Створено', field: r => this.$options.filters.datetime(r.createdOn), align: 'left', sortable: false },
        { name: 'patient', label: 'Пацієнт', field: 'patientName', align: 'left' },
        { name: 'order', label: 'Замовлення', field: 'orderNumber', align: 'left' },
        { name: 'channel', label: 'Канал', field: 'channelLabel', align: 'left' },
        { name: 'recipient', label: 'Одержувач', field: 'recipientMasked', align: 'left', classes: 'mono' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'attempts', label: 'Спроби', field: r => r.attempts || '', align: 'right' },
        { name: 'next', label: 'Наступна спроба', field: r => (r.status === 'QUEUED' && r.nextAttemptAt ? this.$options.filters.datetime(r.nextAttemptAt) : ''), align: 'left' },
        { name: 'error', label: 'Остання помилка', field: 'lastError', align: 'left' },
        { name: 'actions', label: '', field: 'id' }
      ],
      misColumns: [
        { name: 'at', label: 'Створено', field: r => this.$options.filters.datetime(r.at), align: 'left' },
        { name: 'event', label: 'Подія', field: 'eventLabel', align: 'left' },
        { name: 'order', label: 'Замовлення', field: 'orderNumber', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'left' },
        { name: 'attempts', label: 'Спроби', field: r => r.attempts || '', align: 'right' },
        { name: 'delivered', label: 'Доставлено', field: r => (r.deliveredAt ? this.$options.filters.datetime(r.deliveredAt) : ''), align: 'left' },
        { name: 'next', label: 'Наступна спроба', field: r => (r.status === 'QUEUED' && r.nextAttemptAt ? this.$options.filters.datetime(r.nextAttemptAt) : ''), align: 'left' },
        { name: 'error', label: 'Остання помилка', field: 'lastError', align: 'left' },
        { name: 'actions', label: '', field: 'id' }
      ]
    };
  },
  computed: {
    /** Є вже доставлені події — банер «доставку не налаштовано» тоді суперечить даним */
    misDelivered () { return !!(this.mis && (this.mis.byStatus || []).some(s => s.status === 'DELIVERED' && s.count > 0)); },
    canRetry () { return this.canDo(...ACTION_ROLES.notificationRetry); },
    canChannels () { return this.canDo(...ACTION_ROLES.notificationChannels); }
  },
  watch: {
    filters: { deep: true, handler () { this.loadPatients(); } },
    misFilters: { deep: true, handler () { this.loadMis(); } }
  },
  created () { this.load(); },
  methods: {
    color (s) { return COLORS[s] || 'grey-6'; },
    misColor (s) { return MIS_COLORS[s] || 'grey-6'; },
    async load () {
      await Promise.all([this.loadPatients(), this.loadMis(), this.loadChannels()]);
    },
    async loadPatients () {
      const f = this.filters;
      const [list, sum] = await Promise.all([
        this.callApi(() => this.$api.notifications({ status: f.status, channel: f.channel, from: f.from, to: f.to, search: f.search, pageSize: 200 })),
        this.callApi(() => this.$api.notificationsSummary())
      ]);
      if (list) this.rows = this.asList(list);
      if (sum) this.summary = sum.byStatus || [];
    },
    async loadMis () {
      const f = this.misFilters;
      const [list, st] = await Promise.all([
        this.callApi(() => this.$api.misEvents({ status: f.status, from: f.from, to: f.to, search: f.search, pageSize: 200 })),
        this.callApi(() => this.$api.misEventsStatus())
      ]);
      if (list) this.misRows = this.asList(list);
      if (st) this.mis = st;
    },
    async loadChannels () {
      const r = await this.callApi(() => this.$api.notificationChannels());
      if (r) this.channels = r;
    },
    async retry (row) {
      try {
        await this.$api.notificationRetry(row.id);
        this.notifyOk('Сповіщення повернуто в чергу');
      } catch (e) { this.notifyError(e); }
      this.loadPatients();
    },
    async misRetry (row) {
      try {
        await this.$api.misEventRetry(row.id);
        this.notifyOk('Подію повернуто в чергу');
      } catch (e) { this.notifyError(e); }
      this.loadMis();
    },
    async toggleChannel (c, enabled) {
      try {
        await this.$api.notificationChannelSet(c.channel, enabled);
        this.notifyOk(enabled ? 'Канал увімкнено' : 'Канал вимкнено');
      } catch (e) { this.notifyError(e); }
      this.loadChannels();
    },
    async runNow () {
      this.running = true;
      try {
        await this.$api.notificationsRun();
      } catch (e) { this.notifyError(e); } finally {
        this.running = false;
        this.load();
      }
    }
  }
};
</script>
