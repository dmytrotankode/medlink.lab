<template>
  <div class="portal-notifications" data-testid="portalNotifications">
    <div class="row items-center q-mb-sm">
      <q-btn flat dense icon="arrow_back" label="Назад" no-caps :to="{ name: 'portal-orders', params: { patientId } }" />
      <q-space />
      <q-btn flat dense color="primary" icon="refresh" :loading="loading" @click="load" />
    </div>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />
    <div class="medlink-card">
      <div class="medlink-card__title"><span><q-icon name="notifications" class="q-mr-xs" />Сповіщення</span><span class="text-caption text-grey-6">SMS / E-mail / Push / Viber</span></div>
      <q-list v-if="items.length" separator>
        <q-item v-for="n in items" :key="n.id">
          <q-item-section avatar><q-avatar :color="channelColor(n.channel)" text-color="white" :icon="channelIcon(n.channel)" /></q-item-section>
          <q-item-section>
            <q-item-label>{{ n.payload && (n.payload.text || n.payload.message || n.payload.subject) || n.text || n.message || JSON.stringify(n.payload) }}</q-item-label>
            <q-item-label caption>{{ n.sentAt | datetime }} · {{ n.channel }} · <q-badge :color="n.status === 'SENT' || n.status === 'DELIVERED' ? 'positive' : (n.status === 'FAILED' ? 'negative' : 'grey-6')" :label="statusLabel(n.status)" /></q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
      <empty-state v-else title="Сповіщень поки немає" icon="notifications_none" hint="Ви отримаєте повідомлення одразу після видачі результатів" />
    </div>
  </div>
</template>

<script>
import apiMixin from '../../mixins/apiMixin';

export default {
  name: 'PortalNotifications',
  mixins: [apiMixin],
  props: { patientId: { type: String, required: true } },
  data () { return { items: [] }; },
  created () { this.load(); },
  methods: {
    channelIcon (c) { return { SMS: 'sms', EMAIL: 'email', PUSH: 'notifications_active', VIBER: 'chat' }[c] || 'notifications'; },
    channelColor (c) { return { SMS: 'teal-6', EMAIL: 'primary', PUSH: 'deep-orange-6', VIBER: 'purple-6' }[c] || 'grey-6'; },
    statusLabel (s) { return { SENT: 'надіслано', DELIVERED: 'доставлено', PENDING: 'у черзі', FAILED: 'помилка', QUEUED: 'у черзі' }[s] || s; },
    async load () { const res = await this.callApi(() => this.$api.portalNotifications(this.patientId)); if (res !== undefined) this.items = this.asList(res).sort((a, b) => new Date(b.sentAt) - new Date(a.sentAt)); }
  }
};
</script>
