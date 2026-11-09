<template>
  <div v-if="!known" class="dictionary-page column items-center q-pa-xl" data-testid="dictNotFound">
    <q-icon name="menu_book" size="56px" color="grey-5" />
    <div class="text-h6 text-grey-8 q-mt-md">Довідник не знайдено</div>
    <div class="text-body2 text-grey-7 q-mt-sm">Такого довідника немає. Оберіть довідник зі списку.</div>
    <q-btn unelevated color="primary" icon="menu_book" label="До списку довідників" class="q-mt-md" :to="{ name: 'lab-dictionaries' }" />
  </div>
  <div v-else class="dictionary-page">
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
    known () { return !!DICTIONARIES[this.name]; },
    schema () { return DICTIONARIES[this.name] || { title: 'Довідник', icon: 'list', description: '' }; },
    list () { return DICTIONARY_LIST; }
  }
};
</script>
