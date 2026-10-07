# -*- coding: utf-8 -*-
"""
Build the interactive File Viewer Modal and bundle all DDL/SQL/Config/Test files.
Provides:
- file_viewer_modal.css
- file_viewer_modal.js (with pre-cached content so it works offline/file:// and via HTTP)
"""

import os
import json
import shutil

BASE_DIR = r"C:\__MEDLINK___\LABA"

# Files to embed in the viewer bundle
files_to_embed = [
    # SQL Suite
    ("sql/00_master_deploy_all.sql", "SQL Master Deployment", "sql"),
    ("sql/01_lis_schema_core.sql", "PostgreSQL DDL Core", "sql"),
    ("sql/02_lis_schema_microbiology_eucast.sql", "PostgreSQL DDL EUCAST", "sql"),
    ("sql/03_evomis_integration_views_and_fk.sql", "PostgreSQL DDL Views & FK", "sql"),
    ("sql/04_seed_biomaterials.sql", "SQL Seed (Biomaterials)", "sql"),
    ("sql/05_seed_tube_types.sql", "SQL Seed (Tube Types)", "sql"),
    ("sql/06_seed_method_types.sql", "SQL Seed (Methods)", "sql"),
    ("sql/07_seed_analyzer_types.sql", "SQL Seed (Analyzers)", "sql"),
    ("sql/08_seed_parameters_and_profiles.sql", "SQL Seed (Parameters & Profiles)", "sql"),
    ("sql/09_seed_microbiology_eucast.sql", "SQL Seed (Microbiology & EUCAST)", "sql"),
    
    # Legacy & Dictionaries (JSON)
    ("dictionaries/01_biomaterials.json", "JSON Dictionary", "json"),
    ("dictionaries/02_tube_types.json", "JSON Dictionary", "json"),
    ("dictionaries/03_method_types.json", "JSON Dictionary", "json"),
    ("dictionaries/04_analyzer_types.json", "JSON Dictionary", "json"),
    ("dictionaries/05_lab_parameters_and_profiles.json", "JSON Dictionary", "json"),
    
    # Test Packets
    ("test_examples/01_astm_query_sysmex.txt", "ASTM E1394 Query", "text"),
    ("test_examples/02_astm_order_response.txt", "ASTM E1394 Order Response", "text"),
    ("test_examples/03_astm_results_cobas.txt", "ASTM E1394 Results (Cobas)", "text"),
    ("test_examples/04_astm_results_sysmex_xn.txt", "ASTM E1394 Results (Sysmex)", "text"),
    ("test_examples/05_hl7_oru_r01_mindray.hl7", "HL7 v2.5.1 ORU^R01 (Mindray)", "text"),
    ("test_examples/06_hl7_ack_response.hl7", "HL7 v2.5.1 ACK Response", "text"),
    ("test_examples/07_fhir_diagnostic_report_bundle.json", "FHIR R4 DiagnosticReport Bundle", "json"),
    
    # Connector Code & Config
    ("MedLink.LabConnector/Core/AstmDriver.cs", "C# .NET 8 Driver", "csharp"),
    ("MedLink.LabConnector/Core/Hl7V2Driver.cs", "C# .NET 8 Driver", "csharp"),
    ("MedLink.LabConnector/Core/OfflineBufferQueue.cs", "C# SQLite Buffer Queue", "csharp"),
    ("MedLink.LabConnector/LabConnectorWorker.cs", "C# Background Service", "csharp"),
    ("MedLink.LabConnector/appsettings.json", "Connector JSON Configuration", "json"),
    ("MedLink.LabConnector/Installers/install-windows.cmd", "Windows Service Setup Script", "cmd"),
    ("MedLink.LabConnector/Installers/install-linux.sh", "Linux Systemd Setup Script", "bash"),
    ("MedLink.LabConnector/Installers/medlink-labconnector.service", "Systemd Unit File", "ini"),
]

file_db = {}

for rel_path, title, lang in files_to_embed:
    abs_path = os.path.join(BASE_DIR, rel_path.replace("/", "\\"))
    if os.path.exists(abs_path):
        with open(abs_path, "r", encoding="utf-8", errors="replace") as f:
            content = f.read()
        file_db[rel_path] = {
            "title": title,
            "lang": lang,
            "path": rel_path,
            "absPath": abs_path,
            "size": len(content),
            "lines": len(content.splitlines()),
            "content": content
        }
    else:
        print(f"Warning: file not found: {abs_path}")

print(f"Embedded {len(file_db)} files in viewer database.")

