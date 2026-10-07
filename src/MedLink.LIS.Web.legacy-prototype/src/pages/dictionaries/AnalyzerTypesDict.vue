<template>
  <div class="q-pa-md dict-page">
    <div class="row items-center justify-between q-mb-md">
      <h5 class="q-my-none text-weight-bold text-primary">
        <q-icon name="fas fa-server" class="q-mr-sm" /> Довідник моделей аналізаторів
      </h5>
      <q-btn color="primary" icon="add" label="Додати модель" dense />
    </div>
    <q-card flat bordered>
      <q-table :data="items" :columns="columns" row-key="code" dense flat />
    </q-card>
  </div>
</template>

<script>
export default {
  name: 'AnalyzerTypesDict',
  data() {
    return {
      items: [
        { code: 'SYSMEX-XN1000', vendor: 'Sysmex Corporation (Japan)', model: 'XN-1000', discipline: 'Гематологія 5-diff', protocol: 'ASTM E1381/E1394', interfaceType: 'TCP/IP Client/Server' },
        { code: 'ROCHE-COBAS-E411', vendor: 'Roche Diagnostics (Switzerland)', model: 'Cobas e411', discipline: 'Імунохімія (ECLIA)', protocol: 'ASTM E1394', interfaceType: 'RS-232 / TCP' },
        { code: 'MINDRAY-BS240', vendor: 'Mindray Medical (China)', model: 'BS-240', discipline: 'Клінічна біохімія', protocol: 'HL7 v2.3.1 MLLP', interfaceType: 'TCP/IP MLLP' },
        { code: 'SYSMEX-CA660', vendor: 'Sysmex Corporation (Japan)', model: 'CA-660', discipline: 'Коагулометрія', protocol: 'ASTM E1381', interfaceType: 'RS-232 Serial' }
      ],
      columns: [
        { name: 'code', label: 'Код моделі', field: 'code', align: 'left' },
        { name: 'vendor', label: 'Виробник', field: 'vendor', align: 'left' },
        { name: 'model', label: 'Модель', field: 'model', align: 'left' },
        { name: 'discipline', label: 'Дисципліна', field: 'discipline', align: 'left' },
        { name: 'protocol', label: 'Протокол', field: 'protocol', align: 'center' },
        { name: 'interfaceType', label: 'Тип інтерфейсу', field: 'interfaceType', align: 'left' }
      ]
    };
  }
};
</script>
<style scoped> .dict-page { background: #f7f9fa; min-height: 100%; } </style>
