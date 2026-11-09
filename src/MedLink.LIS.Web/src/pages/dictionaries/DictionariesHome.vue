<template>
  <div class="dictionaries-home">
    <page-header title="Довідники лабораторії" icon="fas fa-book-medical" subtitle="Номенклатура ЛІС: біоматеріали, пробірки, методики, показники, послуги, мікробіологія, патоморфологія, календар, реагенти, шаблони бланків, організаційна структура" :breadcrumbs="[{ label: 'Довідники' }]" />
    <q-banner v-if="!canWrite" dense rounded class="bg-blue-1 text-grey-9 q-mb-md" data-testid="dictHomeReadOnly">
      <template v-slot:avatar><q-icon name="visibility" color="primary" /></template>
      Режим перегляду: ваша роль може переглядати та експортувати довідники, але не змінювати їх.
    </q-banner>
    <div v-for="g in groups" :key="g.key" class="q-mb-md">
      <div class="text-subtitle2 text-grey-8 q-mb-sm">{{ g.title }}</div>
      <div class="row q-col-gutter-md">
        <div v-for="d in g.items" :key="d.name || d.route" class="col-12 col-sm-6 col-lg-4">
          <router-link :to="d.to || { name: 'lab-dictionary', params: { name: d.name } }" style="text-decoration: none" :data-testid="`dictCard-${d.name || d.route}`">
            <div class="medlink-card q-pa-md dict-card">
              <div class="row items-center no-wrap">
                <q-avatar :color="d.color || 'blue-1'" :text-color="d.textColor || 'primary'" :icon="d.icon" size="44px" class="q-mr-md" />
                <div>
                  <div class="text-subtitle1 text-weight-bold text-grey-9">{{ d.title }}</div>
                  <div class="text-caption text-grey-7">{{ d.description }}</div>
                </div>
              </div>
            </div>
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import roleMixin from '../../mixins/roleMixin';
import { routeAllowed } from '../../utils/menuAccess';
import { DICTIONARY_LIST, DICTIONARY_GROUPS } from './dictionarySchemas';

/** Довідники зі своїми екранами: показуються в групах поряд з універсальними. */
const EXTRA = [
  { group: 'basic', route: 'lab-norms', icon: 'fas fa-sliders-h', color: 'teal-1', textColor: 'teal-8', title: 'Норми та каскад референсів', description: 'Картка послуги, шари норм за статтю, віком, вагітністю та МКХ-10, перевірка норм' },
  { group: 'basic', route: 'lab-pricing', icon: 'fas fa-money-bill-wave', color: 'teal-1', textColor: 'teal-8', title: 'Прайс-листи, пакети та платники', description: 'Ціни послуг, пакети, масове оновлення цін файлом, платники та програми страхування' },
  { group: 'basic', route: 'lab-report-templates', icon: 'fas fa-file-alt', color: 'teal-1', textColor: 'teal-8', title: 'Шаблони бланків результатів', description: 'Версії бланків (основний, попередній, CITO): шапка, підвал, мови; активація версії' },
  { group: 'path', route: 'lab-path-dictionaries', icon: 'fas fa-book-medical', color: 'teal-1', textColor: 'teal-8', title: 'Довідники патоморфології (усі разом)', description: 'Усі довідники ПГД та цитології на одному екрані з вкладками' }
];

export default {
  name: 'DictionariesHome',
  mixins: [roleMixin],
  computed: {
    canWrite () { return this.canDo('LAB_DOCTOR'); },
    groups () {
      const role = this.currentLabRole;
      return DICTIONARY_GROUPS.map(g => ({
        ...g,
        items: [
          ...DICTIONARY_LIST.filter(d => d.group === g.key),
          ...EXTRA.filter(e => e.group === g.key && routeAllowed(e.route, role)).map(e => ({ ...e, to: { name: e.route } }))
        ]
      })).filter(g => g.items.length);
    }
  }
};
</script>

<style scoped>
.dict-card { transition: box-shadow .15s; height: 100%; }
.dict-card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.10); }
</style>
