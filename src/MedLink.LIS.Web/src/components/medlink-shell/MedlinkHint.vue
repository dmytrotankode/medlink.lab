<!--
  Ненав'язливий підпис «Ця частина належить МедЛінк»: показує, що саме в реальній системі має надавати МІС.
  Вмикається конфігом (LIS_SHELL_HINTS / auth/config → shellHints) і особистим перемикачем в умовній оболонці.
  Використання: <medlink-hint text="Вибір пацієнта — з МІС" detail="..." /> або inline-варіант біля заголовка.
-->
<template>
  <span v-if="visible" class="medlink-hint" :class="{ 'medlink-hint--label': label }" data-testid="medlinkHint" tabindex="0">
    <q-icon name="extension" size="13px" />
    <span v-if="label" class="medlink-hint__text">{{ text }}</span>
    <q-tooltip max-width="320px" :delay="200">
      <div class="text-weight-bold">Ця частина належить МедЛінк — вбудувати у реальний фронт</div>
      <div>{{ text }}</div>
      <div v-if="detail" class="q-mt-xs">{{ detail }}</div>
    </q-tooltip>
  </span>
</template>

<script>
import { mapGetters } from 'vuex';

export default {
  name: 'MedlinkHint',
  props: {
    text: { type: String, required: true }, // що надає МІС, напр. «Вибір пацієнта — з МІС»
    detail: { type: String, default: '' },
    label: { type: Boolean, default: false } // true — з видимим підписом, інакше лише значок
  },
  computed: { ...mapGetters('context', { visible: 'shellHints' }) }
};
</script>

<style lang="stylus">
.medlink-hint
  display inline-flex
  align-items center
  gap 3px
  vertical-align middle
  padding 0 4px
  margin 0 2px
  border 1px dashed #9aa5b1
  border-radius 9px
  color #607080
  font-size 10.5px
  line-height 16px
  cursor help
  background rgba(255, 255, 255, .6)
  &:hover, &:focus
    color #1565c0
    border-color #1565c0
    outline none
</style>
