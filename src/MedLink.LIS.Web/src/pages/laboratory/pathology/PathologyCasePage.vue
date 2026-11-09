<!--
  ТЗ 22.14 п. 4–7, 9: «Картка справи» ПГД — шапка, прогрес етапів, вкладки: Направлення, Вирізка (шаблон, блоки, вартість),
  Блоки і скельця (етапи обробки, дозамовлення забарвлень/ІГХ), Опис і висновок (МКХ-О, reflex ІГХ, попередній, підпис КЕП,
  доповнення, версії), Друк 014/о, Події. Для CITO — таймер і «Повідомити результат в операційну» з підтвердженням отримувача.
  Дії, недоступні ролі поточного співробітника, блокуються заздалегідь з підказкою (roleMixin), а не після відповіді 403.
-->
<template>
  <div data-testid="pathologyCasePage">
    <page-header :title="pageTitle" icon="fas fa-folder-open" :subtitle="subtitle" :breadcrumbs="crumbs">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load(true)" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" :retryable="apiErrorRetryable" @retry="load" @dismiss="apiError = null" />

    <template v-if="c">
      <div class="medlink-card q-pa-sm q-mb-sm">
        <div class="row items-center q-col-gutter-sm">
          <div class="col-12 col-md">
            <div class="stages row no-wrap">
              <div v-for="s in c.stages" :key="s.code" class="stage" :class="{ done: s.done, cur: s.current }">{{ s.label }}</div>
            </div>
          </div>
          <div class="col-auto text-right">
            <q-badge v-if="c.cito" color="negative" label="CITO" class="q-mr-xs" />
            <q-badge v-if="c.overdue" color="negative" outline label="Прострочено" class="q-mr-xs" />
            <span v-if="signed" class="text-caption text-grey-7">Підписано: <b>{{ c.signedAt | datetime }}</b> · вартість <b>{{ c.price | money }}</b></span>
            <span v-else class="text-caption text-grey-7">Строк: <b :class="c.overdue ? 'text-negative' : ''">{{ c.slaDueAt | datetime }}</b> · вартість <b>{{ c.price | money }}</b></span>
          </div>
        </div>
        <div v-if="c.citoTimer" class="row items-center q-mt-sm cito-bar" :class="citoClass" data-testid="citoBar">
          <q-icon :name="c.citoTimer.notifiedAt ? 'check_circle' : 'timer'" size="22px" class="q-mr-sm" />
          <div class="col">
            <template v-if="c.citoTimer.notifiedAt">
              Повідомлено в операційну <b>{{ c.citoTimer.notifiedAt | datetime }}</b> — через <b>{{ c.citoTimer.minutes }} хв</b> від надходження (ціль {{ c.citoTimer.targetMinutes }} хв)<span v-if="citoLate" class="text-deep-orange-9"> · із запізненням</span>
            </template>
            <template v-else>
              <b>{{ c.citoTimer.minutes }} хв</b> від надходження (ціль {{ c.citoTimer.targetMinutes }} хв)
            </template>
          </div>
          <span>
            <q-btn unelevated :color="c.citoTimer.notifiedAt ? 'grey-7' : 'negative'" icon="phone_in_talk" :label="c.citoTimer.notifiedAt ? 'Повторити повідомлення' : 'Повідомити результат в операційну'"
                   :disable="!citoCanNotify" data-testid="citoNoticeBtn" @click="openCito" />
            <q-tooltip v-if="citoHint" max-width="380px" :content-style="{ whiteSpace: 'normal' }">{{ citoHint }}</q-tooltip>
          </span>
        </div>
      </div>

      <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm">
        <q-tab name="ref" icon="description" label="Направлення" />
        <q-tab v-if="!isCyto" name="gross" icon="content_cut" label="Вирізка" />
        <q-tab v-else name="prep" icon="science" label="Приготування" data-testid="prepTab" />
        <q-tab name="blocks" icon="view_module" :label="isCyto ? `Скельця (${c.totals.slides})` : `Блоки і скельця (${c.totals.blocks}/${c.totals.slides})`" />
        <q-tab name="report" icon="biotech" label="Опис і висновок" />
        <q-tab v-if="!isCyto" name="print" icon="print" label="Друк 014/о" />
        <q-tab name="events" icon="history" label="Події" />
      </q-tabs>

      <!-- Направлення -->
      <div v-show="tab === 'ref'" class="medlink-card q-pa-md">
        <q-banner v-if="!isCyto && c.priorSignedCases && c.priorSignedCases.length" dense class="bg-amber-1 text-amber-10 q-mb-sm">
          Направлення позначено «первинна», але в пацієнта вже є підписані справи: {{ c.priorSignedCases.join(', ') }}. Перевірте вид біопсії.
        </q-banner>
        <q-markup-table dense flat bordered wrap-cells class="kv">
          <tbody>
            <tr v-if="c.referral.form014 && c.referral.form014.facility"><td>Заклад</td><td>{{ c.referral.form014.facility }}</td></tr>
            <tr><td>Пацієнт</td><td>{{ c.patient.name }}, {{ c.patient.birthday | date }}</td></tr>
            <tr><td>Направник</td><td>{{ c.department }} · {{ c.referral.referringDoctor }}<span v-if="c.referral.medicalCardNo"> · карта {{ c.referral.medicalCardNo }}</span></td></tr>
            <tr v-if="!isCyto"><td>Біопсія</td><td>{{ c.referral.biopsyKindLabel }}
              <router-link v-if="c.primaryCaseId" :to="{ name: 'lab-path-case', params: { id: c.primaryCaseId } }"> · первинна справа {{ c.referral.primaryCaseNo }}</router-link>
              <span v-else-if="c.referral.primaryCaseNo"> · первинна справа {{ c.referral.primaryCaseNo }}</span></td></tr>
            <tr v-if="!isCyto"><td>Операція / маніпуляція</td><td>{{ c.referral.operationText || '—' }}</td></tr>
            <tr><td>Маркування</td><td>{{ c.referral.markingText || '—' }}</td></tr>
            <tr v-if="c.cyto"><td>Вид матеріалу</td><td>{{ c.cyto.materialLabel }}</td></tr>
            <tr v-if="c.referral.gyn"><td>Гінекологічний анамнез</td><td>
              <span v-if="c.referral.gyn.cycleDay">день циклу {{ c.referral.gyn.cycleDay }}</span><span v-if="c.referral.gyn.lastMenstrualDate"> · останні місячні {{ c.referral.gyn.lastMenstrualDate | date }}</span>
              <span v-if="c.referral.gyn.menopauseState"> · {{ { NONE: 'менопаузи немає', PERI: 'перименопауза', POST: 'постменопауза', AMENORRHEA: 'аменорея' }[c.referral.gyn.menopauseState] }}</span></td></tr>
            <tr><td>Клінічні дані</td><td>{{ c.referral.clinicalData || '—' }}</td></tr>
            <tr><td>Клінічний діагноз</td><td>{{ c.referral.clinicalDiagnosis || '—' }}</td></tr>
            <tr v-if="c.ehealthReferral"><td>Е-направлення ЄСОЗ</td><td>{{ c.ehealthReferral }} · {{ c.ehealthStatus }}</td></tr>
            <tr><td>Прийнято</td><td>{{ c.receivedAt | datetime }}</td></tr>
          </tbody>
        </q-markup-table>
      </div>

      <!-- Вирізка -->
      <div v-show="tab === 'gross'" class="medlink-card q-pa-md" data-testid="grossTab">
        <div class="row q-col-gutter-sm q-mb-sm">
          <div class="col-12 col-md-6">
            <q-select v-model="gross.templateName" dense outlined emit-value map-options :options="templateOptions" label="Шаблон макроопису (орган · вид матеріалу)" :disable="grossLocked" @input="applyMacroTemplate" />
          </div>
          <div class="col-12 col-md-6 text-right text-caption text-grey-8 self-center">
            Блоків <b>{{ grossBlocks }}</b> · шматочків <b>{{ grossPieces }}</b> · вартість вирізки <b data-testid="grossPrice">{{ grossPrice | money }}</b>
            <div v-if="c.kind === 'CITO'">надбавка CITO {{ prices.cito | money }} + перший блок {{ prices.firstBlock | money }} + кожен наступний {{ prices.nextBlock | money }}</div>
            <div v-else>перший блок {{ prices.firstBlock | money }} + кожен наступний {{ prices.nextBlock | money }}</div>
          </div>
        </div>
        <div class="row q-col-gutter-sm q-mb-sm">
          <div class="col-12 col-md-6"><q-select v-model="gross.pathologistId" dense outlined emit-value map-options :options="staffOptions('pathologists')" label="Лікар-патолог *" :disable="grossLocked" data-testid="grossPathologist" /></div>
          <div class="col-12 col-md-6"><q-select v-model="gross.assistantId" dense outlined emit-value map-options :options="staffOptions('assistants')" label="Вирізку провів (асистент) *" :disable="grossLocked" data-testid="grossAssistant" /></div>
        </div>
        <q-input v-model="gross.macro" dense outlined type="textarea" autogrow label="Макроопис (п. 17) *" :readonly="grossLocked" input-style="min-height: 6em" class="q-mb-sm" />
        <q-banner v-if="macroOpen.length" dense class="bg-amber-1 text-amber-10 q-mb-md" data-testid="macroPlaceholders">
          <q-icon name="warning" /> Заповніть заготовки у фігурних дужках: {{ macroOpen.join(', ') }} — з ними вирізку завершити не можна.
        </q-banner>
        <div v-for="o in gross.objects" :key="o.objectId" class="q-mb-sm">
          <div class="row items-center q-mb-xs">
            <b>Об'єкт №{{ o.mark }}</b>&nbsp;<span class="text-grey-7">{{ o.description }}</span>
            <q-space />
            <q-select v-model="o.remainderState" dense outlined emit-value map-options :options="remainderOptions" label="Залишок матеріалу" style="min-width: 190px" class="q-mr-sm" :disable="grossLocked" />
            <q-input v-if="o.remainderState && o.remainderState !== 'NONE'" v-model="o.remainderNote" dense outlined label="Де / примітка" style="min-width: 180px" class="q-mr-sm" :readonly="grossLocked" />
            <q-btn v-if="grossEditable" flat dense size="sm" color="primary" icon="add" label="Додати блок" no-caps :disable="!canPath" @click="o.blocks.push({ pieces: 1, track: 'STANDARD', methods: ['HE'] })" />
          </div>
          <div class="row q-gutter-sm">
            <div v-for="(b, i) in o.blocks" :key="i" class="block-chip">
              <div class="text-weight-bold">{{ o.letter }}{{ i + 1 }}</div>
              <q-input v-model.number="b.pieces" dense outlined type="number" min="1" style="width: 104px" label="Шматочків" :readonly="grossLocked" />
              <q-toggle v-model="b.track" true-value="DECALC" false-value="STANDARD" dense label="Декальцинація" :disable="grossLocked" />
              <q-select v-model="b.methods" dense outlined multiple use-chips emit-value map-options :options="gross5Options" :label="`Методики (до ${maxMethods})`" style="min-width: 190px; max-width: 260px"
                        :disable="grossLocked" :rules="[v => !v || v.length <= maxMethods || `не більше ${maxMethods}`]" />
              <q-btn v-if="grossEditable" flat dense round size="xs" icon="close" :disable="!canPath" @click="o.blocks.splice(i, 1)"><q-tooltip>Прибрати блок</q-tooltip></q-btn>
            </div>
            <div v-if="!o.blocks.length" class="text-caption text-grey-6">Блоків немає</div>
          </div>
        </div>
        <div v-if="grossEditable" class="row q-gutter-sm justify-end items-center">
          <span v-if="!canPath" class="text-caption text-grey-7">{{ roleHint('LAB_DOCTOR') }}</span>
          <q-btn flat color="primary" icon="save" label="Зберегти" :disable="!canPath" @click="saveGross(false)" />
          <span>
            <q-btn unelevated color="primary" icon="done_all" label="Завершити вирізку" data-testid="finishGross" :disable="!canFinishGross" @click="saveGross(true)" />
            <q-tooltip v-if="!canFinishGross" max-width="380px" :content-style="{ whiteSpace: 'normal' }">{{ !canPath ? roleHint('LAB_DOCTOR') : `Бракує: ${grossMissing.join(', ')}` }}</q-tooltip>
          </span>
        </div>
        <div v-else class="text-caption text-grey-7">Вирізку завершено {{ c.grossFinishedAt | datetime }} — скельця створено за обраними методиками. Вкладка лише для перегляду. Патолог: {{ c.pathologist && c.pathologist.name }}; вирізку провів: {{ c.assistant && c.assistant.name }}.</div>
      </div>

      <!-- Цитологія: приготування препаратів -->
      <div v-if="isCyto" v-show="tab === 'prep'" class="medlink-card q-pa-md" data-testid="prepPane">
        <div v-if="!c.cyto.prepared">
          <div class="text-caption text-grey-8 q-mb-sm">Для кожного контейнера вкажіть скельця й методику (за замовчуванням Папаніколау). Вартість скельця — за довідником методик; разом <b>{{ prepSum | money }}</b>.</div>
          <div v-for="o in prep.objects" :key="o.objectId" class="q-mb-sm">
            <div class="row items-center q-mb-xs"><b>Контейнер №{{ o.mark }}</b>&nbsp;<span class="text-grey-7">{{ o.description }}</span><q-space />
              <q-btn flat dense size="sm" color="primary" icon="add" label="Скельце" :disable="o.slides.length >= 10 || !canLab" @click="o.slides.push({ method: 'PAP' })" /></div>
            <div class="row q-gutter-sm">
              <div v-for="(s, i) in o.slides" :key="i" class="block-chip">
                <div class="text-weight-bold">{{ i + 1 }}</div>
                <q-select v-model="s.method" dense outlined emit-value map-options :options="cytoMethodOptions" style="min-width: 190px" :disable="!canLab" />
                <q-btn flat dense round size="xs" icon="close" :disable="!canLab" @click="o.slides.splice(i, 1)"><q-tooltip>Прибрати скельце</q-tooltip></q-btn>
              </div>
              <div v-if="!o.slides.length" class="text-caption text-grey-6">Скелець немає</div>
            </div>
          </div>
          <q-input v-model="prep.note" dense outlined label="Примітка до приготування" class="q-mb-sm" :readonly="!canLab" />
          <div class="row justify-end items-center">
            <span>
              <q-btn unelevated color="primary" icon="done_all" label="Приготувати препарати" data-testid="prepareBtn" :disable="!prepTotal || !canLab" @click="prepare" />
              <q-tooltip v-if="!canLab">{{ roleHint('LAB_TECHNICIAN') }}</q-tooltip>
            </span>
          </div>
        </div>
        <div v-else>
          <q-banner dense class="bg-green-1 text-green-10 q-mb-sm">Препарати приготовано {{ c.cyto.prepared | datetime }} — скельця: {{ c.totals.slides }}. Далі — вкладка «Скельця» (фарбування, покриття скла).</q-banner>
          <span v-if="!signed">
            <q-btn outline color="primary" icon="replay" label="Повторне приготування" data-testid="repeatPrepBtn" :disable="!canLab" @click="repeatPrep" />
            <q-tooltip v-if="!canLab">{{ roleHint('LAB_TECHNICIAN') }}</q-tooltip>
          </span>
        </div>
      </div>

      <!-- Блоки і скельця -->
      <div v-show="tab === 'blocks'" class="medlink-card q-pa-md" data-testid="blocksTab">
        <div class="row items-center q-gutter-sm q-mb-sm">
          <q-input v-model="scan" dense outlined placeholder="Скан касети або скельця" style="width: 240px" :disable="!canLab" @keyup.enter="doScan">
            <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
          </q-input>
          <q-select v-if="labStage" v-model="performerId" dense outlined emit-value map-options stack-label :options="staffOptions('assistants')" label="Виконавець" :placeholder="performerId ? '' : 'Ви (поточний користувач)'" style="width: 220px" :disable="!canLab" />
          <q-input v-if="c.stage === 'SECT'" v-model.number="sectCount" dense outlined type="number" min="1" label="Зрізів" style="width: 90px" :disable="!canLab" />
          <span v-if="labStage">
            <q-btn unelevated color="teal-7" icon="fact_check" :label="advanceLabel" data-testid="advanceAll" :disable="!canLab" @click="advance" />
            <q-tooltip v-if="!canLab">{{ roleHint('LAB_TECHNICIAN') }}</q-tooltip>
          </span>
          <q-space />
          <span v-if="!isCyto">
            <q-btn outline color="purple-6" icon="add_circle_outline" label="Додаткові забарвлення / ІГХ" :disable="!canAdditional || !canPath" @click="openAdditional()" />
            <q-tooltip v-if="!canPath">{{ roleHint('LAB_DOCTOR') }}</q-tooltip>
            <q-tooltip v-else-if="!canAdditional">Дозамовлення стає доступним після завершення вирізки</q-tooltip>
          </span>
        </div>
        <q-markup-table dense flat bordered>
          <thead><tr>
            <th class="text-left">{{ isCyto ? 'Препарат' : 'Блок' }}</th><th v-if="!isCyto" class="text-left">Шматочків</th><th class="text-left">{{ isCyto ? 'Етап' : 'Етап касети' }}</th><th class="text-left">Скельця</th><th class="text-left">Архів</th>
          </tr></thead>
          <tbody>
            <template v-for="o in c.objects">
              <tr v-for="b in o.blocks" :key="b.id">
                <td><b>{{ b.code }}</b> <q-badge v-if="b.isExternal" outline color="indigo-6" label="зовнішній" /><div class="text-caption"><code>{{ b.barcode }}</code></div></td>
                <td v-if="!isCyto">{{ b.pieces || '—' }}</td>
                <td>
                  <template v-if="blockStageInfo(b).label">{{ blockStageInfo(b).label }}</template><template v-else>—</template>
                  <div v-if="blockStageInfo(b).next" class="text-caption text-grey-7">{{ blockStageInfo(b).next }}</div>
                  <div v-if="b.track === 'DECALC'" class="text-caption text-deep-orange-8">декальцинація</div>
                </td>
                <td>
                  <div v-for="s in b.slides" :key="s.id" class="row items-center no-wrap q-mb-xs">
                    <q-badge :color="s.status === 'IHC_IN_WORK' ? 'purple-5' : s.status === 'READY' ? 'positive' : 'grey-6'" :label="s.code" class="q-mr-xs" />
                    <span class="text-caption">{{ s.stainMethod }}<span v-if="s.status === 'IHC_IN_WORK'"> · ІГХ в роботі</span><span v-if="s.orderedFrom && s.orderedFrom !== 'LAB'"> · з {{ orderedFromLabel(s.orderedFrom) }}</span></span>
                    <q-btn v-if="s.status === 'IHC_IN_WORK'" flat dense no-caps size="sm" color="positive" icon="done" label="Готове" class="q-ml-sm" :disable="!canLab" @click="slideReady(s)"><q-tooltip>{{ canLab ? 'Скельце забарвлено, результат готовий' : roleHint('LAB_TECHNICIAN') }}</q-tooltip></q-btn>
                    <span v-if="s.status === 'DISPOSED'" class="text-caption text-negative q-ml-xs">· утилізовано: {{ s.disposeReason }}</span>
                    <q-icon v-else-if="s.coveredAt" name="layers" size="xs" color="teal-7" class="q-ml-xs"><q-tooltip>Покрито покривним склом {{ s.coveredAt | datetime }}</q-tooltip></q-icon>
                    <q-btn v-if="s.status === 'IN_WORK' && !s.isIhc && b.stage === 'STAIN' && !slideStained(s)" flat dense no-caps size="sm" color="primary" icon="brush" label="Забарвлено" class="q-ml-sm" :disable="!canLab" @click="slideReady(s)">
                      <q-tooltip>{{ canLab ? 'Нове скельце: після фарбування позначте його, потім покрийте склом' : roleHint('LAB_TECHNICIAN') }}</q-tooltip>
                    </q-btn>
                    <span v-if="s.status !== 'DISPOSED' && b.stage === 'STAIN' && !s.coveredAt" class="q-ml-sm">
                      <q-btn flat dense no-caps size="sm" color="teal-7" icon="layers" label="Покрити" :disable="!canLab || !slideStained(s)" :data-testid="`cover-${s.code}`" @click="coverSlide(s)" />
                      <q-tooltip>{{ !canLab ? roleHint('LAB_TECHNICIAN') : slideStained(s) ? 'Покрити покривним склом' : s.isIhc ? 'ІГХ ще в роботі: спершу натисніть «Готове», потім покрийте склом' : 'Спершу позначте скельце «Забарвлено», потім покрийте склом' }}</q-tooltip>
                    </span>
                    <q-btn v-if="s.status !== 'DISPOSED' && !s.isExternal" flat dense no-caps size="sm" color="negative" icon="delete_outline" label="Утилізувати" class="q-ml-md" :disable="!canLab" @click="disposeSlide(s)">
                      <q-tooltip>{{ canLab ? 'Утилізувати скельце з браком' : roleHint('LAB_TECHNICIAN') }}</q-tooltip>
                    </q-btn>
                  </div>
                </td>
                <td class="text-caption">
                  <span v-if="b.status === 'ISSUED'" class="text-orange-9">Видано: {{ b.issue && b.issue.recipient }}</span>
                  <span v-else>{{ b.storageLocation || (b.status === 'IN_WORK' ? 'у роботі' : '') }}</span>
                </td>
              </tr>
            </template>
          </tbody>
        </q-markup-table>
        <div v-if="isCyto" class="text-caption text-grey-7 q-mt-sm">Скелець: {{ c.totals.slides }} · методики: {{ c.totals.stains.join(', ') || '—' }}</div>
        <div v-else class="text-caption text-grey-7 q-mt-sm">Шматочків / блоків: {{ c.totals.pieces }} / {{ c.totals.blocks }} · методики: {{ c.totals.stains.join(', ') || '—' }}</div>
      </div>

      <!-- Опис і висновок -->
      <div v-show="tab === 'report'" class="medlink-card q-pa-md" data-testid="reportTab">
        <q-banner v-for="(r, i) in c.reflex" :key="i" dense class="bg-purple-1 text-purple-10 q-mb-sm" data-testid="reflexBanner">
          <q-icon name="auto_awesome" /> Рекомендовано ІГХ-панель: <b>{{ r.panel.join(', ') }}</b> — {{ r.why }}
          <template v-slot:action>
            <q-btn v-if="r.missing.length && canAdditional" flat color="purple-8" no-caps label="Дозамовити панель" :disable="!canPath" @click="openAdditional(r.missing)">
              <q-tooltip>{{ canPath ? `Дозамовити: ${r.missing.join(', ')}` : roleHint('LAB_DOCTOR') }}</q-tooltip>
            </q-btn>
            <span v-else-if="!r.missing.length" class="text-caption">панель замовлено</span>
          </template>
        </q-banner>
        <div v-if="isCyto" class="q-mb-md" data-testid="screeningCard">
          <div class="text-weight-bold q-mb-xs">Оцінка скринінгу</div>
          <q-banner v-if="c.cyto.screening" dense :class="c.cyto.screening.assessment === 'SUSPECT' ? 'bg-orange-1 text-orange-10' : 'bg-green-1 text-green-10'">
            {{ c.cyto.screening.assessment === 'SUSPECT' ? 'Підозра' : 'Норма' }} · {{ c.cyto.screening.by }} · {{ c.cyto.screening.at | datetime }}<span v-if="c.cyto.screening.note"> — {{ c.cyto.screening.note }}</span>
          </q-banner>
          <div v-if="c.stage === 'MICRO'" class="row q-gutter-sm items-center q-mt-xs">
            <q-input v-model="screenNote" dense outlined placeholder="Примітка лаборанта" style="min-width: 260px" :readonly="!canScreen" />
            <span v-if="!c.cyto.screening || c.cyto.screening.assessment !== 'NORMAL'">
              <q-btn unelevated color="positive" :label="c.cyto.screening ? 'Змінити на «Норма»' : 'Норма'" data-testid="screenNormal" :disable="!canScreen" @click="screen('NORMAL')" />
              <q-tooltip v-if="!canScreen">{{ roleHint('LAB_TECHNICIAN', 'LAB_DOCTOR') }}</q-tooltip>
            </span>
            <span v-if="!c.cyto.screening || c.cyto.screening.assessment !== 'SUSPECT'">
              <q-btn unelevated color="orange-8" :label="c.cyto.screening ? 'Змінити на «Підозра»' : 'Підозра'" data-testid="screenSuspect" :disable="!canScreen" @click="screen('SUSPECT')" />
              <q-tooltip v-if="!canScreen">{{ roleHint('LAB_TECHNICIAN', 'LAB_DOCTOR') }}</q-tooltip>
            </span>
          </div>
          <div v-else-if="!c.cyto.screening" class="text-caption text-grey-7">Оцінка скринінгу стане доступною після фарбування.</div>
        </div>
        <q-banner v-if="c.preliminary" dense class="bg-amber-1 text-amber-10 q-mb-sm">Попередній висновок надіслано в МІС {{ c.preliminary.at | datetime }}: {{ c.preliminary.conclusionText }}</q-banner>

        <div class="row q-col-gutter-sm">
          <div class="col-12 col-md-7">
            <q-select v-model="microTemplate" dense outlined emit-value map-options :options="templateOptions" label="Шаблон мікроопису" :disable="signed || !canPath" class="q-mb-sm" @input="applyMicroTemplate" />
            <q-input v-model="rep.micro" dense outlined type="textarea" autogrow :label="isCyto ? 'Опис препарату *' : 'Мікроскопічний опис (п. 17) *'" :readonly="signed || !canPath" input-style="min-height: 9em" class="q-mb-sm" data-testid="microText" />
            <q-input v-model="rep.conclusionText" dense outlined type="textarea" autogrow :label="isCyto ? 'Цитологічний висновок *' : 'Патогістологічний висновок (п. 18) *'" :readonly="signed || !canPath" input-style="min-height: 9em" data-testid="conclusionText">
              <template v-slot:after><q-btn v-if="!signed" flat dense color="primary" icon="merge_type" label="Зібрати текст" :loading="composing" :disable="!canPath" @click="compose" /></template>
            </q-input>
            <q-banner v-if="reportOpen.length && !signed" dense class="bg-amber-1 text-amber-10 q-mt-sm" data-testid="reportPlaceholders">
              <q-icon name="warning" /> У тексті лишились незаповнені заготовки: {{ reportOpen.join(', ') }}. Заповніть їх — інакше підписати не вийде.
            </q-banner>
          </div>
          <div class="col-12 col-md-5">
            <div v-if="isCyto" class="row q-col-gutter-xs" data-testid="cytoFields">
              <div class="col-12"><q-select v-model="rep.adequacy" dense outlined emit-value map-options :options="adequacyOptions" label="Адекватність матеріалу *" :readonly="signed || !canPath" menu-anchor="bottom left" menu-self="top left" data-testid="adequacy" /></div>
              <div class="col-12"><q-select v-model="rep.cytoCategory" dense outlined clearable emit-value map-options :options="categoryOptions" label="Категорія висновку *" :readonly="signed || !canPath" menu-anchor="bottom left" menu-self="top left" :popup-content-style="{ maxHeight: '260px' }" data-testid="cytoCategory" @focus="revealField">
                <template v-slot:option="sc"><q-item v-bind="sc.itemProps" v-on="sc.itemEvents"><q-item-section><q-item-label style="white-space: normal">{{ sc.opt.label }}</q-item-label></q-item-section></q-item></template>
              </q-select></div>
              <div class="col-12"><q-input v-model="rep.recommendation" dense outlined type="textarea" autogrow label="Рекомендації" :readonly="signed || !canPath" /></div>
              <div class="col-12"><q-select v-model="rep.icd10" dense outlined clearable use-input fill-input hide-selected input-debounce="0" emit-value map-options :options="icd10Shown" label="МКХ-10" hint="Оберіть зі списку або введіть код, наприклад N87.0" :rules="icd10Rules" lazy-rules :readonly="signed || !canPath"
                            menu-anchor="bottom left" menu-self="top left" :popup-content-style="{ maxHeight: '220px' }" data-testid="icd10Cyto" @focus="revealField" @filter="icd10Filter" @new-value="icd10New" /></div>
            </div>
            <div v-else class="row q-col-gutter-xs">
              <div class="col-6"><q-select v-model="rep.icd10" dense outlined clearable use-input fill-input hide-selected input-debounce="0" emit-value map-options :options="icd10Shown" label="МКХ-10" hint="Зі списку або код, наприклад C50.4" :rules="icd10Rules" lazy-rules :readonly="signed || !canPath"
                            menu-anchor="bottom left" menu-self="top left" :popup-content-style="{ maxHeight: '220px' }" data-testid="icd10" @focus="revealField" @filter="icd10Filter" @new-value="icd10New" /></div>
              <div class="col-6"><q-select v-model="rep.grade" dense outlined clearable :options="['G1', 'G2', 'G3', 'GX']" label="Ступінь диференціювання (G)" :readonly="signed || !canPath" /></div>
              <div class="col-12">
                <q-select v-model="rep.icdoTopo" dense outlined clearable use-input fill-input hide-selected input-debounce="0" emit-value map-options :options="topoShown" label="МКХ-О топографія *" placeholder="Код або назва" :readonly="signed || !canPath"
                          menu-anchor="bottom left" menu-self="top left" :popup-content-style="{ maxHeight: '220px' }" data-testid="icdoTopo" @focus="revealField" @filter="(v, update) => update(() => { topoQuery = v; })">
                  <template v-slot:no-option><q-item><q-item-section class="text-grey">Нічого не знайдено</q-item-section></q-item></template>
                </q-select>
              </div>
              <div class="col-12">
                <q-select v-model="rep.icdoMorph" dense outlined clearable use-input fill-input hide-selected input-debounce="0" emit-value map-options :options="morphShown" label="МКХ-О морфологія *" placeholder="Код або назва" :readonly="signed || !canPath"
                          menu-anchor="bottom left" menu-self="top left" :popup-content-style="{ maxHeight: '220px' }" data-testid="icdoMorph" @focus="revealField" @filter="(v, update) => update(() => { morphQuery = v; })">
                  <template v-slot:no-option><q-item><q-item-section class="text-grey">Нічого не знайдено</q-item-section></q-item></template>
                </q-select>
              </div>
              <div class="col-6"><q-input v-model="rep.pt" dense outlined label="pT (первинна пухлина)" :readonly="signed || !canPath" /></div>
              <div class="col-6"><q-input v-model="rep.pn" dense outlined label="pN (регіонарні лімфовузли)" :readonly="signed || !canPath" /></div>
              <div class="col-6"><q-input v-model.number="rep.lnTotal" dense outlined type="number" min="0" label="Лімфовузлів досліджено" :readonly="signed || !canPath" /></div>
              <div class="col-6"><q-input v-model.number="rep.lnPositive" dense outlined type="number" min="0" label="Лімфовузлів уражено" :readonly="signed || !canPath" /></div>
              <div class="col-6"><q-select v-model="rep.marginClass" dense outlined clearable :options="['R0', 'R1', 'R2', 'RX']" label="Клас R (край резекції)" :readonly="signed || !canPath" /></div>
              <div class="col-6"><q-input v-model="rep.marginMm" dense outlined label="Відстань до краю, мм" :readonly="signed || !canPath" /></div>
              <div class="col-6"><q-select v-model="rep.lvi" dense outlined emit-value map-options clearable :options="yesNo" label="Лімфоваскулярна інвазія (LVI)" :readonly="signed || !canPath" /></div>
              <div class="col-6"><q-select v-model="rep.pni" dense outlined emit-value map-options clearable :options="yesNo" label="Периневральна інвазія (PNI)" :readonly="signed || !canPath" /></div>
            </div>
          </div>
        </div>

        <div v-if="rep.ihc.length" class="q-mt-md">
          <div class="text-weight-bold q-mb-xs">Таблиця ІГХ</div>
          <q-markup-table dense flat bordered>
            <thead><tr><th class="text-left">Маркер</th><th class="text-left">Скельце</th><th class="text-left">Результат</th><th class="text-left">Оцінка (бал, % клітин)</th></tr></thead>
            <tbody>
              <tr v-for="i in rep.ihc" :key="i.marker">
                <td><b>{{ i.marker }}</b></td>
                <td class="text-caption">{{ slideCode(i.slideId) }}</td>
                <td><q-select v-model="i.resultText" dense outlined clearable emit-value :options="ihcResultOptions(i)" label="Результат" style="min-width: 190px" :readonly="signed || !canPath" /></td>
                <td><q-input v-model="i.score" dense outlined label="Бал / %" :readonly="signed || !canPath" style="width: 130px" /></td>
              </tr>
            </tbody>
          </q-markup-table>
        </div>

        <div class="row q-gutter-sm justify-end items-center q-mt-md">
          <template v-if="!signed">
            <span v-if="!canPath" class="text-caption text-grey-7">{{ roleHint('LAB_DOCTOR') }}</span>
            <q-btn flat color="primary" icon="save" label="Зберегти" data-testid="saveReport" :disable="!canPath" @click="saveReport()" />
            <q-btn outline color="amber-9" icon="schedule_send" label="Попередній у МІС" :disable="!canPath" @click="prelimOpen = true" />
            <span>
              <q-btn unelevated color="primary" icon="draw" label="Підписати КЕП" data-testid="signBtn" :disable="!canSign" @click="sign" />
              <q-tooltip v-if="!canSign" max-width="380px" :content-style="{ whiteSpace: 'normal' }">{{ !canPath ? roleHint('LAB_DOCTOR') : `Бракує: ${signMissing.join('; ')}` }}</q-tooltip>
            </span>
          </template>
          <template v-else>
            <span>
              <q-btn outline color="teal-7" icon="forward_to_inbox" label="Надіслати на пошту" data-testid="dispatchBtn" :disable="!canDispatch" @click="openDispatch" />
              <q-tooltip v-if="!canDispatch">{{ roleHint('LAB_DOCTOR', 'REGISTRAR', 'LAB_TECHNICIAN') }}</q-tooltip>
            </span>
            <span>
              <q-btn unelevated color="primary" icon="post_add" label="Доповнення" data-testid="addendumBtn" :disable="!canPath" @click="openAddendum" />
              <q-tooltip v-if="!canPath">{{ roleHint('LAB_DOCTOR') }}</q-tooltip>
            </span>
          </template>
        </div>

        <div v-if="c.versions.length" class="q-mt-md">
          <div class="text-weight-bold q-mb-xs">Версії висновку</div>
          <q-list bordered separator dense>
            <q-item v-for="v in c.versions" :key="v.id">
              <q-item-section avatar><q-badge :color="v.kind === 'MAIN' ? 'primary' : 'teal-6'" :label="`v${v.version}`" /></q-item-section>
              <q-item-section>
                <q-item-label>{{ v.kindLabel }}<span v-if="v.addendumType"> · {{ addendumLabel(v.addendumType) }}</span> <q-badge v-if="v.changesDiagnosis" color="negative" label="змінює діагноз" /></q-item-label>
                <q-item-label caption class="text-grey-9">{{ v.conclusionText }}</q-item-label>
                <q-item-label v-if="v.reason" caption>Обґрунтування: {{ v.reason }}</q-item-label>
              </q-item-section>
              <q-item-section side class="text-caption">{{ v.signedByName }}<br>{{ v.signedAt | datetime }} · КЕП</q-item-section>
            </q-item>
          </q-list>
        </div>
      </div>

      <!-- Друк 014/о -->
      <div v-show="tab === 'print'" class="medlink-card q-pa-md" data-testid="printTab">
        <div class="row items-center q-mb-sm">
          <q-toggle v-model="highlight" label="Підсвітити автозаповнення" />
          <span v-if="highlight" class="text-caption text-grey-8 q-ml-sm"><span class="legend-hl" /> жовтим підсвічено поля, заповнені автоматично (сторона 1 — з МІС, сторона 2 — з ЛІС)</span>
          <q-space />
          <q-btn flat color="primary" icon="picture_as_pdf" label="Сторона 1" type="a" :href="$api.pathForm014PdfUrl(c.id, '1')" target="_blank" />
          <q-btn flat color="primary" icon="picture_as_pdf" label="Сторона 2" type="a" :href="$api.pathForm014PdfUrl(c.id, '2')" target="_blank" />
          <q-btn unelevated color="primary" icon="print" label="Обидві сторони (PDF)" type="a" :href="$api.pathForm014PdfUrl(c.id)" target="_blank" />
        </div>
        <div v-if="form" class="row q-col-gutter-md">
          <div v-for="side in [{ t: 'Сторона 1 — заклад, що направляє (дані МІС)', f: form.side1, src: 'МІС' }, { t: 'Сторона 2 — патологоанатомічне відділення (ЛІС)', f: form.side2, src: 'ЛІС' }]" :key="side.t" class="col-12 col-md-6">
            <div class="form-sheet">
              <div class="text-caption text-right">Форма № 014/о · наказ МОЗ № 435 від 29.05.2013</div>
              <div class="text-weight-bold text-center q-mb-sm">{{ side.t }}</div>
              <div v-for="(v, k) in side.f" :key="k" class="form-row">
                <div class="form-k">{{ k }}</div>
                <div class="form-v" :class="{ hl: highlight && v }" :title="highlight && v ? `Джерело: ${side.src}` : ''">{{ v || ' ' }}</div>
              </div>
            </div>
          </div>
          <div v-if="!form.signed" class="col-12 text-caption text-grey-7">До підпису висновку п. 18 і підписи в бланку порожні.</div>
        </div>
      </div>

      <!-- Події -->
      <div v-show="tab === 'events'" class="row q-col-gutter-md">
        <div class="col-12 col-md-6">
          <div class="medlink-card q-pa-sm">
            <div class="text-weight-bold q-mb-xs">Журнал етапів</div>
            <q-list dense separator>
              <q-item v-for="(l, i) in stageLogGroups" :key="i">
                <q-item-section><q-item-label>{{ l.title }}<span v-if="l.comment" class="text-grey-7"> · {{ l.comment }}</span></q-item-label>
                  <q-item-label caption>{{ l.by }}<span v-if="l.device"> · {{ l.device }}</span></q-item-label></q-item-section>
                <q-item-section side class="text-caption">{{ l.at | datetime }}</q-item-section>
              </q-item>
            </q-list>
          </div>
        </div>
        <div class="col-12 col-md-6">
          <div class="medlink-card q-pa-sm">
            <div class="text-weight-bold q-mb-xs">Події для МІС</div>
            <q-list dense separator>
              <q-item v-for="(e, i) in misEvents" :key="i">
                <q-item-section>
                  <q-item-label><q-badge :color="eventColor(e.code)" :label="e.title" /></q-item-label>
                  <q-item-label v-if="e.text" caption class="text-grey-9">{{ e.text }}</q-item-label>
                  <q-expansion-item dense dense-toggle label="Деталі" header-class="text-caption text-grey-7 q-pa-none"><pre class="event-json">{{ e.details }}</pre></q-expansion-item>
                </q-item-section>
                <q-item-section side top class="text-caption">{{ e.at | datetime }}</q-item-section>
              </q-item>
            </q-list>
            <div v-if="c.citoNotices.length" class="q-mt-sm">
              <div class="text-weight-bold">Повідомлення в операційну (CITO)</div>
              <div v-for="(n, i) in c.citoNotices" :key="i" class="text-caption">{{ n.at | datetime }} · {{ n.notifiedPerson }} ({{ citoMethodLabel(n.method) }}{{ n.readBack ? ', отримувач повторив висновок' : '' }}) · {{ n.minutesFromReceipt }} хв від надходження: {{ n.text }}</div>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- Діалоги -->
    <q-dialog v-model="addOpen">
      <q-card style="min-width: 480px">
        <q-card-section class="text-h6">Додаткові забарвлення та ІГХ</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-select v-model="add.blockId" dense outlined emit-value map-options :options="blockOptions" label="Блок *" />
          <q-select v-model="add.methods" dense outlined multiple use-chips emit-value map-options :options="methodOptions" label="Методики *" />
          <q-select v-model="add.source" dense outlined emit-value map-options :options="[{ label: 'Лабораторія', value: 'LAB' }, { label: 'МІС', value: 'MIS' }, { label: 'Мультидисциплінарна комісія', value: 'MDK' }]" label="Джерело дозамовлення" />
          <div class="text-caption text-grey-7">ІГХ подовжує строк на {{ prices.ihcExtraDays }} роб. дні; вартість перераховується; лікаря-направника буде повідомлено про додаткове дослідження.</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="purple-6" label="Дозамовити" :disable="!add.blockId || !add.methods.length" @click="doAdditional" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="prelimOpen">
      <q-card style="min-width: 460px">
        <q-card-section class="text-h6">Попередній висновок</q-card-section>
        <q-card-section>
          <q-input v-model="prelimText" dense outlined type="textarea" autogrow autofocus label="Текст попереднього висновку *" input-style="min-height: 6em" />
          <div class="text-caption text-grey-7 q-mt-xs">Це не остаточний діагноз. Висновок не потрапляє у виписку й не передається на мультидисциплінарну комісію.</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="amber-9" label="Надіслати в МІС" :disable="!prelimText.trim()" @click="doPreliminary" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="addendumOpen">
      <q-card style="min-width: 520px">
        <q-card-section class="text-h6">Доповнення до висновку (v{{ c ? c.versions.length + 1 : '' }})</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-select v-model="addendum.type" dense outlined emit-value map-options :options="addendumTypeOptions" label="Тип доповнення *" autofocus />
          <q-input v-model="addendum.text" dense outlined type="textarea" autogrow label="Текст доповнення *" />
          <q-input v-if="addendum.type === 'CORRECTION'" v-model="addendum.reason" dense outlined label="Обґрунтування виправлення *" />
          <q-input v-if="addendum.type === 'CONSULT'" v-model="addendum.consultantName" dense outlined label="Консультант (ПІБ, заклад) *" data-testid="consultantName" />
          <q-select v-model="addendum.icdoMorph" dense outlined clearable use-input fill-input hide-selected input-debounce="0" emit-value map-options :options="morphShown" label="МКХ-О морфологія (якщо змінюється)" placeholder="Код або назва"
                    menu-anchor="bottom left" menu-self="top left" :popup-content-style="{ maxHeight: '240px' }" @filter="(v, update) => update(() => { morphQuery = v; })">
            <template v-slot:no-option><q-item><q-item-section class="text-grey">Нічого не знайдено</q-item-section></q-item></template>
          </q-select>
          <q-checkbox v-model="addendum.changesDiagnosis" label="Змінює діагноз або стадію (буде сповіщено мультидисциплінарну комісію)" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" icon="draw" label="Підписати доповнення КЕП" :disable="!addendumValid" @click="doAddendum" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="citoOpen">
      <q-card style="min-width: 480px">
        <q-card-section class="text-h6">Повідомлення результату в операційну</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-banner v-if="cito.repeat" dense class="bg-orange-1 text-orange-10">Це повторне повідомлення: у журналі з'явиться ще один запис.</q-banner>
          <q-input v-model="cito.text" dense outlined type="textarea" autogrow autofocus label="Інтраопераційний висновок *" />
          <q-input v-model="cito.notifiedPerson" dense outlined label="Кому повідомлено *" placeholder="ПІБ, посада" />
          <q-select v-model="cito.method" dense outlined emit-value map-options :options="citoMethods" label="Спосіб *" />
          <q-checkbox v-model="cito.readBack" label="Отримувач повторив висновок вголос *" data-testid="readBack" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="negative" label="Зафіксувати повідомлення" :disable="!cito.readBack || !cito.text.trim() || !cito.notifiedPerson.trim()" @click="doCito" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="disposeOpen">
      <q-card style="min-width: 460px">
        <q-card-section class="text-h6">Утилізувати скельце {{ dispose.slide && dispose.slide.code }}</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input v-model="dispose.reason" dense outlined autofocus label="Причина утилізації *" placeholder="Наприклад, брак зрізу" @keyup.enter="doDispose" />
          <q-checkbox v-model="dispose.recreate" label="Створити нове скельце тієї ж методики" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="negative" label="Утилізувати" :disable="!dispose.reason.trim()" @click="doDispose" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="repeatOpen">
      <q-card style="min-width: 460px">
        <q-card-section class="text-h6">Повторне приготування</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input v-model="repeat.reason" dense outlined autofocus label="Причина *" placeholder="Наприклад, мало клітин або брак мазка" />
          <q-select v-model="repeat.method" dense outlined emit-value map-options :options="cytoMethodOptions" label="Методика" />
          <div class="text-caption text-grey-7">До справи буде додано одне нове скельце; перші скельця й журнал не змінюються.</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" label="Додати скельце" :disable="!repeat.reason.trim()" @click="doRepeat" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="dispatchOpen">
      <q-card style="min-width: 440px">
        <q-card-section class="text-h6">Надіслати висновок на пошту</q-card-section>
        <q-card-section>
          <q-input v-model="dispatch.address" dense outlined autofocus type="email" label="Адреса електронної пошти *" placeholder="name@example.org" :rules="[() => !dispatch.address || emailOk || 'Вкажіть адресу у форматі name@example.org']" lazy-rules @keyup.enter="doDispatch" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="teal-7" label="Надіслати" :disable="!emailOk" @click="doDispatch" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';
