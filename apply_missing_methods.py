# -*- coding: utf-8 -*-
import sys

METHODS_TO_ADD = """
      openBiomaterialCard(row) {
        this.activeBiomaterial = Object.assign({}, row);
        this.showBiomaterialModal = true;
      },
      openTubeCard(row) {
        this.activeTube = Object.assign({}, row);
        this.showTubeModal = true;
      },
      openAnalyzerCard(row) {
        this.activeAnalyzer = Object.assign({}, row);
        this.showAnalyzerModal = true;
      },
      openParamCard(row) {
        this.activeParam = Object.assign({}, row);
        this.showParamModal = true;
      },
      openNormsForParam(param) {
        this.showParamModal = false;
        let srv = this.servicesCatalog.find(s => s.code === param.code);
        if (!srv) {
          srv = {
            code: param.code,
            name: param.name,
            loinc: param.loinc,
            category: param.category,
            defaultMethod: 'Стандартна методика',
            combinationsCount: 5,
            deltaCheckPct: 25.0,
            deltaHours: 72,
            deltaAutoBlock: true
          };
          this.servicesCatalog.push(srv);
        }
        this.openServiceDetail(srv);
      },
      populateEditForm(row) {
        this.newCombForm = Object.assign({}, row);
        this.$q.notify({ type: 'info', message: 'Дані правила ' + (row.norm_name || '') + ' завантажено у конструктор', position: 'top', timeout: 1200 });
      },
      duplicateRow(row) {
        this.newCombForm = Object.assign({}, row);
        this.newCombForm.id = null;
        this.newCombForm.norm_name = (row.norm_name || 'Правило') + ' (Копія)';
        this.$q.notify({ type: 'positive', message: 'Правило скопійовано у конструктор для швидкого редагування', position: 'top', timeout: 1200 });
      },
      resetCombForm() {
        this.newCombForm = {
          id: null,
          norm_name: '',
          method_name: (this.selectedService && this.selectedService.defaultMethod) || 'Стандартна',
          gender: 'ANY',
          age_unit: 'YEARS',
          age_from: 18,
          age_to: 65,
          is_age: 1,
          is_pregnancy: 0,
          pregnancy_week_from: 1,
          pregnancy_week_to: 14,
          is_menstrual_phase: 0,
          menstrual_phase: 'FOLLICULAR',
          norm_low: 4.10,
          norm_high: 5.90,
          crit_low: 2.50,
          crit_high: 25.00,
          unit: this.currentServiceUnit,
          delta_check_max_pct: 25.0,
          norm_text: ''
        };
      },
      saveNewCombination() {
        const payload = Object.assign({}, this.newCombForm, { test_code: this.selectedService ? this.selectedService.code : 'GLU_SERUM' });
        fetch('/api/laboratory/norms/combinations', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload)
        })
          .then(res => res.json())
          .then(data => {
            if (data.success) {
              this.$q.notify({ type: 'positive', message: 'Правило збережено в БД!', position: 'top' });
              this.loadAllCombinations();
              if (!this.selectedServiceCombinations.find(c => c.id === data.id)) {
                payload.id = data.id;
                this.selectedServiceCombinations.push(payload);
              }
              this.resetCombForm();
            }
          })
          .catch(e => {
            this.$q.notify({ type: 'positive', message: 'Комбінацію додано в локальний сеанс!', position: 'top' });
            payload.id = 'LOCAL-' + Date.now();
            this.selectedServiceCombinations.push(payload);
            this.resetCombForm();
          });
      },
      testInCardResolver() {
        const val = parseFloat(this.inCardTest.value);
        fetch('/api/laboratory/norms/resolve', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            test_code: this.selectedService ? this.selectedService.code : 'GLU_SERUM',
            gender: this.inCardTest.gender,
            age: this.inCardTest.age,
            menstrual_phase: this.inCardTest.phase,
            pregnancy_week: this.inCardTest.pregWeek
          })
        })
          .then(res => res.json())
          .then(data => {
            const rule = data.rule || (this.selectedServiceCombinations[0] || {});
            const nLow = parseFloat(rule.norm_low || 4.1);
            const nHigh = parseFloat(rule.norm_high || 5.9);
            const cLow = rule.crit_low ? parseFloat(rule.crit_low) : null;
            const cHigh = rule.crit_high ? parseFloat(rule.crit_high) : null;

            let statusText = 'В МЕЖАХ НОРМИ';
            let badgeColor = 'positive';
            let color = '#21ba45';
            let bg = '#f0fdf4';

            if ((cLow !== null && val <= cLow) || (cHigh !== null && val >= cHigh)) {
              statusText = '🚨 ПАНІЧНЕ ЗНАЧЕННЯ CITO!';
              badgeColor = 'negative';
              color = '#d04f45';
              bg = '#fef2f2';
            } else if (val < nLow) {
              statusText = '⚠️ НИЖЧЕ НОРМИ';
              badgeColor = 'warning';
              color = '#f2c037';
              bg = '#fffbeb';
            } else if (val > nHigh) {
              statusText = '⚠️ ВИЩЕ НОРМИ';
              badgeColor = 'warning';
              color = '#f2c037';
              bg = '#fffbeb';
            }

            this.inCardResult = {
              ruleName: rule.norm_name || 'Базове правило',
              normLow: nLow,
              normHigh: nHigh,
              unit: rule.unit || this.currentServiceUnit,
              statusText: statusText,
              badgeColor: badgeColor,
              color: color,
              bg: bg
            };
            this.$q.notify({ type: badgeColor === 'negative' ? 'negative' : 'positive', message: 'Результат верифікації: ' + statusText, position: 'top', timeout: 1500 });
          })
          .catch(e => {
            this.inCardResult = {
              ruleName: 'Локальний референс (Клінічна норма)',
              normLow: 4.1,
              normHigh: 5.9,
              unit: 'ммоль/л',
              statusText: val > 5.9 ? '⚠️ ВИЩЕ НОРМИ' : 'В МЕЖАХ НОРМИ',
              badgeColor: val > 5.9 ? 'warning' : 'positive',
              color: val > 5.9 ? '#f2c037' : '#21ba45',
              bg: '#f0fdf4'
            };
          });
      },
"""

for filepath in [r"C:\__MEDLINK___\LABA\medlink_lab_frontend\run_prototype.html", r"C:\__MEDLINK___\LABA\medlink_lab_frontend\index.html"]:
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    if "methods: {" in content:
        # Check if saveNewCombination is already in methods
        if "saveNewCombination()" not in content:
            new_content = content.replace("methods: {", "methods: {\n" + METHODS_TO_ADD)
            with open(filepath, "w", encoding="utf-8") as f:
                f.write(new_content)
            print(f"Updated {filepath} successfully!")
        else:
            print(f"saveNewCombination already present in {filepath}")
    else:
        print(f"Error: 'methods: {{' not found in {filepath}")
