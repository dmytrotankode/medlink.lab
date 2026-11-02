<!--
  ТЗ 22.14 п. 4–7, 9: «Картка справи» ПГД — шапка, прогрес етапів, вкладки: Направлення, Вирізка (шаблон, блоки, вартість),
  Блоки і скельця (етапи обробки, дозамовлення забарвлень/ІГХ), Опис і висновок (МКХ-О, reflex ІГХ, попередній, підпис КЕП,
  доповнення, версії), Друк 014/о, Події. Для CITO — таймер і «Повідомити результат в операційну» з read-back.
-->
<template>
  <div data-testid="pathologyCasePage">
    <page-header :title="c ? `Справа ${c.caseNo || c.orderNumber}` : 'Справа ПГД'" icon="fas fa-folder-open"
      :subtitle="c ? `${c.patient.name} · ${c.kindLabel} · ${c.sourceLabel}${c.department ? ' · ' + c.department : ''}` : ''"
      :breadcrumbs="[{ label: 'Патоморфологія' }, { label: 'Журнал справ', to: { name: 'lab-path-cases' } }, { label: c ? c.caseNo || '' : '' }]">
      <q-btn flat dense color="primary" icon="refresh" label="Оновити" :loading="loading" @click="load" />
    </page-header>
    <api-error-banner v-if="apiError && !apiOffline" :message="apiError" @retry="load" />

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
            <span class="text-caption text-grey-7">Строк: <b :class="c.overdue ? 'text-negative' : ''">{{ c.slaDueAt | datetime }}</b> · вартість <b>{{ c.price | money }}</b></span>
          </div>
        </div>
        <div v-if="c.citoTimer" class="row items-center q-mt-sm cito-bar" :class="citoLate ? 'late' : ''" data-testid="citoBar">
          <q-icon name="timer" size="22px" class="q-mr-sm" />
          <div class="col">
            <b>{{ c.citoTimer.minutes }} хв</b> від надходження (ціль {{ c.citoTimer.targetMinutes }} хв)
            <span v-if="c.citoTimer.notifiedAt"> · повідомлено в операційну {{ c.citoTimer.notifiedAt | datetime }}</span>
          </div>
          <q-btn unelevated color="negative" icon="phone_in_talk" label="Повідомити результат в операційну" data-testid="citoNoticeBtn" @click="citoOpen = true" />
        </div>
      </div>

      <q-tabs v-model="tab" dense align="left" active-color="primary" indicator-color="primary" class="text-grey-8 q-mb-sm">
        <q-tab name="ref" icon="description" label="Направлення" />
        <q-tab name="gross" icon="content_cut" label="Вирізка" />
        <q-tab name="blocks" icon="view_module" :label="`Блоки і скельця (${c.totals.blocks}/${c.totals.slides})`" />
        <q-tab name="report" icon="biotech" label="Опис і висновок" />
        <q-tab name="print" icon="print" label="Друк 014/о" />
        <q-tab name="events" icon="history" label="Події" />
      </q-tabs>

      <!-- Направлення -->
      <div v-show="tab === 'ref'" class="medlink-card q-pa-md">
        <q-banner v-if="c.priorSignedCases && c.priorSignedCases.length" dense class="bg-amber-1 text-amber-10 q-mb-sm">
          МІС позначила біопсію «первинна», але в пацієнта є підписані справи: {{ c.priorSignedCases.join(', ') }} (FR-PAT-010)
        </q-banner>
        <q-markup-table dense flat bordered wrap-cells class="kv">
          <tbody>
            <tr v-if="c.referral.form014 && c.referral.form014.facility"><td>1. Заклад</td><td>{{ c.referral.form014.facility }}</td></tr>
            <tr><td>Пацієнт</td><td>{{ c.patient.name }}, {{ c.patient.birthday | date }}</td></tr>
            <tr><td>Направник</td><td>{{ c.department }} · {{ c.referral.referringDoctor }}<span v-if="c.referral.medicalCardNo"> · карта {{ c.referral.medicalCardNo }}</span></td></tr>
            <tr><td>4–5. Біопсія</td><td>{{ c.referral.biopsyKindLabel }}
              <router-link v-if="c.primaryCaseId" :to="{ name: 'lab-path-case', params: { id: c.primaryCaseId } }"> · первинна справа {{ c.referral.primaryCaseNo }}</router-link>
              <span v-else-if="c.referral.primaryCaseNo"> · первинна справа {{ c.referral.primaryCaseNo }}</span></td></tr>
            <tr><td>6. Операція</td><td>{{ c.referral.operationText || '—' }}</td></tr>
            <tr><td>7. Маркування</td><td>{{ c.referral.markingText || '—' }}</td></tr>
            <tr><td>8. Клінічні дані</td><td>{{ c.referral.clinicalData || '—' }}</td></tr>
            <tr><td>9. Клінічний діагноз</td><td>{{ c.referral.clinicalDiagnosis || '—' }}</td></tr>
            <tr v-if="c.ehealthReferral"><td>Е-направлення ЕСОЗ</td><td>{{ c.ehealthReferral }} · {{ c.ehealthStatus }}</td></tr>
            <tr><td>Прийнято</td><td>{{ c.receivedAt | datetime }} · {{ c.orderNumber }}</td></tr>
          </tbody>
        </q-markup-table>
      </div>

      <!-- Вирізка -->
      <div v-show="tab === 'gross'" class="medlink-card q-pa-md" data-testid="grossTab">
        <div class="row q-col-gutter-sm q-mb-sm">
          <div class="col-12 col-md-6">
            <q-select v-model="gross.templateName" dense outlined emit-value map-options :options="templateOptions" label="Шаблон макроопису (орган · вид матеріалу)" :disable="!grossEditable" @input="applyMacroTemplate" />
          </div>
          <div class="col-12 col-md-6 text-right text-caption text-grey-8 self-center">
            Блоків <b>{{ grossBlocks }}</b> · шматочків <b>{{ grossPieces }}</b> · вартість вирізки <b data-testid="grossPrice">{{ grossPrice | money }}</b>
            <div>перший блок {{ prices.firstBlock | money }} + кожен наступний {{ prices.nextBlock | money }}</div>
          </div>
        </div>
        <q-input v-model="gross.macro" dense outlined type="textarea" autogrow label="Макроопис (п. 17)" :readonly="!grossEditable" class="q-mb-md" />
        <div v-for="o in gross.objects" :key="o.objectId" class="q-mb-sm">
          <div class="row items-center q-mb-xs">
            <b>Об'єкт №{{ o.mark }}</b>&nbsp;<span class="text-grey-7">{{ o.description }}</span>
            <q-space />
            <q-btn v-if="grossEditable" flat dense size="sm" color="primary" icon="add" label="Блок" @click="o.blocks.push({ pieces: 1 })" />
          </div>
          <div class="row q-gutter-sm">
            <div v-for="(b, i) in o.blocks" :key="i" class="block-chip">
              <div class="text-weight-bold">{{ o.letter }}{{ i + 1 }}</div>
              <q-input v-model.number="b.pieces" dense outlined type="number" min="1" style="width: 70px" label="шм." :readonly="!grossEditable" />
              <q-btn v-if="grossEditable" flat dense round size="xs" icon="close" @click="o.blocks.splice(i, 1)" />
            </div>
            <div v-if="!o.blocks.length" class="text-caption text-grey-6">Блоків немає</div>
          </div>
        </div>
        <div v-if="grossEditable" class="row q-gutter-sm justify-end">
          <q-btn flat color="primary" icon="save" label="Зберегти" @click="saveGross(false)" />
          <q-btn unelevated color="primary" icon="done_all" label="Завершити вирізку" data-testid="finishGross" :disable="!grossBlocks" @click="saveGross(true)" />
        </div>
        <div v-else class="text-caption text-grey-7">Вирізку завершено {{ c.grossFinishedAt | datetime }} — скельця Г-Е створено для кожного блока.</div>
      </div>

      <!-- Блоки і скельця -->
      <div v-show="tab === 'blocks'" class="medlink-card q-pa-md" data-testid="blocksTab">
        <div class="row items-center q-gutter-sm q-mb-sm">
          <q-input v-model="scan" dense outlined placeholder="Скан касети або скельця" style="width: 240px" @keyup.enter="doScan">
            <template v-slot:prepend><q-icon name="qr_code_scanner" /></template>
          </q-input>
          <q-btn v-if="labStage" unelevated color="teal-7" icon="fact_check" :label="`${stageLabel(c.stage)}: виконано для всіх касет`" data-testid="advanceAll" @click="advance" />
          <q-space />
          <q-btn outline color="purple-6" icon="add_circle_outline" label="Додаткові забарвлення / ІГХ" :disable="!canAdditional" @click="openAdditional()" />
        </div>
        <q-markup-table dense flat bordered>
          <thead><tr><th class="text-left">Блок</th><th class="text-left">Шматочків</th><th class="text-left">Етап касети</th><th class="text-left">Скельця</th><th class="text-left">Архів</th></tr></thead>
          <tbody>
            <template v-for="o in c.objects">
              <tr v-for="b in o.blocks" :key="b.id">
                <td><b>{{ b.code }}</b> <q-badge v-if="b.isExternal" outline color="indigo-6" label="зовнішній" /><div class="text-caption"><code>{{ b.barcode }}</code></div></td>
                <td>{{ b.pieces || '—' }}</td>
                <td>{{ b.stageLabel || '—' }}</td>
                <td>
                  <div v-for="s in b.slides" :key="s.id" class="row items-center no-wrap">
                    <q-badge :color="s.status === 'IHC_IN_WORK' ? 'purple-5' : s.status === 'READY' ? 'positive' : 'grey-6'" :label="s.code" class="q-mr-xs" />
                    <span class="text-caption">{{ s.stainMethod }}<span v-if="s.status === 'IHC_IN_WORK'"> · ІГХ в роботі</span><span v-if="s.orderedFrom && s.orderedFrom !== 'LAB'"> · з {{ s.orderedFrom }}</span></span>
                    <q-btn v-if="s.status === 'IHC_IN_WORK'" flat dense size="xs" color="positive" icon="done" @click="slideReady(s)"><q-tooltip>Скельце готове</q-tooltip></q-btn>
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
        <div class="text-caption text-grey-7 q-mt-sm">Шматочків / блоків: {{ c.totals.pieces }} / {{ c.totals.blocks }} · методики: {{ c.totals.stains.join(', ') || '—' }}</div>
      </div>

      <!-- Опис і висновок -->
      <div v-show="tab === 'report'" class="medlink-card q-pa-md" data-testid="reportTab">
        <q-banner v-for="(r, i) in c.reflex" :key="i" dense class="bg-purple-1 text-purple-10 q-mb-sm" data-testid="reflexBanner">
          <q-icon name="auto_awesome" /> Reflex ІГХ ({{ r.when }}): пропонується панель <b>{{ r.panel.join(', ') }}</b> — {{ r.why }}
          <template v-slot:action>
            <q-btn v-if="r.missing.length && canAdditional" flat color="purple-8" :label="`Дозамовити ${r.missing.join(', ')}`" @click="openAdditional(r.missing)" />
            <span v-else-if="!r.missing.length" class="text-caption">панель замовлено</span>
          </template>
        </q-banner>
        <q-banner v-if="c.preliminary" dense class="bg-amber-1 text-amber-10 q-mb-sm">Попередній висновок надіслано в МІС {{ c.preliminary.at | datetime }}: {{ c.preliminary.conclusionText }}</q-banner>

        <div class="row q-col-gutter-sm">
          <div class="col-12 col-md-7">
            <q-select v-model="microTemplate" dense outlined emit-value map-options :options="templateOptions" label="Шаблон мікроопису" :disable="signed" class="q-mb-sm" @input="applyMicroTemplate" />
            <q-input v-model="rep.micro" dense outlined type="textarea" autogrow label="Мікроскопічний опис (п. 17)" :readonly="signed" class="q-mb-sm" data-testid="microText" />
            <q-input v-model="rep.conclusionText" dense outlined type="textarea" autogrow label="Патогістологічний висновок (п. 18)" :readonly="signed" data-testid="conclusionText">
              <template v-slot:after><q-btn v-if="!signed" flat dense color="primary" icon="merge_type" label="Зібрати текст" @click="compose" /></template>
            </q-input>
          </div>
          <div class="col-12 col-md-5">
            <div class="row q-col-gutter-xs">
              <div class="col-6"><q-input v-model="rep.icd10" dense outlined label="МКХ-10" :readonly="signed" /></div>
              <div class="col-6"><q-select v-model="rep.grade" dense outlined clearable :options="['G1', 'G2', 'G3', 'GX']" label="G" :readonly="signed" /></div>
              <div class="col-12"><q-select v-model="rep.icdoTopo" dense outlined clearable use-input fill-input hide-selected input-debounce="0" emit-value map-options :options="topoOptions" label="МКХ-О топографія *" :readonly="signed" data-testid="icdoTopo" @new-value="(v, done) => done(v, 'add-unique')" /></div>
              <div class="col-12"><q-select v-model="rep.icdoMorph" dense outlined clearable use-input fill-input hide-selected input-debounce="0" emit-value map-options :options="morphOptions" label="МКХ-О морфологія *" :readonly="signed" data-testid="icdoMorph" @new-value="(v, done) => done(v, 'add-unique')" /></div>
              <div class="col-6"><q-input v-model="rep.pt" dense outlined label="pT" :readonly="signed" /></div>
              <div class="col-6"><q-input v-model="rep.pn" dense outlined label="pN" :readonly="signed" /></div>
              <div class="col-6"><q-input v-model.number="rep.lnTotal" dense outlined type="number" label="Л/в досліджено" :readonly="signed" /></div>
              <div class="col-6"><q-input v-model.number="rep.lnPositive" dense outlined type="number" label="Л/в уражено" :readonly="signed" /></div>
              <div class="col-12"><q-input v-model="rep.margin" dense outlined label="Край резекції (R, мм)" :readonly="signed" /></div>
              <div class="col-6"><q-select v-model="rep.lvi" dense outlined emit-value map-options clearable :options="yesNo" label="LVI" :readonly="signed" /></div>
              <div class="col-6"><q-select v-model="rep.pni" dense outlined emit-value map-options clearable :options="yesNo" label="PNI" :readonly="signed" /></div>
            </div>
          </div>
        </div>

        <div v-if="rep.ihc.length" class="q-mt-md">
          <div class="text-weight-bold q-mb-xs">Таблиця ІГХ</div>
          <q-markup-table dense flat bordered>
            <thead><tr><th class="text-left">Маркер</th><th class="text-left">Скельце</th><th class="text-left">Результат</th><th class="text-left">Оцінка</th></tr></thead>
            <tbody>
              <tr v-for="i in rep.ihc" :key="i.marker">
                <td><b>{{ i.marker }}</b></td>
                <td class="text-caption">{{ slideCode(i.slideId) }}</td>
                <td><q-input v-model="i.resultText" dense borderless placeholder="позитивний / негативний…" :readonly="signed" /></td>
                <td><q-input v-model="i.score" dense borderless placeholder="бали, %" :readonly="signed" style="width: 110px" /></td>
              </tr>
            </tbody>
          </q-markup-table>
        </div>

        <div class="row q-gutter-sm justify-end q-mt-md">
          <template v-if="!signed">
            <q-btn flat color="primary" icon="save" label="Зберегти" data-testid="saveReport" @click="saveReport()" />
            <q-btn outline color="amber-9" icon="schedule_send" label="Попередній у МІС" @click="prelimOpen = true" />
            <q-btn unelevated color="primary" icon="draw" label="Підписати КЕП" data-testid="signBtn" :disable="!canSign" @click="sign" >
              <q-tooltip v-if="!rep.icdoTopo || !rep.icdoMorph">Для підпису обов'язкові топографія та морфологія МКХ-О</q-tooltip>
            </q-btn>
          </template>
          <q-btn v-else unelevated color="primary" icon="post_add" label="Доповнення" data-testid="addendumBtn" @click="openAddendum" />
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
                <div class="form-v" :class="{ hl: highlight && v }" :title="highlight && v ? `Джерело: ${side.src}` : ''">{{ v || ' ' }}</div>
              </div>
            </div>
          </div>
          <div v-if="!form.signed" class="col-12 text-caption text-grey-7">До підпису висновку п. 18 і підписи друкуються порожніми (FR-PAT-082).</div>
        </div>
      </div>

      <!-- Події -->
      <div v-show="tab === 'events'" class="row q-col-gutter-md">
        <div class="col-12 col-md-6">
          <div class="medlink-card q-pa-sm">
            <div class="text-weight-bold q-mb-xs">Журнал етапів</div>
            <q-list dense separator>
              <q-item v-for="(l, i) in c.stageLog" :key="i">
                <q-item-section><q-item-label>{{ l.stageLabel }}<span v-if="l.comment" class="text-grey-7"> · {{ l.comment }}</span></q-item-label>
                  <q-item-label caption>{{ l.performedByName }}<span v-if="l.device"> · {{ l.device }}</span></q-item-label></q-item-section>
                <q-item-section side class="text-caption">{{ l.performedAt | datetime }}</q-item-section>
              </q-item>
            </q-list>
          </div>
        </div>
        <div class="col-12 col-md-6">
          <div class="medlink-card q-pa-sm">
            <div class="text-weight-bold q-mb-xs">Події для МІС</div>
            <q-list dense separator>
              <q-item v-for="(e, i) in c.events" :key="i">
                <q-item-section><q-item-label><q-badge :color="eventColor(e.event)" :label="e.event" /></q-item-label>
                  <q-item-label caption class="ellipsis-2-lines">{{ e.payload }}</q-item-label></q-item-section>
                <q-item-section side class="text-caption">{{ e.at | datetime }}</q-item-section>
              </q-item>
            </q-list>
            <div v-if="c.citoNotices.length" class="q-mt-sm">
              <div class="text-weight-bold">Повідомлення CITO</div>
              <div v-for="(n, i) in c.citoNotices" :key="i" class="text-caption">{{ n.at | datetime }} · {{ n.notifiedPerson }} ({{ n.method }}, read-back ✓) · {{ n.minutesFromReceipt }} хв від надходження: {{ n.text }}</div>
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
          <q-select v-model="add.blockId" dense outlined emit-value map-options :options="blockOptions" label="Блок" />
          <q-select v-model="add.methods" dense outlined multiple use-chips emit-value map-options :options="methodOptions" label="Методики" />
          <q-select v-model="add.source" dense outlined emit-value map-options :options="[{ label: 'Лабораторія', value: 'LAB' }, { label: 'МІС', value: 'MIS' }, { label: 'МДК', value: 'MDK' }]" label="Джерело дозамовлення" />
          <div class="text-caption text-grey-7">ІГХ подовжує строк на {{ prices.ihcExtraDays }} роб. дні; вартість перераховується; МІС отримає ADDITIONAL_STUDY_ORDERED.</div>
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
          <q-input v-model="prelimText" dense outlined type="textarea" autogrow label="Текст (позначка «Попередній»)" />
          <div class="text-caption text-grey-7 q-mt-xs">Не є морфологічною верифікацією, не потрапляє у виписку та МДК (FR-PAT-043).</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="amber-9" label="Надіслати в МІС" :disable="!prelimText" @click="doPreliminary" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="addendumOpen">
      <q-card style="min-width: 520px">
        <q-card-section class="text-h6">Доповнення до висновку (v{{ c ? c.versions.length + 1 : '' }})</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-select v-model="addendum.type" dense outlined emit-value map-options :options="addendumTypes" label="Тип доповнення" />
          <q-input v-model="addendum.text" dense outlined type="textarea" autogrow label="Текст доповнення" />
          <q-input v-if="addendum.type === 'CORRECTION'" v-model="addendum.reason" dense outlined label="Обґрунтування виправлення *" />
          <q-input v-model="addendum.icdoMorph" dense outlined label="МКХ-О морфологія (якщо змінюється)" />
          <q-checkbox v-model="addendum.changesDiagnosis" label="Змінює діагноз / стадію (сповіщення МДК)" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="primary" icon="draw" label="Підписати доповнення КЕП" :disable="!addendum.text || (addendum.type === 'CORRECTION' && !addendum.reason)" @click="doAddendum" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <q-dialog v-model="citoOpen">
      <q-card style="min-width: 480px">
        <q-card-section class="text-h6">Повідомлення результату в операційну</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input v-model="cito.text" dense outlined type="textarea" autogrow label="Інтраопераційний висновок" />
          <q-input v-model="cito.notifiedPerson" dense outlined label="Кому повідомлено" />
          <q-select v-model="cito.method" dense outlined emit-value map-options :options="[{ label: 'Телефон', value: 'PHONE' }, { label: 'Інтерком', value: 'INTERCOM' }, { label: 'Особисто', value: 'IN_PERSON' }]" label="Спосіб" />
          <q-checkbox v-model="cito.readBack" label="Отримувач повторив висновок (read-back) *" data-testid="readBack" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Скасувати" v-close-popup />
          <q-btn unelevated color="negative" label="Зафіксувати повідомлення" :disable="!cito.readBack || !cito.text || !cito.notifiedPerson" @click="doCito" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import apiMixin from '../../../mixins/apiMixin';

