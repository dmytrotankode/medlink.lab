<template>
  <div class="dictionary-page">
    <page-header :title="schema.title" :icon="schema.icon" :subtitle="schema.description" :breadcrumbs="[{ label: 'Довідники', to: { name: 'lab-dictionaries' } }, { label: schema.title }]">
      <q-btn-dropdown outline dense color="primary" icon="menu_book" label="Інший довідник" no-caps>
        <q-list dense>
          <q-item v-for="d in list" :key="d.name" clickable v-close-popup :active="d.name === name" :to="{ name: 'lab-dictionary', params: { name: d.name } }">
            <q-item-section avatar><q-icon :name="d.icon" size="16px" /></q-item-section>
            <q-item-section>{{ d.title }}</q-item-section>
          </q-item>
        </q-list>
      </q-btn-dropdown>
    </page-header>
    <dictionary-crud-table :key="name" :name="name" />
  </div>
</template>

<script>
import DictionaryCrudTable from './DictionaryCrudTable.vue';
import { DICTIONARIES, DICTIONARY_LIST } from './dictionarySchemas';

export default {
  name: 'DictionaryPage',
  components: { DictionaryCrudTable },
  props: { name: { type: String, required: true } },
  computed: {
    schema () { return DICTIONARIES[this.name] || { title: `Довідник ${this.name}`, icon: 'list', description: 'Невідомий довідник — відображення у загальному вигляді' }; },
    list () { return DICTIONARY_LIST; }
  }
};
</script>