css_content = """/* ==========================================================================
   MedLink LIS: Interactive File & DDL Modal Viewer
   ========================================================================== */

#medlinkFileModalOverlay {
  display: none;
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(15, 23, 42, 0.85);
  backdrop-filter: blur(6px);
  -webkit-backdrop-filter: blur(6px);
  z-index: 999999;
  justify-content: center;
  align-items: center;
  padding: 20px;
  box-sizing: border-box;
}

#medlinkFileModalOverlay.active {
  display: flex;
}

.medlink-modal-card {
  background: #1e1e1e;
  color: #d4d4d4;
  width: 95vw;
  max-width: 1250px;
  height: 88vh;
  max-height: 920px;
  border-radius: 10px;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.7), 0 0 0 1px rgba(255, 255, 255, 0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
}

.medlink-modal-header {
  background: #252526;
  padding: 12px 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #333333;
}

.medlink-modal-title-area {
  display: flex;
  align-items: center;
  gap: 12px;
  overflow: hidden;
}

.medlink-modal-badge {
  background: #0178BC;
  color: #ffffff;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  padding: 3px 8px;
  border-radius: 4px;
  letter-spacing: 0.5px;
  white-space: nowrap;
}

.medlink-modal-filename {
  font-family: "JetBrains Mono", Consolas, "Courier New", monospace;
  font-size: 15px;
  font-weight: 600;
  color: #ffffff;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.medlink-modal-path {
  font-size: 11px;
  color: #888888;
  font-family: "JetBrains Mono", Consolas, monospace;
}

.medlink-modal-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.medlink-btn {
  background: #333333;
  color: #ffffff;
  border: 1px solid #444444;
  padding: 6px 14px;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: all 0.15s;
  text-decoration: none;
}

.medlink-btn:hover {
  background: #444444;
  border-color: #555555;
  color: #ffffff;
}

.medlink-btn-primary {
  background: #0178BC;
  border-color: #0178BC;
}

.medlink-btn-primary:hover {
  background: #005a8e;
  border-color: #005a8e;
}

.medlink-btn-close {
  background: transparent;
  border: none;
  color: #aaaaaa;
  font-size: 20px;
  line-height: 1;
  padding: 4px 8px;
  cursor: pointer;
  border-radius: 4px;
}

.medlink-btn-close:hover {
  background: #d04f45;
  color: #ffffff;
}

.medlink-modal-toolbar {
  background: #2d2d2d;
  padding: 6px 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
  color: #999999;
  border-bottom: 1px solid #333333;
}

.medlink-modal-body {
  flex: 1;
  overflow-y: auto;
  overflow-x: auto;
  padding: 16px 20px;
  background: #1e1e1e;
}

.medlink-code-pre {
  margin: 0;
  padding: 0;
  font-family: "JetBrains Mono", "Cascadia Code", Consolas, "Courier New", monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #d4d4d4;
  white-space: pre;
  tab-size: 2;
}

/* Modal Trigger Buttons In Documentation */
.doc-file-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: #e0f2fe;
  color: #0369a1;
  border: 1px solid #bae6fd;
  border-radius: 5px;
  padding: 4px 10px;
  font-size: 12.5px;
  font-weight: 600;
  cursor: pointer;
  text-decoration: none;
  transition: all 0.15s;
  margin: 2px 2px;
  font-family: inherit;
}

.doc-file-btn:hover {
  background: #0178BC;
  color: #ffffff;
  border-color: #0178BC;
  box-shadow: 0 2px 4px rgba(1, 120, 188, 0.25);
}

.doc-file-btn-dark {
  background: #2c3e50;
  color: #ffffff;
  border: 1px solid #34495e;
}

.doc-file-btn-dark:hover {
  background: #0178BC;
  border-color: #0178BC;
}

.doc-file-badge {
  background: #0284c7;
  color: #fff;
  border-radius: 3px;
  font-size: 10px;
  padding: 1px 5px;
  text-transform: uppercase;
}
"""

with open(os.path.join(BASE_DIR, "file_viewer_modal.css"), "w", encoding="utf-8") as f:
    f.write(css_content)

