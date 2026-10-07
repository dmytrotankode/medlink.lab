<template>
  <div class="q-pa-md analyzer-monitor-page">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <h5 class="q-my-none text-weight-bold text-primary">
          <q-icon name="fas fa-network-wired" class="q-mr-sm" />
          Монітор підключення аналізаторів (Шлюз MedLink.LabConnector)
        </h5>
        <div class="text-caption text-grey-7">
          Статус драйверів ASTM E1381/E1394, HL7 v2.x MLLP, COM/RS-232 та мережевих TCP/IP з'єднань з приладами.
        </div>
      </div>
      <div class="row q-gutter-sm">
        <q-btn color="secondary" icon="refresh" label="Оновити статуси" dense flat />
      </div>
    </div>

    <!-- Analyzers Table -->
    <q-card flat bordered class="q-mb-md">
      <q-table
        :data="analyzers"
        :columns="columns"
        row-key="id"
        dense
        flat
      >
        <template v-slot:body-cell-status="props">
          <q-td :props="props">
            <q-badge :color="props.row.status === 'ONLINE' ? 'positive' : 'warning'">
              {{ props.row.status }}
            </q-badge>
          </q-td>
        </template>
        <template v-slot:body-cell-actions="props">
          <q-td :props="props" class="q-gutter-xs">
            <q-btn size="sm" color="primary" dense icon="terminal" label="Лог пакетів" @click="openLogs(props.row)" />
            <q-btn size="sm" color="teal" dense icon="send" label="Тест зв'язку" @click="testPing(props.row)" />
          </q-td>
        </template>
      </q-table>
    </q-card>

    <!-- Raw Packet Logs Modal -->
    <q-dialog v-model="showLogDialog">
      <q-card style="min-width: 650px;">
        <q-card-section class="bg-primary text-white row items-center justify-between">
          <div class="text-subtitle1 text-weight-bold">
            <q-icon name="terminal" class="q-mr-sm" /> ASTM / HL7 Raw Packet Monitor
          </div>
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-md bg-dark text-cyan-2" style="font-family: monospace; font-size: 12px; max-height: 350px; overflow-y: auto;">
          [10:14:02.102] TCP CONNECT from 192.168.1.101:5100 ESTABLISHED<br>
          [10:14:02.105] RECV &lt;ENQ&gt; (0x05)<br>
          [10:14:02.106] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.110] RECV &lt;STX&gt;1H|\^&|||Sysmex^XN-1000^00-14||||||||E1394-97&lt;CR&gt;&lt;ETX&gt;4A&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.111] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.115] RECV &lt;STX&gt;2P|1||108291||Коваленко^Олександр^Сергійович||19850412|M&lt;CR&gt;&lt;ETX&gt;7B&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.116] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.122] RECV &lt;STX&gt;3O|1|1026004820||^^^WBC\^^^HGB|R|20261006084700||||||||||||||||||F&lt;CR&gt;&lt;ETX&gt;9D&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.123] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.130] RECV &lt;STX&gt;4R|1|^^^WBC|7.45|10*9/L|4.0-9.0|N||F||||20261006095011&lt;CR&gt;&lt;ETX&gt;21&lt;CR&gt;&lt;LF&gt;<br>
          [10:14:02.131] SEND &lt;ACK&gt; (0x06)<br>
          [10:14:02.140] RECV &lt;EOT&gt; (0x04) - Transaction completed successfully.<br>
          [10:14:02.145] MedLink Backend: Processed sample 1026004820, WBC saved.
        </q-card-section>
        <q-card-actions align="right" class="bg-grey-2">
          <q-btn flat label="Закрити" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import { mockAnalyzers } from '../../services/mockData';

export default {
  name: 'AnalyzerMonitor',
  data() {
    return {
      showLogDialog: false,
      analyzers: mockAnalyzers,
      columns: [
        { name: 'name', label: 'Модель аналізатора', field: 'name', align: 'left', sortable: true },
        { name: 'type', label: 'Тип дослідження', field: 'type', align: 'left' },
        { name: 'protocol', label: 'Протокол зв'язку', field: 'protocol', align: 'center' },
        { name: 'connection', label: 'Мережевий порт / COM', field: 'connection', align: 'left' },
        { name: 'status', label: 'Статус', field: 'status', align: 'center' },
        { name: 'actions', label: 'Дії', align: 'center' }
      ]
    };
  },
  methods: {
    openLogs() {
      this.showLogDialog = true;
    },
    testPing(a) {
      this.$q.notify({ type: 'positive', message: `Зв'язок з ${a.name} перевірено: OK (Ping 4ms, ACK отримано).` });
    }
  }
};
</script>
<style scoped>
.analyzer-monitor-page { background: #f7f9fa; min-height: 100%; }
</style>