import roleMixin from '../../../mixins/roleMixin';

const LAB_STAGES = ['DECALC', 'PROC', 'EMB', 'SECT', 'STAIN'];
const STAGE_LABELS = { RCV: 'Прийнято', GROSS: 'Вирізка', DECALC: 'Декальцинація', COVER: 'Покриття скла', PROC: 'Проводка', EMB: 'Заливка', SECT: 'Мікротомія', STAIN: 'Фарбування', MICRO: 'Мікроскопія й опис', SIGNED: 'Підписано' };
const EVENT_TITLES = {
  RECEIVED: 'Матеріал прийнято', REJECTED: 'Відмова в прийомі', QUERY: 'Запит направнику', STAGE_CHANGED: 'Етап змінено', RESULT_PRELIMINARY: 'Попередній висновок',
  RESULT_FINAL: 'Підписано висновок', ADDENDUM: 'Доповнення до висновку', ADDITIONAL_STUDY_ORDERED: 'Дозамовлено дослідження', CRITICAL_RESULT: 'Критичний результат'
};
const CITO_METHODS = { PHONE: 'телефоном', INTERCOM: 'через інтерком', IN_PERSON: 'особисто' };
const PLACEHOLDER_RE = /\{[^{}\r\n]{1,30}\}/g;
const IHC_RESULTS = ['позитивний', 'слабопозитивний', 'негативний', 'сумнівний'];
const trim = v => String(v === null || v === undefined ? '' : v).trim();
const placeholdersOf = text => Array.from(new Set(trim(text).match(PLACEHOLDER_RE) || []));

