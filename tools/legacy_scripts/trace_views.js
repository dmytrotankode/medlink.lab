const fs = require('fs');
const html = fs.readFileSync('medlink_lab_frontend/run_prototype.html', 'utf-8');
const lines = html.split('\n');
const views = ['workstation', 'validation', 'qc', 'phlebotomy', 'logistics', 'patient', 'microbiology', 'biobank', 'reagents', 'analyzers', 'tat', 'norms'];
views.forEach(v => {
  const vStart = lines.findIndex(l => l.includes("v-show=\"currentView === '" + v + "'"));
  let balance = 0;
  for (let i = 0; i <= vStart; i++) {
    const line = lines[i];
    const opens = (line.match(/<div(\s[^>]*)?>/gi) || []).length;
    const closes = (line.match(/<\/div>/gi) || []).length;
    balance += opens - closes;
  }
  console.log('View', v, 'starts at line', vStart + 1, 'running balance:', balance);
});
