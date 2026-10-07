<template>
  <div class="dictionaries-home">
    <page-header title="Довідники лабораторії" icon="fas fa-book-medical" subtitle="Номенклатура ЛІС: біоматеріали, пробірки, методики, показники, послуги, моделі аналізаторів, reflex-правила, мікробіологія, організаційна структура" :breadcrumbs="[{ label: 'Довідники' }]" />
    <div class="row q-col-gutter-md">
      <div v-for="d in list" :key="d.name" class="col-12 col-sm-6 col-lg-4">
        <router-link :to="{ name: 'lab-dictionary', params: { name: d.name } }" style="text-decoration: none">
          <div class="medlink-card q-pa-md dict-card">
            <div class="row items-center no-wrap">
              <q-avatar color="blue-1" text-color="primary" :icon="d.icon" size="44px" class="q-mr-md" />
              <div>
                <div class="text-subtitle1 text-weight-bold text-grey-9">{{ d.title }}</div>
                <div class="text-caption text-grey-7">{{ d.description }}</div>
              </div>
            </div>
          </div>
        </router-link>
      </div>
      <div class="col-12 col-sm-6 col-lg-4">
        <router-link :to="{ name: 'lab-norms' }" style="text-decoration: none">
          <div class="medlink-card q-pa-md dict-card">
            <div class="row items-center no-wrap">
              <q-avatar color="teal-1" text-color="teal-8" icon="fas fa-sliders-h" size="44px" class="q-mr-md" />
              <div><div class="text-subtitle1 text-weight-bold text-grey-9">Норми та каскад референсів</div><div class="text-caption text-grey-7">Картка послуги, шари норм за статтю/віком/вагітністю/МКХ-10, резолвер</div></div>
            </div>
          </div>
        </router-link>
      </div>
    </div>
  </div>
</template>

<script>
import { DICTIONARY_LIST } from './dictionarySchemas';
export default {
  name: 'DictionariesHome',
  computed: { list () { return DICTIONARY_LIST; } }
};
</script>

<style scoped>
.dict-card { transition: box-shadow .15s; height: 100%; }
.dict-card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.10); }
</style>
