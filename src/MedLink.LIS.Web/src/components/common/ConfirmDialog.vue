<template>
  <q-dialog :value="value" @input="$emit('input', $event)">
    <q-card style="min-width: 420px; max-width: 95vw">
      <q-card-section class="row items-center q-pb-none">
        <q-icon :name="icon" :color="color" size="28px" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">{{ title }}</div>
      </q-card-section>
      <q-card-section>
        <div class="text-body2">{{ message }}</div>
        <q-select
          v-if="withReason && reasonOptions.length"
          v-model="reason"
          outlined
          dense
          class="q-mt-sm"
          :label="reasonLabel"
          :options="reasonOptions"
          use-input
          hide-selected
          fill-input
          input-debounce="0"
          new-value-mode="add-unique"
          @new-value="(v, done) => done(v, 'add-unique')"
          @input-value="v => (reason = v)"
          data-testid="reasonSelect"
        />
        <q-input
          v-else-if="withReason"
          v-model="reason"
          type="textarea"
          outlined
          dense
          autogrow
          class="q-mt-sm"
          :label="reasonLabel"
          :rules="reasonRequired ? [v => !!(v && v.trim()) || 'Обов’язкове поле'] : []"
        />
        <slot />
      </q-card-section>
      <q-card-actions align="right" class="bg-grey-1">
        <q-btn flat label="Скасувати" :data-autofocus="!withReason" @click="$emit('input', false)" />
        <q-btn :color="color" :label="okLabel" :disable="reasonRequired && !(reason && reason.trim())" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
export default {
  name: 'ConfirmDialog',
  props: {
    value: Boolean,
    title: { type: String, default: 'Підтвердження' },
    message: { type: String, default: 'Ви впевнені?' },
    okLabel: { type: String, default: 'Підтвердити' },
    color: { type: String, default: 'primary' },
    icon: { type: String, default: 'help_outline' },
    withReason: { type: Boolean, default: false },
    reasonRequired: { type: Boolean, default: false },
    reasonLabel: { type: String, default: 'Причина' },
    /** Назви причин із довідника для випадного списку (порожньо — вільний текст) */
    reasonOptions: { type: Array, default: () => [] }
  },
  data () {
    return { reason: '' };
  },
  watch: {
    value (v) { if (v) this.reason = ''; }
  },
  methods: {
    confirm () {
      this.$emit('confirm', this.reason);
      this.$emit('input', false);
    }
  }
};
</script>