js_template = """// =============================================================================
// MedLink LIS: Interactive File & DDL Modal Viewer
// Works offline (file://) and over HTTP (http://localhost:8088)
// =============================================================================

(function() {
  const MEDLINK_FILES_DB = __FILES_JSON__;

  function createModalDOM() {
    if (document.getElementById('medlinkFileModalOverlay')) return;

    const overlay = document.createElement('div');
    overlay.id = 'medlinkFileModalOverlay';
    overlay.innerHTML = '<div class="medlink-modal-card" onclick="event.stopPropagation()">' +
      '<div class="medlink-modal-header">' +
        '<div class="medlink-modal-title-area">' +
          '<span class="medlink-modal-badge" id="medlinkModalBadge">SQL DDL</span>' +
          '<div>' +
            '<div class="medlink-modal-filename" id="medlinkModalFilename">file.sql</div>' +
            '<div class="medlink-modal-path" id="medlinkModalPath">sql/file.sql</div>' +
          '</div>' +
        '</div>' +
        '<div class="medlink-modal-actions">' +
          '<button class="medlink-btn" id="medlinkCopyBtn" title="Скопіювати вміст">' +
            '<span>📋</span> <span id="medlinkCopyText">Скопіювати</span>' +
          '</button>' +
          '<a class="medlink-btn medlink-btn-primary" id="medlinkDownloadBtn" download="" href="#" title="Завантажити">' +
            '<span>💾</span> <span>Завантажити</span>' +
          '</a>' +
          '<button class="medlink-btn-close" id="medlinkCloseBtn" title="Закрити (Esc)">&times;</button>' +
        '</div>' +
      '</div>' +
      '<div class="medlink-modal-toolbar">' +
        '<span id="medlinkModalMeta">Завантаження...</span>' +
        '<span>Підтримка прокручування (Scrollable) | MedLink LIS 3.0</span>' +
      '</div>' +
      '<div class="medlink-modal-body" id="medlinkModalBody">' +
        '<pre class="medlink-code-pre"><code id="medlinkCodeContainer"></code></pre>' +
      '</div>' +
    '</div>';

    document.body.appendChild(overlay);

    overlay.addEventListener('click', closeMedlinkModal);
    document.getElementById('medlinkCloseBtn').addEventListener('click', closeMedlinkModal);

    document.addEventListener('keydown', function(e) {
      if (e.key === 'Escape' && overlay.classList.contains('active')) {
        closeMedlinkModal();
      }
    });

    document.getElementById('medlinkCopyBtn').addEventListener('click', function() {
      const code = document.getElementById('medlinkCodeContainer').innerText;
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(code).then(() => showCopiedFeedback());
      } else {
        const ta = document.createElement('textarea');
        ta.value = code;
        document.body.appendChild(ta);
        ta.select();
        document.execCommand('copy');
        document.body.removeChild(ta);
        showCopiedFeedback();
      }
    });
  }

  function showCopiedFeedback() {
    const copyText = document.getElementById('medlinkCopyText');
    const oldText = copyText.innerText;
    copyText.innerText = '✓ Скопійовано!';
    setTimeout(() => {
      copyText.innerText = oldText;
    }, 2000);
  }

  function highlightSyntax(code, lang) {
    let safe = code
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');

    if (lang === 'sql') {
      safe = safe.replace(/(--.*?$)/gm, '<span style="color:#6a9955;">$1</span>');
      const keywords = ['SELECT','FROM','WHERE','INSERT','INTO','VALUES','UPDATE','SET','DELETE','CREATE','TABLE','INDEX','IF','NOT','EXISTS','PRIMARY','KEY','DEFAULT','REFERENCES','CASCADE','UNIQUE','FOREIGN','CONSTRAINT','START','TRANSACTION','COMMIT','DO','BEGIN','END','OR','REPLACE','VIEW','SERIAL','UUID','VARCHAR','INT','BOOLEAN','TIMESTAMP','WITHOUT','TIME','ZONE','NUMERIC','JSONB','TEXT','DATE','JOIN','LEFT','ON'];
      const kwRegex = new RegExp('\\\\b(' + keywords.join('|') + ')\\\\b', 'gi');
      safe = safe.replace(kwRegex, '<span style="color:#569cd6; font-weight:600;">$1</span>');
      safe = safe.replace(/('([^'\\\\]|\\\\.)*')/g, '<span style="color:#ce9178;">$1</span>');
    } else if (lang === 'json') {
      safe = safe.replace(/("(\\\\u[a-zA-Z0-9]{4}|\\\\[^u]|[^\\\\"])*"(\\s*:)?|\\b(true|false|null)\\b|-?\\d+(?:\\.\\d*)?(?:[eE][+\\-]?\\d+)?)/g, function (match) {
        let cls = '#b5cea8';
        if (/^"/.test(match)) {
          if (/:$/.test(match)) {
            cls = '#9cdcfe';
          } else {
            cls = '#ce9178';
          }
        } else if (/true|false/.test(match)) {
          cls = '#569cd6';
        } else if (/null/.test(match)) {
          cls = '#569cd6';
        }
        return '<span style="color:' + cls + ';">' + match + '</span>';
      });
    } else if (lang === 'csharp') {
      safe = safe.replace(/(\\/\\/.*?$)/gm, '<span style="color:#6a9955;">$1</span>');
      const csKw = ['public','private','protected','internal','class','interface','struct','enum','async','await','Task','void','string','int','bool','double','decimal','override','new','return','if','else','foreach','in','var','using','namespace','try','catch','throw'];
      const csRegex = new RegExp('\\\\b(' + csKw.join('|') + ')\\\\b', 'g');
      safe = safe.replace(csRegex, '<span style="color:#569cd6; font-weight:600;">$1</span>');
      safe = safe.replace(/("([^"\\\\]|\\\\.)*")/g, '<span style="color:#ce9178;">$1</span>');
    }

    return safe;
  }

  window.openMedlinkFileModal = function(fileKey) {
    createModalDOM();
    const overlay = document.getElementById('medlinkFileModalOverlay');
    const badge = document.getElementById('medlinkModalBadge');
    const filenameEl = document.getElementById('medlinkModalFilename');
    const pathEl = document.getElementById('medlinkModalPath');
    const metaEl = document.getElementById('medlinkModalMeta');
    const downloadBtn = document.getElementById('medlinkDownloadBtn');
    const codeEl = document.getElementById('medlinkCodeContainer');
    const modalBody = document.getElementById('medlinkModalBody');

    const cleanKey = fileKey.replace(/\\\\/g, '/');
    let fileInfo = null;

    for (const k in MEDLINK_FILES_DB) {
      if (k === cleanKey || cleanKey.endsWith(k) || k.endsWith(cleanKey)) {
        fileInfo = MEDLINK_FILES_DB[k];
        break;
      }
    }

    if (fileInfo) {
      badge.innerText = fileInfo.title;
      const parts = fileInfo.path.split('/');
      filenameEl.innerText = parts[parts.length - 1];
      pathEl.innerText = 'C:\\\\__MEDLINK___\\\\LABA\\\\' + fileInfo.path.replace(/\\//g, '\\\\');
      metaEl.innerText = 'Розмір: ' + (fileInfo.size / 1024).toFixed(1) + ' КБ | Рядків: ' + fileInfo.lines + ' | Мова: ' + fileInfo.lang.toUpperCase();
      
      codeEl.innerHTML = highlightSyntax(fileInfo.content, fileInfo.lang);
      
      const blob = new Blob([fileInfo.content], { type: 'text/plain;charset=utf-8' });
      downloadBtn.href = URL.createObjectURL(blob);
      downloadBtn.download = parts[parts.length - 1];

      overlay.classList.add('active');
      modalBody.scrollTop = 0;
    } else {
      fetch(fileKey)
        .then(res => res.text())
        .then(text => {
          badge.innerText = 'File Preview';
          const p = fileKey.split('/');
          filenameEl.innerText = p[p.length - 1];
          pathEl.innerText = fileKey;
          metaEl.innerText = 'Завантажено: ' + (text.length / 1024).toFixed(1) + ' КБ';
          codeEl.innerText = text;
          overlay.classList.add('active');
          modalBody.scrollTop = 0;
        })
        .catch(err => {
          alert('Файл не знайдено: ' + fileKey);
        });
    }
  };

  window.closeMedlinkModal = function() {
    const overlay = document.getElementById('medlinkFileModalOverlay');
    if (overlay) {
      overlay.classList.remove('active');
    }
  };

  document.addEventListener('DOMContentLoaded', function() {
    createModalDOM();
  });
})();
"""

files_json = json.dumps(file_db, ensure_ascii=False)
js_final = js_template.replace("__FILES_JSON__", files_json)

with open(os.path.join(BASE_DIR, "file_viewer_modal.js"), "w", encoding="utf-8") as f:
    f.write(js_final)

# Copy to specs_html
specs_dir = os.path.join(BASE_DIR, "specs_html")
shutil.copyfile(os.path.join(BASE_DIR, "file_viewer_modal.css"), os.path.join(specs_dir, "file_viewer_modal.css"))
shutil.copyfile(os.path.join(BASE_DIR, "file_viewer_modal.js"), os.path.join(specs_dir, "file_viewer_modal.js"))

print("Viewer CSS and JS successfully generated!")
