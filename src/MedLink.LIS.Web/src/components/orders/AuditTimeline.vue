<template>
  <div class="audit-timeline">
    <div v-if="loading" class="text-center q-pa-md"><q-spinner color="primary" /></div>
    <empty-state v-else-if="!events.length" title="Подій аудиту немає" icon="history" hint="Журнал /audit порожній для цієї сутності" />
    <q-timeline v-else color="primary" layout="dense" class="q-px-sm">
      <q-timeline-entry
        v-for="(e, i) in events"
        :key="e.id || i"
        :title="actionLabel(e.action)"
        :subtitle="`${formatDateTime(e.at)} · ${e.userName || e.userId || 'система'}`"
        :icon="iconFor(e.action)"
        :color="colorFor(e.action)"
      >
        <div class="text-caption text-grey-8">
          {{ e.entity }} <span class="mono">{{ shortId(e.entityId) }}</span>
          <span v-if="e.ip"> · {{ e.ip }}</span>
          <q-btn v-if="e.before || e.after" flat dense size="sm" color="primary" :label="open === i ? 'сховати зміни' : 'зміни'" @click="open = open === i ? null : i" />
        </div>
        <json-diff-viewer v-if="open === i" :before="e.before" :after="e.after" class="q-mt-xs" />
      </q-timeline-entry>
    </q-timeline>
  </div>
</template>

<script>
import JsonDiffViewer from '../common/JsonDiffViewer.vue';
import { formatDateTime } from '../../utils/format';

const LABELS = {
  CREATE: 'Створено', UPDATE: 'Оновлено', DELETE: 'Видалено', CANCEL: 'Скасовано', RELEASE: 'Видано',
  COLLECT: 'Забір проби', RECEIVE: 'Прийом проби', REJECT: 'Відхилено', VERIFY: 'Верифіковано', AUTO_VERIFY: 'Автоверифікація',
  RESULT: 'Результат', RERUN: 'Повтор', REOPEN: 'Повернуто в роботу', STATUS_CHANGE: 'Зміна статусу', PANIC_CALL: 'Панічний дзвінок',
  REFLEX: 'Reflex-тест', DELTA: 'Delta-check', PRINT: 'Друк'
};

export default {
  name: 'AuditTimeline',
  components: { JsonDiffViewer },
  props: {
    events: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false }
  },
  data () { return { open: null }; },
  methods: {
    formatDateTime,
    actionLabel (a) { return LABELS[a] || LABELS[(a || '').toUpperCase()] || a || 'Подія'; },
    iconFor (a) {
      const k = (a || '').toUpperCase();
      if (k.includes('CREATE')) return 'add_circle';
      if (k.includes('DELETE') || k.includes('CANCEL')) return 'cancel';
      if (k.includes('VERIFY')) return 'how_to_reg';
      if (k.includes('RELEASE')) return 'verified';
      if (k.includes('REJECT')) return 'block';
      if (k.includes('COLLECT')) return 'colorize';
      if (k.includes('RECEIVE')) return 'inbox';
      if (k.includes('PANIC')) return 'call';
      return 'edit';
    },
    colorFor (a) {
      const k = (a || '').toUpperCase();
      if (k.includes('DELETE') || k.includes('REJECT') || k.includes('PANIC')) return 'negative';
      if (k.includes('CANCEL')) return 'grey-7';
      if (k.includes('VERIFY') || k.includes('RELEASE')) return 'positive';
      return 'primary';
    },
    shortId (id) { return id ? String(id).slice(0, 8) : ''; }
  }
};
</script>
