# -*- coding: utf-8 -*-
import subprocess
import os

chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
if not os.path.exists(chrome):
    chrome = r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"

# Test page to open barcode modal
test_html = """<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <link rel="stylesheet" href="http://localhost:8088/file_viewer_modal.css">
  <script>
    window.location.href = "http://localhost:8088/medlink_lab_frontend/run_prototype.html";
  </script>
</head>
<body></body>
</html>
"""

# Let's test phlebotomy view by running script in Chrome
shot1 = r"C:\__MEDLINK___\LABA\phlebotomy_screenshot.png"
shot2 = r"C:\__MEDLINK___\LABA\pdf_modal_screenshot.png"

# We can trigger view switch or open dialog via URL or script
script_phleb = """
  const app = document.querySelector('#q-app').__vue__;
  app.setView('phlebotomy', 'Пункт забору (Забір)');
  setTimeout(() => {
    app.openSampleBarcode(app.phlebotomyQueue[0]);
  }, 400);
"""

# Write a small runner script with node or python to evaluate in chrome
# Or let's test directly with node if puppeteer is installed, or with a simple test page that mounts Vue and opens the dialog
print("Test runner ready.")
