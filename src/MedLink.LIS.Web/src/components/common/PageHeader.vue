<template>
  <div class="page-header">
    <div>
      <q-breadcrumbs v-if="breadcrumbs.length" class="text-grey-7 q-mb-xs" active-color="primary" style="font-size: 12px">
        <q-breadcrumbs-el icon="home" :to="{ name: 'lab-dashboard' }" />
        <q-breadcrumbs-el label="Лабораторія" :to="{ name: 'lab-dashboard' }" />
        <q-breadcrumbs-el v-for="(b, i) in breadcrumbs" :key="i" :label="b.label" :to="b.to" />
      </q-breadcrumbs>
      <div class="page-header__title">
        <q-icon v-if="icon" :name="icon" color="primary" size="26px" />
        <span>{{ title }}</span>
        <slot name="title-after" />
      </div>
      <div v-if="subtitle" class="page-header__subtitle">{{ subtitle }}</div>
    </div>
    <div class="row items-center q-gutter-sm no-wrap page-header__actions">
      <slot />
    </div>
  </div>
</template>

<script>
export default {
  name: 'PageHeader',
  props: {
    title: { type: String, required: true },
    subtitle: { type: String, default: '' },
    icon: { type: String, default: '' },
    breadcrumbs: { type: Array, default: () => [] }
  }
};
</script>

<style scoped>
.page-header__actions { flex-wrap: wrap; justify-content: flex-end; }
</style>
