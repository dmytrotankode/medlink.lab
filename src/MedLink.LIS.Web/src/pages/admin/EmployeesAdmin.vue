<template>
  <div class="employees-page" data-testid="employeesPage">
    <page-header title="Співробітники" icon="fas fa-users" subtitle="org_employee: ПІБ, посада, лабораторна роль (довідкова — не обмежує доступ), відділення. Перемикач «Працюю як…» використовує цей список." :breadcrumbs="[{ label: 'Адміністрування' }, { label: 'Співробітники' }]" />
    <q-banner dense rounded class="bg-blue-1 text-grey-9 q-mb-sm"><q-icon name="info" color="primary" /> Ролі: {{ roles }}. У бойовому evomis ролі мапляться на claims IdentityServer; тут — лише для відображення дозволених дій та аудиту.</q-banner>
    <dictionary-crud-table name="employees" :allow-import="false" @loaded="$store.dispatch('context/loadEmployees')" />
  </div>
</template>

<script>
import DictionaryCrudTable from '../dictionaries/DictionaryCrudTable.vue';
import { ROLE } from '../../utils/statuses';

export default {
  name: 'EmployeesAdmin',
  components: { DictionaryCrudTable },
  computed: { roles () { return Object.values(ROLE).join(', '); } }
};
</script>
