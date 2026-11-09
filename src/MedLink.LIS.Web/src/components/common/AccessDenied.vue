<!--
  Екран «Недостатньо прав»: показується замість сторінки, якщо маршрут недоступний ролі поточного співробітника
  (таблиця маршрутів і ролей — utils/menuAccess.js). Прямий перехід за URL не ламає екран.
-->
<template>
  <div class="column items-center q-pa-xl" data-testid="accessDenied">
    <q-icon name="lock" size="56px" color="grey-5" />
    <div class="text-h6 text-grey-8 q-mt-md">{{ text.title }}</div>
    <div class="text-body2 text-grey-7 q-mt-sm text-center" style="max-width: 480px">{{ text.message }}</div>
    <div v-if="roleName" class="text-caption text-grey-6 q-mt-sm">Ваша роль: {{ roleName }}</div>
    <q-btn unelevated color="primary" icon="home" :label="text.button" class="q-mt-md" :to="{ name: 'lab-dashboard' }" />
  </div>
</template>

<script>
import roleMixin from '../../mixins/roleMixin';
import { ACCESS_DENIED_TEXT } from '../../utils/menuAccess';

export default {
  name: 'AccessDenied',
  mixins: [roleMixin],
  computed: {
    text () { return ACCESS_DENIED_TEXT; },
    roleName () { return this.currentLabRole ? this.roleLabel(this.currentLabRole) : ''; }
  }
};
</script>