const LAB_STAGES = ['PROC', 'EMB', 'SECT', 'STAIN'];
const STAGE_LABELS = { RCV: 'Прийнято', GROSS: 'Вирізка', PROC: 'Проводка', EMB: 'Заливка', SECT: 'Мікротомія', STAIN: 'Фарбування', MICRO: 'Мікроскопія й опис', SIGNED: 'Підписано' };

export default {
  name: 'PathologyCasePage',
  mixins: [apiMixin],
  props: { id: { type: String, required: true } },
  data () {
    return {
      c: null, tab: this.$route.query.tab || 'ref', dict: { templates: [], topo: [], morph: [], stains: [], prices: {} }, form: null, highlight: true,
      gross: { macro: '', templateName: null, objects: [] }, rep: this.emptyReport(), microTemplate: null, scan: '',
      addOpen: false, add: { blockId: null, methods: [], source: 'LAB' }, prelimOpen: false, prelimText: '',
      addendumOpen: false, addendum: { type: 'IHC', text: '', reason: '', icdoMorph: '', changesDiagnosis: false },
      citoOpen: false, cito: { text: '', notifiedPerson: '', method: 'PHONE', readBack: false },
      yesNo: [{ label: 'виявлено', value: true }, { label: 'не виявлено', value: false }],
      addendumTypes: [{ label: 'ІГХ / молекулярні дослідження', value: 'IHC' }, { label: 'Перегляд препаратів', value: 'REVIEW' }, { label: 'Виправлення з обґрунтуванням', value: 'CORRECTION' }]
    };
  },
  computed: {
    prices () { return this.dict.prices || {}; },
    signed () { return this.c && this.c.stage === 'SIGNED'; },
    labStage () { return this.c && LAB_STAGES.includes(this.c.stage); },
    citoLate () { return this.c && this.c.citoTimer && this.c.citoTimer.minutes > this.c.citoTimer.targetMinutes && !this.c.citoTimer.notifiedAt; },
    grossEditable () { return this.c && ['RCV', 'GROSS'].includes(this.c.stage) && this.c.intakeStatus === 'ACCEPTED' && this.c.kind !== 'CONSULT'; },
    grossBlocks () { return this.gross.objects.reduce((n, o) => n + o.blocks.length, 0); },
    grossPieces () { return this.gross.objects.reduce((n, o) => n + o.blocks.reduce((m, b) => m + (Number(b.pieces) || 0), 0), 0); },
    grossPrice () { const n = this.grossBlocks; return (this.c && this.c.kind === 'CITO' ? this.prices.cito : 0) + (n ? this.prices.firstBlock + this.prices.nextBlock * (n - 1) : 0); },
    canAdditional () { return this.c && !['RCV', 'GROSS'].includes(this.c.stage); },
    canSign () { return this.rep.icdoTopo && this.rep.icdoMorph; },
    templateOptions () { return this.dict.templates.map(t => ({ label: t.name, value: t.name })); },
    topoOptions () { return this.dict.topo.map(t => ({ label: `${t.code} ${t.name}`, value: t.code })); },
    morphOptions () { return this.dict.morph.map(t => ({ label: `${t.code} ${t.name}`, value: t.code })); },
    blockOptions () { return this.c ? this.c.objects.flatMap(o => o.blocks).map(b => ({ label: `${b.code}${b.status === 'ISSUED' ? ' (видано)' : ''}`, value: b.id, disable: b.status === 'ISSUED' })) : []; },
    methodOptions () { return this.dict.stains.filter(s => s.code !== 'HE').map(s => ({ label: s.isIhc ? `ІГХ ${s.code} — ${s.name}` : s.name, value: s.code })); }
  },
  watch: {
    id () { this.load(); },
    tab (v) { if (v === 'print') this.loadForm(); }
  },
  async created () {
    this.dict = await this.$api.pathDictionaries().catch(() => this.dict);
    await this.load();
    if (this.tab === 'ref' && this.c) this.tab = this.c.stage === 'RCV' || this.c.stage === 'GROSS' ? 'gross' : LAB_STAGES.includes(this.c.stage) ? 'blocks' : this.c.stage === 'MICRO' ? 'report' : 'ref';
    if (this.tab === 'print') this.loadForm();
  },
  methods: {
    emptyReport () { return { micro: '', conclusionText: '', icd10: '', icdoTopo: null, icdoMorph: null, grade: null, pt: '', pn: '', lnTotal: null, lnPositive: null, margin: '', lvi: null, pni: null, ihc: [] }; },
    stageLabel (s) { return STAGE_LABELS[s] || s; },
    addendumLabel (t) { return (this.addendumTypes.find(x => x.value === t) || { label: t }).label; },
    eventColor (e) { return { RECEIVED: 'primary', REJECTED: 'negative', CRITICAL_RESULT: 'negative', RESULT_FINAL: 'positive', ADDENDUM: 'teal-6', RESULT_PRELIMINARY: 'amber-9', ADDITIONAL_STUDY_ORDERED: 'purple-5' }[e] || 'grey-7'; },
    slideCode (id) { const s = this.c && this.c.objects.flatMap(o => o.blocks).flatMap(b => b.slides).find(x => x.id === id); return s ? s.code : '—'; },
    async load () {
      const c = await this.callApi(() => this.$api.pathCase(this.id));
      if (c) this.apply(c);
    },
    apply (c) {
      this.c = c;
      this.gross = {
        macro: c.macro || '', templateName: c.templateName,
        objects: c.objects.map(o => ({ objectId: o.id, mark: o.mark, letter: o.letter, description: o.description, blocks: o.blocks.filter(b => !b.isExternal).map(b => ({ pieces: b.pieces })) }))
      };
      const r = c.report || {};
      const rep = this.emptyReport();
      Object.keys(rep).forEach(k => { if (r[k] !== undefined && r[k] !== null) rep[k] = r[k]; });
      // ІГХ-рядки: збережені результати + маркери з ІГХ-скелець справи
      const ihc = (r.ihc || []).map(i => ({ ...i }));
      c.objects.flatMap(o => o.blocks).flatMap(b => b.slides).filter(s => s.isIhc && s.marker).forEach(s => {
        if (!ihc.some(i => i.marker === s.marker)) ihc.push({ marker: s.marker, slideId: s.id, resultText: '', score: '' });
      });
      rep.ihc = ihc;
      this.rep = rep;
      if (this.tab === 'print') this.loadForm();
    },
    async loadForm () { this.form = await this.callApi(() => this.$api.pathForm014(this.c.id), { silent: true }); },
    applyMacroTemplate (name) {
      const t = this.dict.templates.find(x => x.name === name);
      if (t) this.gross.macro = t.macro;
    },
    applyMicroTemplate (name) { const t = this.dict.templates.find(x => x.name === name); if (t) this.rep.micro = t.micro; },
    async saveGross (finish) {
      const body = { macro: this.gross.macro, templateName: this.gross.templateName, finish, objects: this.gross.objects.map(o => ({ objectId: o.objectId, blocks: o.blocks.map(b => ({ pieces: Number(b.pieces) || 1 })) })) };
      const c = await this.callApi(() => this.$api.pathGross(this.c.id, body), { success: finish ? 'Вирізку завершено: скельця Г-Е створено, вартість перераховано' : 'Збережено' });
      if (c) { this.apply(c); if (finish) this.tab = 'blocks'; }
    },
    async doScan () {
      const code = (this.scan || '').trim();
      if (!code) return;
      const r = await this.callApi(() => this.$api.pathScan({ barcode: code }));
      this.scan = '';
      if (r) { this.$q.notify({ type: 'positive', message: `${r.blockCode}: ${r.stageLabel}` }); await this.load(); }
    },
    async advance () {
      const c = await this.callApi(() => this.$api.pathAdvance(this.c.id, { stage: this.c.stage }), { success: `${this.stageLabel(this.c.stage)}: виконано` });
      if (c) this.apply(c);
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
    reportBody () { return { ...this.rep, ihc: this.rep.ihc.map(i => ({ marker: i.marker, slideId: i.slideId, resultText: i.resultText, score: i.score })) }; },
    async saveReport (silent) {
      const c = await this.callApi(() => this.$api.pathSaveReport(this.c.id, this.reportBody()), { success: silent ? null : 'Висновок збережено' });
      if (c) this.apply(c);
      return c;
    },
    async compose () {
      const c = await this.saveReport(true);
      if (c && c.composedText) this.rep.conclusionText = c.composedText;
    },
    async doPreliminary () {
      const c = await this.callApi(() => this.$api.pathPreliminary(this.c.id, this.prelimText), { success: 'Попередній висновок надіслано в МІС (RESULT_PRELIMINARY)' });
      if (c) { this.prelimOpen = false; this.apply(c); }
    },
    async signer () {
      const s = await this.$api.ehealthSession();
      return s && s.testCertificate;
    },
    async sign () {
      if (!(await this.saveReport(true))) return;
      const cert = await this.signer();
      this.$q.dialog({
        title: 'Підпис КЕП', cancel: true, persistent: true,
        message: cert ? `Підписати висновок справи ${this.c.caseNo} сертифікатом ${cert.name} (${cert.certSerial})?` : 'Сертифікат КЕП недоступний'
      }).onOk(async () => {
        const r = await this.callApi(() => this.$api.pathSign(this.c.id, cert), { success: 'Висновок підписано КЕП, МІС отримала RESULT_FINAL' });
        if (r) { (r.warnings || []).forEach(w => this.$q.notify({ type: 'warning', message: w, timeout: 6000 })); this.apply(r.case); }
      });
    },
    openAddendum () { this.addendum = { type: 'IHC', text: '', reason: '', icdoMorph: '', changesDiagnosis: false }; this.addendumOpen = true; },
    async doAddendum () {
      const cert = await this.signer();
      const body = { ...this.addendum, icdoMorph: this.addendum.icdoMorph || null, ihc: this.addendum.type === 'IHC' ? this.reportBody().ihc.filter(i => i.resultText) : null, signer: cert };
      const c = await this.callApi(() => this.$api.pathAddendum(this.c.id, body), { success: 'Доповнення підписано; МІС отримала ADDENDUM' });
      if (c) { this.addendumOpen = false; this.apply(c); }
    },
    async doCito () {
      const c = await this.callApi(() => this.$api.pathCitoNotice(this.c.id, this.cito), { success: 'Повідомлення зафіксовано; МІС отримала CRITICAL_RESULT' });
      if (c) { this.citoOpen = false; this.apply(c); }
    }
  }
};
</script>

<style scoped>
.stages { gap: 3px; overflow-x: auto; }
.stage { flex: 1; min-width: 86px; text-align: center; font-size: 11px; padding: 5px 4px; border-radius: 4px; background: #eef1f5; color: #6b7785; white-space: nowrap; }
.stage.done { background: #e3f2e5; color: #2e7d32; }
.stage.cur { background: #fff3e0; color: #e65100; font-weight: 700; }
.cito-bar { background: #fff3e0; border-radius: 6px; padding: 6px 10px; color: #bf360c; }
.cito-bar.late { background: #ffebee; color: #c62828; }
.block-chip { display: flex; align-items: center; gap: 6px; border: 1px solid #dde3ea; border-radius: 6px; padding: 4px 6px; }
.kv td:first-child { width: 190px; color: #5f6b7a; white-space: nowrap; }
.form-sheet { border: 1px solid #9aa8b8; border-radius: 4px; padding: 10px 12px; background: #fff; font-size: 12px; }
.form-row { display: flex; border-bottom: 1px solid #dde3ea; padding: 3px 0; }
.form-k { width: 42%; color: #5f6b7a; padding-right: 6px; }
.form-v { flex: 1; white-space: pre-wrap; }
.form-v.hl { background: #fff8c4; }
</style>
