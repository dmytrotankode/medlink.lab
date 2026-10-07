<template>
  <div class="empty-state text-center q-pa-lg">
    <q-icon :name="icon" size="44px" color="grey-5" />
    <div class="text-subtitle1 text-grey-7 q-mt-sm">{{ title }}</div>
    <div v-if="hint" class="text-caption text-grey-6">{{ hint }}</div>
    <div class="q-mt-sm"><slot /></div>
  </div>
</template>

<script>
export default {
  name: 'EmptyState',
  props: {
    title: { type: String, default: 'Немає даних' },
    hint: { type: String, default: '' },
    icon: { type: String, default: 'inbox' }
  }
};
</script>
