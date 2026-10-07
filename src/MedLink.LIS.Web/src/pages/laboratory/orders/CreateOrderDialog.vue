<template>
  <q-dialog :value="value" @input="$emit('input', $event)" persistent maximized-mobile>
    <q-card style="width: 980px; max-width: 98vw">
      <q-card-section class="row items-center bg-primary text-white q-py-sm">
        <q-icon name="post_add" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-bold">Нове направлення / замовлення</div>
        <q-space />
        <q-toggle v-model="form.isUrgentCito" color="deep-orange-5" keep-color label="CITO (терміново)" class="q-mr-md" />
        <q-btn flat round dense icon="close" v-close-popup />
      </q-card-section>

      <q-form @submit.prevent="submit">
        <q-card-section class="q-gutter-y-sm" style="max-height: 70vh; overflow: auto">
          <!-- Пацієнт -->
          <div class="section-title">1. Пацієнт</div>
          <div v-if="!newPatientMode" class="row q-col-gutter-sm items-start">
            <div class="col-12 col-md-8">
              <patient-select v-model="patient" :rules="[v => !!v || 'Оберіть пацієнта']" @create="startNewPatient" />
            </div>
            <div class="col-12 col-md-4">
              <q-btn outline color="primary" icon="person_add" label="Швидко створити пацієнта" class="full-width" @click="startNewPatient('')" />
            </div>
            <div v-if="patient" class="col-12">
              <q-banner dense class="bg-blue-1 text-grey-9" rounded>
                <b>{{ patientName }}</b> · {{ patient.birthDate | date }} ({{ patientAge }} р.) · {{ genderLabel(patient.gender) }}
                <span v-if="patient.phone"> · {{ patient.phone }}</span>
              </q-banner>
            </div>
          </div>
          <div v-else class="row q-col-gutter-sm">
            <div class="col-12 col-md-4"><q-input v-model="newPatient.lastName" outlined dense label="Прізвище *" :rules="[v => !!v || 'Обов’язково']" /></div>
            <div class="col-12 col-md-4"><q-input v-model="newPatient.firstName" outlined dense label="Ім’я *" :rules="[v => !!v || 'Обов’язково']" /></div>
            <div class="col-12 col-md-4"><q-input v-model="newPatient.middleName" outlined dense label="По батькові" /></div>
            <div class="col-6 col-md-3"><q-input v-model="newPatient.birthDate" outlined dense type="date" label="Дата народження *" stack-label :rules="[v => !!v || 'Обов’язково']" /></div>
            <div class="col-6 col-md-3">
              <q-select v-model="newPatient.gender" outlined dense label="Стать *" :options="[{ value: 'M', label: 'Чоловіча' }, { value: 'F', label: 'Жіноча' }]" emit-value map-options :rules="[v => !!v || 'Обов’язково']" />
            </div>
            <div class="col-6 col-md-3"><q-input v-model="newPatient.phone" outlined dense label="Телефон" mask="+38 (###) ###-##-##" unmasked-value fill-mask /></div>
            <div class="col-6 col-md-3"><q-input v-model="newPatient.taxId" outlined dense label="ІПН" mask="##########" /></div>
            <div class="col-12 col-md-8"><q-input v-model="newPatient.email" outlined dense label="E-mail (для сповіщень)" type="email" /></div>
            <div class="col-12 col-md-4 text-right">
              <q-btn flat color="grey-8" label="Назад до пошуку" icon="arrow_back" @click="newPatientMode = false" />
            </div>
          </div>

          <!-- Направлення -->
          <div class="section-title q-mt-md">2. Направлення</div>
          <div class="row q-col-gutter-sm">
            <div class="col-12 col-md-4">
              <q-select v-model="form.doctorId" outlined dense clearable label="Лікар-замовник" :options="doctorOptions" emit-value map-options use-input input-debounce="0" @filter="filterDoctors" />
            </div>
            <div class="col-12 col-md-4">
              <q-select v-model="form.departmentId" outlined dense clearable label="Відділення" :options="departmentOptions" emit-value map-options />
            </div>
            <div class="col-12 col-md-4">
              <q-input v-model="form.ehealthReferralId" outlined dense label="ID е-направлення (eHealth)" />
            </div>
            <div class="col-12">
              <q-input v-model="form.clinicalNotes" outlined dense autogrow label="Клінічні дані / діагноз (МКХ-10), коментар" />
            </div>
          </div>

          <!-- Послуги -->
          <div class="section-title q-mt-md row items-center justify-between">
            <span>3. Послуги та показники</span>
            <q-btn-toggle v-model="pickMode" dense no-caps unelevated toggle-color="primary" color="white" text-color="grey-8" :options="[{ value: 'lists', label: 'Списки' }, { value: 'matrix', label: 'Матриця призначень' }]" data-testid="pickMode" />
          </div>
          <order-matrix v-if="pickMode === 'matrix'" :value="{ profileIds: form.profileIds, testIds: form.testIds }" height="34vh" @input="onMatrix" />
          <div v-show="pickMode === 'lists'" class="row q-col-gutter-sm">
            <div class="col-12 col-md-6">
              <q-select
                v-model="form.profileIds"
                outlined dense multiple use-chips use-input input-debounce="0"
                label="Профілі (послуги)"
                :options="profileOptionsFiltered"
                emit-value map-options
                @filter="filterProfiles"
              >
                <template v-slot:option="scope">
                  <q-item v-bind="scope.itemProps" v-on="scope.itemEvents">
                    <q-item-section>
                      <q-item-label>{{ scope.opt.label }}</q-item-label>
                      <q-item-label caption>{{ scope.opt.item.code }} · {{ scope.opt.item.category }} · {{ (scope.opt.item.items || []).length }} показн. · TAT {{ scope.opt.item.turnaroundHours }} год</q-item-label>
                    </q-item-section>
                    <q-item-section side><span class="text-weight-bold">{{ scope.opt.item.price | money }}</span></q-item-section>
                  </q-item>
                </template>
              </q-select>
            </div>
            <div class="col-12 col-md-6">
              <q-select
                v-model="form.testIds"
                outlined dense multiple use-chips use-input input-debounce="0"
                label="Окремі показники (тести)"
                :options="testOptionsFiltered"
                emit-value map-options
                @filter="filterTests"
              >
                <template v-slot:option="scope">
                  <q-item v-bind="scope.itemProps" v-on="scope.itemEvents">
                    <q-item-section>
                      <q-item-label>{{ scope.opt.label }}</q-item-label>
                      <q-item-label caption>{{ scope.opt.item.code }} · {{ scope.opt.item.unit }} · {{ biomaterialName(scope.opt.item.biomaterialTypeId) }}</q-item-label>
                    </q-item-section>
                  </q-item>
                </template>
              </q-select>
            </div>
          </div>

          <!-- План пробірок + ціна -->
          <div v-show="pickMode === 'lists'" class="row q-col-gutter-md q-mt-xs">
            <div class="col-12 col-md-7">
              <div class="medlink-card">
                <div class="medlink-card__title"><span><q-icon name="science" class="q-mr-xs" />Автопідбір пробірок (попередній план)</span><span class="text-grey-6 text-caption">{{ tubePlan.length }} пробірк.</span></div>
                <q-list v-if="tubePlan.length" dense separator>
                  <q-item v-for="t in tubePlan" :key="t.key">
                    <q-item-section avatar>
                      <div :style="{ background: t.color, width: '18px', height: '28px', borderRadius: '3px 3px 8px 8px', border: '1px solid #999' }" />
                    </q-item-section>
                    <q-item-section>
                      <q-item-label>{{ t.tubeName }} <span class="text-grey-7">· {{ t.biomaterialName }}</span></q-item-label>
                      <q-item-label caption>{{ t.tests.join(', ') }}</q-item-label>
                    </q-item-section>
                    <q-item-section side><q-badge color="grey-7" :label="`${t.tests.length} тест.`" /></q-item-section>
                  </q-item>
                </q-list>
                <div v-else class="q-pa-md text-grey-6 text-caption">Оберіть профілі або показники — план пробірок сформується автоматично. Остаточний підбір і штрихкоди генерує сервер.</div>
              </div>
            </div>
            <div class="col-12 col-md-5">
              <div class="medlink-card q-pa-md">
                <div class="row items-center justify-between"><span class="text-grey-7">Показників усього</span><b>{{ allTestCodes.length }}</b></div>
                <div class="row items-center justify-between"><span class="text-grey-7">Профілів</span><b>{{ form.profileIds.length }}</b></div>
                <q-separator class="q-my-sm" />
                <div class="row items-center justify-between text-h6"><span>До сплати</span><span class="text-primary">{{ totalPrice | money }}</span></div>
                <div v-if="form.isUrgentCito" class="text-caption text-deep-orange-7 q-mt-xs"><q-icon name="bolt" /> CITO — пріоритетна черга та окрема позначка на етикетках</div>
              </div>
            </div>
          </div>
        </q-card-section>

        <q-card-actions align="right" class="bg-grey-1">
          <div v-if="submitError" class="text-negative text-caption q-mr-md ellipsis" style="max-width: 50%">{{ submitError }}</div>
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn type="submit" color="primary" icon="save" label="Створити замовлення" :loading="saving" :disable="!canSubmit" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script>
import PatientSelect from '../../../components/common/PatientSelect.vue';
import OrderMatrix from '../../../components/orders/OrderMatrix.vue';
import { patientDisplay, genderLabel, ageFromBirthDate } from '../../../utils/format';

