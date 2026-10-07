const fs = require('fs');
const html = fs.readFileSync('medlink_lab_frontend/run_prototype.html', 'utf-8');
const startIdx = html.indexOf('id="q-app"');
const scriptIdx = html.lastIndexOf('<script>');
if (startIdx !== -1 && scriptIdx !== -1) {
  const template = html.substring(startIdx, scriptIdx);
  console.log('Template length:', template.length);
  const tags = ['div', 'q-card', 'q-card-section', 'svg', 'g', 'circle', 'text', 'line', 'polyline', 'rect', 'q-banner', 'q-table'];
  tags.forEach(t => {
    const openMatches = template.match(new RegExp('<' + t + '(\\s[^>]*)?>', 'gi')) || [];
    const closeMatches = template.match(new RegExp('</' + t + '>', 'gi')) || [];
    if (openMatches.length !== closeMatches.length) {
      console.log('MISMATCH:', t, 'open:', openMatches.length, 'close:', closeMatches.length, 'diff:', openMatches.length - closeMatches.length);
    } else {
      console.log('OK:', t, openMatches.length);
    }
  });
} else {
  console.log('Not found:', startIdx, scriptIdx);
}