export default {
  name: 'PathologyCasePage',
  mixins: [apiMixin, roleMixin],
  props: { id: { type: String, required: true } },
  data () {
    return {
      c: null, tab: this.$route.query.tab || 'ref', dict: { templates: [], topo: [], morph: [], stains: [], icd10: [], prices: {} }, form: null, highlight: true,
      gross: { macro: '', templateName: null, objects: [] }, prep: { objects: [], note: '' }, screenNote: '', rep: this.emptyReport(), microTemplate: null, scan: '', composing: false,
      topoQuery: '', morphQuery: '', icd10Query: '', repSnapshot: '',
      addOpen: false, add: { blockId: null, methods: [], source: 'LAB' }, prelimOpen: false, prelimText: '',
      addendumOpen: false, addendum: { type: null, text: '', reason: '', consultantName: '', icdoMorph: null, changesDiagnosis: false },
      citoOpen: false, cito: { text: '', notifiedPerson: '', method: 'PHONE', readBack: false, repeat: false },
      citoMethods: [{ label: 'Телефон', value: 'PHONE' }, { label: 'Інтерком', value: 'INTERCOM' }, { label: 'Особисто', value: 'IN_PERSON' }],
      disposeOpen: false, dispose: { slide: null, reason: '', recreate: false },
      repeatOpen: false, repeat: { reason: '', method: 'PAP' },
      dispatchOpen: false, dispatch: { address: '' },
      yesNo: [{ label: 'виявлено', value: true }, { label: 'не виявлено', value: false }],
      addendumTypes: [{ label: 'ІГХ / молекулярні дослідження', value: 'IHC' }, { label: 'Перегляд препаратів', value: 'REVIEW' }, { label: 'Виправлення з обґрунтуванням', value: 'CORRECTION' }, { label: 'Висновок зовнішнього консультанта', value: 'CONSULT' }],
      staff: { pathologists: [], assistants: [], current: null }, performerId: null, sectCount: null,
      icd10Rules: [v => !v || /^[A-Z]\d{2}(\.\d{1,2})?$/i.test(String(v).trim()) || 'Код у форматі C50.4'],
      remainderOptions: [{ label: 'Залишку немає', value: 'NONE' }, { label: 'Залишок збережено', value: 'KEPT' }, { label: 'Залишок утилізовано', value: 'DISCARDED' }]
    };
  },
  computed: {
    prices () { return this.dict.prices || {}; },
    pageTitle () { return this.c ? `${this.isCyto ? 'Справа цитології' : 'Справа'} ${this.c.caseNo || this.c.orderNumber}` : (this.isCyto ? 'Справа цитології' : 'Справа ПГД'); },
    subtitle () {
      if (!this.c) return '';
      // той самий текст двічі (джерело й відділення «Поліклініка») не повторюємо
      // відділення з МІС може вже містити назву джерела («Поліклініка · гінеколог») — розбираємо на частини й прибираємо повтори
      const parts = [this.c.patient.name, this.c.kindLabel, this.c.sourceLabel].concat(String(this.c.department || '').split('·')).map(trim);
      return parts.filter((v, i, a) => v && a.indexOf(v) === i).join(' · ');
    },
    crumbs () {
      const cyto = this.c ? this.isCyto : this.$route.query.kind === 'cyto';
      return [{ label: cyto ? 'Цитологія' : 'Патоморфологія' }, cyto ? { label: 'Журнал цитології', to: { name: 'lab-cyto-cases' } } : { label: 'Журнал справ', to: { name: 'lab-path-cases' } }, { label: this.c ? this.c.caseNo || '' : '' }];
    },
    signed () { return this.c && this.c.stage === 'SIGNED'; },
    labStage () { return this.c && LAB_STAGES.includes(this.c.stage); },
    // ролі: що може поточний співробітник (джерело істини — серверні політики)
    canPath () { return this.canDo('LAB_DOCTOR'); },
    canLab () { return this.canDo('LAB_TECHNICIAN'); },
    canScreen () { return this.canDo('LAB_TECHNICIAN', 'LAB_DOCTOR'); },
    canDispatch () { return this.canDo('LAB_DOCTOR', 'REGISTRAR', 'LAB_TECHNICIAN'); },
    citoTarget () { return this.c && this.c.citoTimer ? this.c.citoTimer.targetMinutes : 0; },
    citoLate () { return !!(this.c && this.c.citoTimer && this.c.citoTimer.minutes > this.citoTarget); },
    // повідомлення вже зафіксовано — банер спокійний (зелений) навіть із запізненням: червоним лишається лише таймер, що іде
    citoClass () { return this.c.citoTimer.notifiedAt ? 'done' : (this.citoLate ? 'late' : ''); },
    citoHasResult () { return !!(this.c && (this.c.preliminary || trim(this.rep.conclusionText) || trim(this.c.report && this.c.report.conclusionText) || ['MICRO', 'SIGNED'].includes(this.c.stage))); },
    citoCanNotify () { return this.canPath && this.citoHasResult; },
    citoHint () {
      if (!this.canPath) return this.roleHint('LAB_DOCTOR');
      if (!this.citoHasResult) return 'Спершу внесіть висновок на вкладці «Опис і висновок» або надішліть попередній висновок';
      return '';
    },
    grossEditable () { return this.c && ['RCV', 'GROSS'].includes(this.c.stage) && this.c.intakeStatus === 'ACCEPTED' && this.c.kind !== 'CONSULT'; },
    grossLocked () { return !this.grossEditable || !this.canPath; },
    grossBlocks () { return this.gross.objects.reduce((n, o) => n + o.blocks.length, 0); },
    grossPieces () { return this.gross.objects.reduce((n, o) => n + o.blocks.reduce((m, b) => m + (Number(b.pieces) || 0), 0), 0); },
    grossPrice () { const n = this.grossBlocks; return (this.c && this.c.kind === 'CITO' ? this.prices.cito : 0) + (n ? this.prices.firstBlock + this.prices.nextBlock * (n - 1) : 0); },
    macroOpen () { return placeholdersOf(this.gross.macro); },
    grossMissing () {
      const m = [];
      if (!this.grossBlocks) m.push('хоча б один блок');
      if (!this.gross.pathologistId) m.push('лікар-патолог');
      if (!this.gross.assistantId) m.push('асистент');
      if (!trim(this.gross.macro)) m.push('макроопис');
      if (this.macroOpen.length) m.push(`заготовки в макрописі (${this.macroOpen.join(', ')})`);
      return m;
    },
    canFinishGross () { return this.canPath && !this.grossMissing.length; },
    canAdditional () { return this.c && !['RCV', 'GROSS'].includes(this.c.stage); },
    isCyto () { return !!this.c && this.c.kind === 'CYTO'; },
    advanceLabel () { return this.isCyto ? `${this.stageLabel(this.c.stage)} виконано` : `${this.stageLabel(this.c.stage)}: виконано для всіх касет`; },
    cytoDict () { return this.dict.cytology || { materials: [], adequacy: [], categories: [], slidePrice: 0 }; },
    cytoPrice () { return this.cytoDict.slidePrice; },
    adequacyOptions () { return this.cytoDict.adequacy.map(a => ({ label: a.name, value: a.code })); },
    // категорії залежать від виду матеріалу (гінекологічний мазок — шкала шийки матки; пунктат, виділення — загальні); вибрану раніше не ховаємо
    categoryOptions () { return this.cytoDict.categories.filter(a => !a.materials || !this.c || !this.c.cyto || a.materials.includes(this.c.cyto.material) || a.code === this.rep.cytoCategory).map(a => ({ label: `${a.name} (${a.code})${a.high ? ' — критично' : ''}`, value: a.code })); },
    cytoMethodOptions () { return this.dict.stains.filter(s => !s.isIhc).map(s => ({ label: s.name, value: s.code })).concat(this.dict.stains.some(s => s.code === 'PAP') ? [] : [{ label: 'Папаніколау', value: 'PAP' }]); },
    prepSum () { return this.prep.objects.reduce((n, o) => n + o.slides.reduce((m, s) => m + ((this.dict.stains.find(x => x.code === s.method) || {}).price || this.cytoPrice), 0), 0); },
    prepTotal () { return this.prep.objects.reduce((n, o) => n + o.slides.length, 0); },
    reportOpen () { return Array.from(new Set(placeholdersOf(this.rep.micro).concat(placeholdersOf(this.rep.conclusionText)))); },
    ihcPending () { return this.rep.ihc.filter(i => !trim(i.resultText)).map(i => i.marker); },
    signMissing () {
      const m = [];
      if (this.isCyto) {
        if (!this.rep.adequacy) m.push('адекватність матеріалу');
        else if (this.rep.adequacy !== 'UNSATISFACTORY' && !this.rep.cytoCategory) m.push('категорія висновку');
      } else {
        if (!this.rep.icdoTopo) m.push('топографія МКХ-О');
        if (!this.rep.icdoMorph) m.push('морфологія МКХ-О');
      }
      if (!trim(this.rep.micro)) m.push(this.isCyto ? 'опис препарату' : 'мікроскопічний опис');
      if (!trim(this.rep.conclusionText)) m.push('висновок');
      if (this.reportOpen.length) m.push(`заготовки в тексті (${this.reportOpen.join(', ')})`);
      if (this.rep.ihc.length && this.ihcPending.length === this.rep.ihc.length) m.push(`результати ІГХ (${this.ihcPending.join(', ')})`);
      return m;
    },
    canSign () { return this.canPath && !this.signMissing.length; },
    templateOptions () { return this.dict.templates.map(t => ({ label: t.name, value: t.name })); },
    topoOptions () { return this.dict.topo.map(t => ({ label: `${t.code} ${t.name}`, value: t.code })); },
    morphOptions () { return this.dict.morph.map(t => ({ label: `${t.code} ${t.name}`, value: t.code })); },
    // стартовий набір поширених кодів МКХ-10 (не повний класифікатор): інший код вводиться вручну й перевіряється за форматом
    icd10Options () { return (this.dict.icd10 || []).map(t => ({ label: `${t.code} ${t.name}`, value: t.code })); },
    icd10Shown () { return this.filterOptions(this.icd10Options, this.icd10Query); },
    topoShown () { return this.filterOptions(this.topoOptions, this.topoQuery); },
    morphShown () { return this.filterOptions(this.morphOptions, this.morphQuery); },
    blockOptions () { return this.c ? this.c.objects.flatMap(o => o.blocks.map(b => ({ label: `${b.code}${o.description ? ' — ' + o.description : ''}${b.status === 'ISSUED' ? ' (видано)' : ''}`, value: b.id, disable: b.status === 'ISSUED' }))) : []; },
    maxMethods () { return this.dict.maxMethodsPerBlock || 5; },
    gross5Options () { return this.dict.stains.map(s => ({ label: s.isIhc ? `ІГХ ${s.code}` : s.name, value: s.code })); },
    methodOptions () { return this.dict.stains.filter(s => s.code !== 'HE').map(s => ({ label: s.isIhc ? `ІГХ ${s.code} — ${s.name}` : s.name, value: s.code })); },
    addendumTypeOptions () { return this.isCyto ? this.addendumTypes.filter(t => t.value !== 'IHC') : this.addendumTypes; },
    addendumValid () {
      const a = this.addendum;
      return !!a.type && !!trim(a.text) && !(a.type === 'CORRECTION' && !trim(a.reason)) && !(a.type === 'CONSULT' && !trim(a.consultantName));
    },
    emailOk () { return /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(trim(this.dispatch.address)); },
    stageLogGroups () {
      if (!this.c) return [];
      const blockCode = {}; const slideCode = {};
      this.c.objects.forEach(o => o.blocks.forEach(b => { blockCode[b.id] = b.code; b.slides.forEach(s => { slideCode[s.id] = s.code; }); }));
      const groups = [];
      (this.c.stageLog || []).forEach(l => {
        const t = new Date(l.performedAt).getTime();
        let comment = l.comment || '';
        if (l.stage === 'RCV') comment = comment.replace(/^Прийнято\s+/i, '');
        // службові коди джерела в тексті журналу → українські слова; «Підписано · Підписано КЕП» → «Підписано · КЕП»
        comment = comment.replace(/Дозамовлено \(LAB\)/, 'Дозамовлено лабораторією').replace(/Дозамовлено \(MIS\)/, 'Дозамовлено від МІС')
          .replace(/Дозамовлено \(MDK\)/, 'Дозамовлено від мультидисциплінарної комісії').replace(/Дозамовлено \(лабораторією\)/, 'Дозамовлено лабораторією')
          .replace(/Дозамовлено \(від ([^)]+)\)/, 'Дозамовлено від $1').replace(/^Підписано КЕП:/i, 'КЕП:');
        const target = l.slideId && slideCode[l.slideId] ? { text: slideCode[l.slideId], slide: true } : l.blockId && blockCode[l.blockId] ? { text: blockCode[l.blockId], slide: false } : null;
        const last = groups[groups.length - 1];
        if (target && last && last.stage === l.stage && last.by === l.performedByName && last.comment === comment && last.slide === target.slide && Math.abs(t - last.t) < 120000) {
          if (!last.targets.includes(target.text)) last.targets.push(target.text);
          return;
        }
        groups.push({ stage: l.stage, label: l.stageLabel, comment, by: l.performedByName, device: l.device, at: l.performedAt, t, slide: target ? target.slide : false, targets: target ? [target.text] : [] });
      });
      return groups.map(g => ({ ...g, title: g.targets.length ? `${g.label} · ${g.slide ? 'скельця' : this.isCyto ? 'препарати' : 'блоки'} ${g.targets.join(', ')}` : g.label }));
    },
    misEvents () { return ((this.c && this.c.events) || []).map(e => this.eventView(e)); }
  },
  watch: {
    id () { this.repSnapshot = ''; this.load(true); },
    tab (v) {
      if (v === 'print') this.loadForm();
      this.apiError = null; // застаріла помилка не повинна «висіти» на іншій вкладці
      this.regrow();
    }
  },
  async created () {
    this.dict = await this.$api.pathDictionaries().catch(() => this.dict);
    this.staff = await this.$api.pathStaff().catch(() => this.staff);
    this.performerId = this.defaultStaff('assistants');
    await this.load();
    if (this.tab === 'ref' && this.c && this.c.kind === 'CYTO') this.tab = this.c.stage === 'RCV' ? 'prep' : this.c.stage === 'STAIN' ? 'blocks' : this.c.stage === 'MICRO' ? 'report' : 'ref';
    else if (this.tab === 'ref' && this.c) this.tab = this.c.stage === 'RCV' || this.c.stage === 'GROSS' ? 'gross' : LAB_STAGES.includes(this.c.stage) ? 'blocks' : this.c.stage === 'MICRO' ? 'report' : 'ref';
    if (this.tab === 'print') this.loadForm();
  },
  methods: {
    emptyReport () { return { adequacy: null, cytoCategory: null, recommendation: '', micro: '', conclusionText: '', icd10: '', icdoTopo: null, icdoMorph: null, grade: null, pt: '', pn: '', lnTotal: null, lnPositive: null, margin: '', marginClass: null, marginMm: '', lvi: null, pni: null, ihc: [] }; },
    stageLabel (s) { return STAGE_LABELS[s] || s; },
    staffOptions (kind) { return (this.staff[kind] || []).map(e => ({ label: e.name, value: e.id })); },
    defaultStaff (kind) { const e = (this.staff[kind] || []).find(x => x.id === this.staff.current); return e ? e.id : null; },
    methodCode (name) { const m = this.dict.stains.find(s => s.name === name); return m ? m.code : null; },
    addendumLabel (t) { return (this.addendumTypes.find(x => x.value === t) || { label: t }).label; },
    orderedFromLabel (s) { return { MIS: 'МІС', MDK: 'мультидисциплінарної комісії', LAB: 'лабораторії' }[s] || s; },
    citoMethodLabel (m) { const t = CITO_METHODS[m] || m; return t.charAt(0).toUpperCase() + t.slice(1); },
    eventColor (e) { return { RECEIVED: 'primary', REJECTED: 'negative', CRITICAL_RESULT: 'negative', RESULT_FINAL: 'positive', ADDENDUM: 'teal-6', RESULT_PRELIMINARY: 'amber-9', ADDITIONAL_STUDY_ORDERED: 'purple-5' }[e] || 'grey-7'; },
    icd10Filter (v, update) { update(() => { this.icd10Query = v; }); },
    /** Код, якого немає в стартовому наборі, приймаємо лише у форматі МКХ-10 (латинська літера і дві цифри, далі крапка й до двох цифр) */
    icd10New (v, done) {
      const code = trim(v).toUpperCase();
      if (/^[A-Z]\d{2}(\.\d{1,2})?$/.test(code)) done(code, 'add-unique');
      else this.$q.notify({ type: 'warning', message: 'Код у форматі C50.4' });
    },
    /** Поле з випадним списком підсуваємо до середини екрана, щоб список відкривався вниз, а не накладався на шапку */
    revealField (e) {
      const el = e && e.target && e.target.closest ? e.target.closest('.q-field') : null;
      if (el && el.scrollIntoView) el.scrollIntoView({ block: 'center' });
    },
    /** Скельце забарвлено: «Готово», або первинне скельце при завершеному фарбуванні (те саме правило перевіряє сервер) */
    slideStained (s) {
      return s.status === 'READY' || (s.status === 'IN_WORK' && !s.isIhc && !s.replacesSlideId && !s.orderedFrom && !!this.c && this.c.stage === 'STAIN');
    },
    slideCode (id) { const s = this.c && this.c.objects.flatMap(o => o.blocks).flatMap(b => b.slides).find(x => x.id === id); return s ? s.code : '—'; },
    filterOptions (options, query) {
      const q = trim(query).toLowerCase();
      return q ? options.filter(o => o.label.toLowerCase().includes(q)) : options;
    },
    ihcResultOptions (row) {
      const cur = trim(row.resultText);
      return cur && !IHC_RESULTS.includes(cur) ? IHC_RESULTS.concat(cur) : IHC_RESULTS;
    },
    /** Етап касети: що вже виконано і що очікується далі (а не лише останній виконаний) */
    blockStageInfo (b) {
      if (!this.c || ['RCV', 'GROSS'].includes(this.c.stage)) return { label: '', next: '' };
      const seq = this.isCyto ? ['STAIN'] : b.track === 'DECALC' ? ['DECALC', 'PROC', 'EMB', 'SECT', 'STAIN'] : ['PROC', 'EMB', 'SECT', 'STAIN'];
      const idx = b.stage ? seq.indexOf(b.stage) : -1;
      const next = seq[idx + 1];
      if (!b.stage) return { label: `Очікує: ${this.stageLabel(seq[0])}`, next: '' };
      return { label: `Виконано: ${this.stageLabel(b.stage)}`, next: next ? `Далі: ${this.stageLabel(next)}` : 'Далі: мікроскопія й опис' };
    },
    eventView (e) {
      let p = {};
      try { p = JSON.parse(e.payload || '{}') || {}; } catch (x) { p = {}; }
      const fmt = d => (d ? this.$options.filters.datetime(d) : '');
      const short = t => { const s = trim(t); return s.length > 160 ? s.slice(0, 157) + '…' : s; };
      let title = EVENT_TITLES[e.event] || 'Подія';
      let text = '';
      switch (e.event) {
        case 'RECEIVED': text = `Справа ${p.caseNo || ''}${p.slaDueAt ? `, строк до ${fmt(p.slaDueAt)}` : ''}`; break;
        case 'REJECTED': text = [p.reason, p.comment].filter(Boolean).join(' — '); break;
        case 'QUERY': text = short(p.text); break;
        case 'STAGE_CHANGED': title = `Етап змінено: ${p.stageLabel || this.stageLabel(p.stage)}`; break;
        case 'RESULT_PRELIMINARY': text = short(p.text); break;
        case 'RESULT_FINAL': title = `Підписано висновок v${p.version || 1}`; text = short(p.conclusionText); break;
        case 'ADDENDUM': {
          const a = p.payload || {};
          title = `Доповнення v${a.version || ''}`.trim();
          text = [a.changesDiagnosis ? 'змінює діагноз — мультидисциплінарну комісію сповіщено' : '', short(a.conclusionText)].filter(Boolean).join('. ');
          break;
        }
        case 'ADDITIONAL_STUDY_ORDERED': text = `Блок ${p.block || ''}: ${(p.methods || []).join(', ')}`; break;
        case 'CRITICAL_RESULT':
          // одна подія МІС для трьох різних ситуацій: називаємо кожну своїм іменем і пояснюємо причину
          if (p.cito) { title = 'Результат повідомлено в операційну'; text = short(p.text); }
          else if (p.reason === 'unexpected-malignancy') {
            title = 'Критична знахідка: злоякісна пухлина';
            text = `Клінічний діагноз у направленні${p.clinicalDiagnosis ? ` (${trim(p.clinicalDiagnosis).split(' ')[0]})` : ''} не передбачав злоякісного новоутворення. Направнику потрібно підтвердити ознайомлення. ${short(String(p.text || '').replace(/^(Неочікувана злоякісність|Виявлено злоякісну пухлину[^:]*):\s*/i, ''))}`.trim();
          } else if (p.reason === 'cytology-high-grade') { title = 'Критична знахідка: високий ступінь підозри'; text = short(String(p.text || '').replace(/^Цитологія:\s*/i, '')); }
          else text = short(p.text);
          break;
        default: break;
      }
      let details = '';
      try { details = JSON.stringify(p, null, 1); } catch (x) { details = String(e.payload || ''); }
      return { code: e.event, title, text, details, at: e.at };
    },
    /** Автовисота textarea не рахується, поки вкладка прихована (v-show): перераховуємо після показу */
    regrow () {
      this.$nextTick(() => {
        if (!this.$el || !this.$el.querySelectorAll) return;
        this.$el.querySelectorAll('.q-textarea--autogrow textarea').forEach(el => {
          if (el.offsetParent === null) return;
          el.style.height = '1px';
          el.style.height = `${el.scrollHeight}px`;
        });
      });
    },
    parseMargin (m) {
      const s = trim(m);
      if (!s) return { marginClass: null, marginMm: '' };
      const cls = (s.match(/\bR[0-2X]\b/i) || [])[0];
      const mm = (s.match(/(\d+(?:[.,]\d+)?)\s*мм/i) || [])[1];
      if (!cls && !mm) return { marginClass: null, marginMm: s };
      return { marginClass: cls ? cls.toUpperCase() : null, marginMm: mm || '' };
    },
    marginText () {
      const mm = trim(this.rep.marginMm);
      const part = !mm ? '' : /^\d+([.,]\d+)?$/.test(mm) ? `${mm} мм` : mm;
      return [this.rep.marginClass, part].filter(Boolean).join(', ');
    },
    async load (force) {
      const c = await this.callApi(() => this.$api.pathCase(this.id));
      if (c) this.apply(c, force === true);
    },
    /**
     * Застосувати відповідь сервера. Незбережені правки висновку не затираємо: оцінка скринінгу, покриття скельця, дозамовлення
     * та інші дії оновлюють картку, але не очищають введені поля. Повна заміна — лише після власного збереження (force).
     */
    apply (c, force) {
      const keepRep = !force && this.repSnapshot !== '' && JSON.stringify(this.rep) !== this.repSnapshot;
      const keptRep = keepRep ? this.rep : null;
      this.applyCase(c);
      if (keptRep) {
        // нові ІГХ-скельця додаємо до рядків, решту не чіпаємо
        this.rep.ihc.filter(i => !keptRep.ihc.some(k => k.marker === i.marker)).forEach(i => keptRep.ihc.push(i));
        this.rep = keptRep;
      }
      if (!keptRep) this.repSnapshot = JSON.stringify(this.rep);
    },
    applyCase (c) {
      this.c = c;
      const caseTitle = c.kind === 'CYTO' ? 'Справа цитології' : 'Справа ПГД';
      document.title = `${caseTitle} ${c.caseNo || c.orderNumber} — MedLink LIS`;
      this.prep = { note: '', objects: c.objects.map(o => ({ objectId: o.id, mark: o.mark, description: o.description, slides: c.kind === 'CYTO' && !(c.cyto && c.cyto.prepared) ? [{ method: 'PAP' }] : [] })) };
      // шаблон вибрано, а макроопис порожній — підставляємо текст шаблону одразу
      let macro = c.macro || '';
      if (!trim(macro) && c.templateName) { const t = this.dict.templates.find(x => x.name === c.templateName); if (t) macro = t.macro || ''; }
      this.gross = {
        macro, templateName: c.templateName,
        pathologistId: (c.pathologist && c.pathologist.id) || this.defaultStaff('pathologists'), assistantId: (c.assistant && c.assistant.id) || null,
        objects: c.objects.map(o => ({
          objectId: o.id, mark: o.mark, letter: o.letter, description: o.description, remainderState: o.remainderState || 'NONE', remainderNote: o.remainderNote || '',
          blocks: o.blocks.filter(b => !b.isExternal).map(b => ({ pieces: b.pieces, track: b.track || 'STANDARD', methods: b.slides && b.slides.length ? b.slides.filter(x => x.status !== 'DISPOSED' && !x.isIhc).map(x => this.methodCode(x.stainMethod)).filter(Boolean).slice(0, 5) : ['HE'] }))
        }))
      };
      const r = c.report || {};
      const rep = this.emptyReport();
      Object.keys(rep).forEach(k => { if (r[k] !== undefined && r[k] !== null) rep[k] = r[k]; });
      Object.assign(rep, this.parseMargin(r.margin));
      // ІГХ-рядки: збережені результати + маркери з ІГХ-скелець справи
      const ihc = (r.ihc || []).map(i => ({ ...i }));
      c.objects.flatMap(o => o.blocks).flatMap(b => b.slides).filter(s => s.isIhc && s.marker && s.status !== 'DISPOSED').forEach(s => {
        if (!ihc.some(i => i.marker === s.marker)) ihc.push({ marker: s.marker, slideId: s.id, resultText: '', score: '' });
      });
      rep.ihc = ihc;
      this.rep = rep;
      if (this.tab === 'print') this.loadForm();
      this.regrow();
    },
    async loadForm () { this.form = await this.callApi(() => this.$api.pathForm014(this.c.id), { silent: true }); },
    applyMacroTemplate (name) {
      const t = this.dict.templates.find(x => x.name === name);
      if (t) { this.gross.macro = t.macro; this.regrow(); }
    },
    applyMicroTemplate (name) {
      const t = this.dict.templates.find(x => x.name === name);
      if (!t) return;
      this.rep.micro = t.micro;
      if (placeholdersOf(t.micro).length) this.$q.notify({ type: 'info', message: 'Заповніть заготовки у фігурних дужках — з ними висновок не підписати', timeout: 5000 });
      this.regrow();
    },
    async saveGross (finish) {
      const body = {
        macro: this.gross.macro, templateName: this.gross.templateName, finish, pathologistId: this.gross.pathologistId, assistantId: this.gross.assistantId,
        objects: this.gross.objects.map(o => ({
          objectId: o.objectId, remainderState: o.remainderState, remainderNote: o.remainderNote || null,
          blocks: o.blocks.map(b => ({ pieces: Number(b.pieces) || 1, track: b.track || 'STANDARD', methods: (b.methods && b.methods.length ? b.methods : ['HE']) }))
        }))
      };
      const c = await this.callApi(() => this.$api.pathGross(this.c.id, body), { success: finish ? 'Вирізку завершено: скельця створено за методиками, вартість перераховано' : 'Збережено' });
      if (c) { this.apply(c); if (finish) this.tab = 'blocks'; }
    },
    async prepare () {
      const body = { note: this.prep.note || null, objects: this.prep.objects.filter(o => o.slides.length).map(o => ({ objectId: o.objectId, slides: o.slides.map(s => ({ method: s.method })) })) };
      const c = await this.callApi(() => this.$api.pathPrepare(this.c.id, body), { success: 'Препарати приготовано, справа передана на фарбування' });
      if (c) { this.apply(c); this.tab = 'blocks'; }
    },
    repeatPrep () { this.repeat = { reason: '', method: 'PAP' }; this.repeatOpen = true; },
    async doRepeat () {
      const before = new Set(this.c.objects.flatMap(o => o.blocks).flatMap(b => b.slides).map(s => s.code));
      const c = await this.callApi(() => this.$api.pathPrepareRepeat(this.c.id, { reason: this.repeat.reason.trim(), method: this.repeat.method || 'PAP' }));
      if (!c) return;
      this.repeatOpen = false;
      this.apply(c);
      const added = c.objects.flatMap(o => o.blocks).flatMap(b => b.slides).map(s => s.code).filter(code => !before.has(code));
      this.$q.notify({ type: 'positive', message: added.length ? `Додано скельце ${added.join(', ')}` : 'Скельце додано' });
    },
    async screen (assessment) {
      const prev = this.c.cyto && this.c.cyto.screening;
      if (prev && prev.assessment !== assessment) {
        const ok = await new Promise(resolve => this.$q.dialog({ title: 'Змінити оцінку скринінгу?', message: `Збережено: «${prev.assessment === 'SUSPECT' ? 'Підозра' : 'Норма'}» (${prev.by}). Замінити оцінку?`, cancel: { label: 'Скасувати', flat: true }, ok: { label: 'Замінити', color: 'primary' }, persistent: true }).onOk(() => resolve(true)).onCancel(() => resolve(false)).onDismiss(() => resolve(false)));
        if (!ok) return;
      }
      const c = await this.callApi(() => this.$api.pathScreening(this.c.id, { assessment, note: this.screenNote || null }), { success: 'Оцінку скринінгу збережено' });
      if (c) { this.screenNote = ''; this.apply(c); }
    },
    async doScan () {
      const code = (this.scan || '').trim();
      if (!code) return;
      const r = await this.callApi(() => this.$api.pathScan({ barcode: code }));
      this.scan = '';
      if (r) { this.$q.notify({ type: 'positive', message: `${r.blockCode}: ${r.stageLabel}` }); await this.load(); }
    },
    async advance () {
      const body = { stage: this.c.stage, performerId: this.performerId || null, count: this.c.stage === 'SECT' && this.sectCount ? this.sectCount : null };
      const c = await this.callApi(() => this.$api.pathAdvance(this.c.id, body), { success: `${this.stageLabel(this.c.stage)}: виконано` });
      if (c) this.apply(c);
    },
    async coverSlide (s) {
      const r = await this.callApi(() => this.$api.pathScan({ barcode: s.barcode, stage: 'COVER', performerId: this.performerId || null }), { success: `Скельце ${s.code}: покрито` });
      if (r) await this.load();
    },
    disposeSlide (s) { this.dispose = { slide: s, reason: '', recreate: false }; this.disposeOpen = true; },
    async doDispose () {
      const d = this.dispose;
      if (!d.slide || !trim(d.reason)) return;
      const r = await this.callApi(() => this.$api.pathDisposeSlide(d.slide.id, { reason: d.reason.trim(), recreate: d.recreate }), { success: d.recreate ? 'Скельце утилізовано, створено нове' : 'Скельце утилізовано' });
      if (r) { this.disposeOpen = false; await this.load(); }
    },
    openDispatch () {
      this.dispatch = { address: (this.c.referral && this.c.referral.resultEmail) || this.c.resultEmail || '' };
      this.dispatchOpen = true;
    },
    async doDispatch () {
      if (!this.emailOk) return;
      const r = await this.callApi(() => this.$api.pathDispatch(this.c.id, { address: trim(this.dispatch.address) }), { success: 'Висновок поставлено в розсилку' });
      if (r) { this.dispatchOpen = false; await this.load(); }
    },
    async slideReady (s) { if (await this.callApi(() => this.$api.pathSlideReady(s.id))) await this.load(); },
    openAdditional (markers) {
      const blocks = this.blockOptions.filter(b => !b.disable);
      this.add = { blockId: blocks.length ? blocks[0].value : null, methods: markers ? [...markers] : [], source: 'LAB' };
      this.addOpen = true;
    },
    async doAdditional () {
      const c = await this.callApi(() => this.$api.pathAdditional(this.c.id, this.add), { success: 'Дозамовлено; строк і вартість перераховано' });
      if (c) { this.addOpen = false; this.apply(c); }
    },
    reportBody () {
      const { marginClass, marginMm, ...rest } = this.rep; // клас R і відстань зберігаємо одним полем «край резекції»
      return { ...rest, margin: this.marginText(), ihc: this.rep.ihc.map(i => ({ marker: i.marker, slideId: i.slideId, resultText: i.resultText || '', score: i.score || '' })) };
    },
    async saveReport (silent) {
      const c = await this.callApi(() => this.$api.pathSaveReport(this.c.id, this.reportBody()), { success: silent ? null : 'Висновок збережено' });
      if (c) this.apply(c, true);
      return c;
    },
    async compose () {
      this.composing = true;
      try {
        const c = await this.saveReport(true);
        if (!c) return;
        if (c.composedText) { this.rep.conclusionText = c.composedText; this.regrow(); this.$q.notify({ type: 'positive', message: 'Текст висновку зібрано з введених полів — перегляньте й за потреби відредагуйте' }); }
        else this.$q.notify({ type: 'warning', message: 'Нема з чого зібрати текст: заповніть опис і класифікацію' });
      } finally { this.composing = false; }
    },
    async doPreliminary () {
      const c = await this.callApi(() => this.$api.pathPreliminary(this.c.id, this.prelimText.trim()), { success: 'Попередній висновок надіслано в МІС' });
      if (c) { this.prelimOpen = false; this.prelimText = ''; this.apply(c); }
    },
    async signer () {
      const s = await this.$api.ehealthSession();
      return s && s.testCertificate;
    },
    async sign () {
      if (!this.canSign) return;
      if (!(await this.saveReport(true))) return;
      const cert = await this.signer();
      if (!cert) { this.$q.notify({ type: 'negative', message: 'Сертифікат КЕП недоступний — підписати неможливо' }); return; }
      const pending = this.ihcPending;
      const message = `Підписати висновок справи ${this.c.caseNo} сертифікатом ${cert.name} (${cert.certSerial})?`
        + (pending.length ? ` Результатів ІГХ немає для: ${pending.join(', ')} — їх буде оформлено доповненням.` : '');
      this.$q.dialog({ title: 'Підпис КЕП', message, cancel: { label: 'Скасувати', flat: true }, ok: { label: 'Підписати', color: 'primary' }, persistent: true }).onOk(async () => {
        const r = await this.callApi(() => this.$api.pathSign(this.c.id, cert, { acknowledgePendingIhc: pending.length > 0 }), { success: 'Висновок підписано й передано в МІС' });
        if (r) { (r.warnings || []).forEach(w => this.$q.notify({ type: 'warning', message: w, timeout: 6000 })); this.apply(r.case); }
      });
    },
    openAddendum () { this.addendum = { type: null, text: '', reason: '', consultantName: '', icdoMorph: null, changesDiagnosis: false }; this.addendumOpen = true; },
    async doAddendum () {
      const cert = await this.signer();
      if (!cert) { this.$q.notify({ type: 'negative', message: 'Сертифікат КЕП недоступний — підписати неможливо' }); return; }
      const body = { ...this.addendum, icdoMorph: this.addendum.icdoMorph || null, ihc: this.addendum.type === 'IHC' ? this.reportBody().ihc.filter(i => i.resultText) : null, signer: cert };
      const c = await this.callApi(() => this.$api.pathAddendum(this.c.id, body), { success: 'Доповнення підписано й передано в МІС' });
      if (c) { this.addendumOpen = false; this.apply(c); }
    },
    async openCito () {
      const t = this.c.citoTimer;
      let repeat = false;
      if (t && t.notifiedAt) {
        const ok = await new Promise(resolve => this.$q.dialog({
          title: 'Повторити повідомлення?', message: `Результат уже повідомлено в операційну ${this.$options.filters.datetime(t.notifiedAt)}. Зафіксувати ще одне повідомлення?`,
          cancel: { label: 'Скасувати', flat: true }, ok: { label: 'Повторити', color: 'negative' }, persistent: true
        }).onOk(() => resolve(true)).onCancel(() => resolve(false)).onDismiss(() => resolve(false)));
        if (!ok) return;
        repeat = true;
      }
      const text = trim(this.rep.conclusionText) || trim(this.c.preliminary && this.c.preliminary.conclusionText) || this.cito.text || '';
      this.cito = { text, notifiedPerson: '', method: 'PHONE', readBack: false, repeat };
      this.citoOpen = true;
    },
    async doCito () {
      const body = { text: this.cito.text.trim(), notifiedPerson: this.cito.notifiedPerson.trim(), method: this.cito.method, readBack: this.cito.readBack, repeat: this.cito.repeat };
      const c = await this.callApi(() => this.$api.pathCitoNotice(this.c.id, body), { success: 'Повідомлення зафіксовано, критичний результат передано в МІС' });
      if (c) { this.citoOpen = false; this.apply(c); }
    }
  }
};
</script>