const emptyForm = () => ({
  isUrgentCito: false,
  doctorId: null,
  departmentId: null,
  ehealthReferralId: '',
  clinicalNotes: '',
  profileIds: [],
  testIds: []
});

export default {
  name: 'CreateOrderDialog',
  components: { PatientSelect, OrderMatrix },
  props: { value: Boolean },
  data () {
    return {
      pickMode: 'lists',
      patient: null,
      newPatientMode: false,
      newPatient: { lastName: '', firstName: '', middleName: '', birthDate: '', gender: null, phone: '', taxId: '', email: '' },
      form: emptyForm(),
      saving: false,
      submitError: '',
      profileFilter: '',
      testFilter: '',
      doctorFilter: ''
    };
  },
  computed: {
    profiles () { return this.$store.getters['dictionaries/items']('profiles').filter(p => p.isActive !== false); },
    tests () { return this.$store.getters['dictionaries/items']('tests').filter(t => t.isActive !== false); },
    biomaterials () { return this.$store.getters['dictionaries/items']('biomaterials'); },
    tubeTypes () { return this.$store.getters['dictionaries/items']('tube-types'); },
    employees () { return this.$store.getters['dictionaries/items']('employees'); },
    departmentOptions () { return this.$store.getters['dictionaries/options']('departments'); },
    doctorOptions () {
      const f = this.doctorFilter.toLowerCase();
      return this.employees
        .map(e => ({ value: e.id, label: e.fullName || e.name, caption: e.position }))
        .filter(o => !f || (o.label || '').toLowerCase().includes(f));
    },
    profileOptionsFiltered () {
      const f = this.profileFilter.toLowerCase();
      return this.profiles.filter(p => !f || `${p.name} ${p.code}`.toLowerCase().includes(f)).map(p => ({ value: p.id, label: p.name, item: p }));
    },
    testOptionsFiltered () {
      const f = this.testFilter.toLowerCase();
      return this.tests.filter(t => !f || `${t.name} ${t.code}`.toLowerCase().includes(f)).map(t => ({ value: t.id, label: t.name, item: t }));
    },
    selectedProfiles () { return this.profiles.filter(p => this.form.profileIds.includes(p.id)); },
    selectedTests () {
      const ids = new Set(this.form.testIds);
      this.selectedProfiles.forEach(p => (p.items || []).forEach(i => ids.add(i.testId)));
      return this.tests.filter(t => ids.has(t.id));
    },
    allTestCodes () { return this.selectedTests.map(t => t.code); },
    totalPrice () {
      const profiles = this.selectedProfiles.reduce((s, p) => s + (Number(p.price) || 0), 0);
      const inProfiles = new Set();
      this.selectedProfiles.forEach(p => (p.items || []).forEach(i => inProfiles.add(i.testId)));
      const singles = this.tests.filter(t => this.form.testIds.includes(t.id) && !inProfiles.has(t.id)).reduce((s, t) => s + (Number(t.price) || 0), 0);
      return profiles + singles;
    },
    tubePlan () {
      const groups = {};
      this.selectedTests.forEach(t => {
        const bio = this.biomaterials.find(b => b.id === t.biomaterialTypeId);
        const tube = this.tubeTypes.find(tt => bio && (tt.code === bio.defaultContainer || tt.name === bio.defaultContainer || tt.id === bio.defaultTubeTypeId));
        const key = `${t.biomaterialTypeId || 'x'}-${tube ? tube.id : 'def'}`;
        if (!groups[key]) {
          groups[key] = {
            key,
            biomaterialName: bio ? bio.name : 'Біоматеріал',
            tubeName: tube ? tube.name : (bio && bio.defaultContainer) || 'Пробірка за замовчуванням',
            color: tube ? tube.colorCode : '#bbb',
            order: tube ? tube.orderOfDrawIndex : 99,
            tests: []
          };
        }
        groups[key].tests.push(t.code);
      });
      return Object.values(groups).sort((a, b) => a.order - b.order);
    },
    patientName () { return patientDisplay(this.patient); },
    patientAge () { return ageFromBirthDate(this.patient && this.patient.birthDate); },
    canSubmit () {
      const hasPatient = this.newPatientMode ? (this.newPatient.lastName && this.newPatient.firstName && this.newPatient.birthDate && this.newPatient.gender) : !!this.patient;
      return hasPatient && (this.form.profileIds.length || this.form.testIds.length) && !this.saving;
    }
  },
  watch: {
    value (v) {
      if (v) {
        this.reset();
        this.$store.dispatch('dictionaries/loadMany', ['profiles', 'tests', 'biomaterials', 'tube-types', 'employees', 'departments']);
      }
    }
  },
  methods: {
    genderLabel,
    reset () {
      this.patient = null;
      this.newPatientMode = false;
      this.form = emptyForm();
      this.submitError = '';
      this.newPatient = { lastName: '', firstName: '', middleName: '', birthDate: '', gender: null, phone: '', taxId: '', email: '' };
    },
    startNewPatient (query) {
      this.newPatientMode = true;
      const parts = (query || '').trim().split(/\s+/);
      if (parts[0]) this.newPatient.lastName = parts[0];
      if (parts[1]) this.newPatient.firstName = parts[1];
      if (parts[2]) this.newPatient.middleName = parts[2];
    },
    biomaterialName (id) {
      const b = this.biomaterials.find(x => x.id === id);
      return b ? b.name : '';
    },
    onMatrix (sel) { this.form.profileIds = sel.profileIds || []; this.form.testIds = sel.testIds || []; },
    filterProfiles (val, update) { update(() => { this.profileFilter = val || ''; }); },
    filterTests (val, update) { update(() => { this.testFilter = val || ''; }); },
    filterDoctors (val, update) { update(() => { this.doctorFilter = val || ''; }); },
    async submit () {
      this.saving = true;
      this.submitError = '';
      const body = {
        ...this.form,
        ehealthReferralId: this.form.ehealthReferralId || null,
        clinicalNotes: this.form.clinicalNotes || null
      };
      if (this.newPatientMode) body.newPatient = { ...this.newPatient };
      else body.patientId = this.patient.id;
      try {
        const order = await this.$api.createOrder(body);
        this.$q.notify({ type: 'positive', message: `Замовлення ${order.orderNumber || ''} створено` });
        this.$emit('created', order);
        this.$emit('input', false);
      } catch (e) {
        this.submitError = e.userMessage || 'Не вдалося створити замовлення';
      } finally {
        this.saving = false;
      }
    }
  }
};
</script>
