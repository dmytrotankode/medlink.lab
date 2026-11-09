<template>
  <div class="employees-page" data-testid="employeesPage">
    <page-header title="Співробітники" icon="fas fa-users" subtitle="Список співробітників для перемикача «Працюю як…»: ПІБ, посада, лабораторна роль, відділення" :breadcrumbs="[{ label: 'Адміністрування' }, { label: 'Співробітники' }]" />
    <q-banner dense rounded class="bg-blue-1 text-grey-9 q-mb-sm"><q-icon name="info" color="primary" /> Ролі: {{ roles }}. Роль визначає, які розділи меню й кнопки бачить співробітник, і записується в журнал аудиту.</q-banner>
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