<style scoped>
/* неактивна кнопка має явно відрізнятися від активної (сірий, без насиченого кольору) */
.q-btn.disabled { opacity: 0.45 !important; filter: grayscale(0.85); cursor: not-allowed; }
.stages { gap: 3px; overflow-x: auto; }
.stage { flex: 1; min-width: 86px; text-align: center; font-size: 11px; padding: 5px 4px; border-radius: 4px; background: #eef1f5; color: #6b7785; white-space: nowrap; }
.stage.done { background: #e3f2e5; color: #2e7d32; }
.stage.cur { background: #fff3e0; color: #e65100; font-weight: 700; }
.cito-bar { background: #fff3e0; border-radius: 6px; padding: 6px 10px; color: #bf360c; }
.cito-bar.late { background: #ffebee; color: #c62828; }
.cito-bar.done { background: #e8f5e9; color: #2e7d32; }
.block-chip { display: flex; align-items: center; gap: 6px; border: 1px solid #dde3ea; border-radius: 6px; padding: 4px 6px; }
.kv td:first-child { width: 190px; color: #5f6b7a; white-space: nowrap; }
.form-sheet { border: 1px solid #9aa8b8; border-radius: 4px; padding: 10px 12px; background: #fff; font-size: 12px; }
.form-row { display: flex; border-bottom: 1px solid #dde3ea; padding: 3px 0; }
.form-k { width: 42%; color: #5f6b7a; padding-right: 6px; }
.form-v { flex: 1; white-space: pre-wrap; }
.form-v.hl { background: #fff8c4; }
.legend-hl { display: inline-block; width: 12px; height: 12px; background: #fff8c4; border: 1px solid #e0d17a; vertical-align: middle; }
.event-json { margin: 0; padding: 4px 6px; font-size: 11px; white-space: pre-wrap; word-break: break-word; background: #f6f8fa; border-radius: 4px; }
</style>
