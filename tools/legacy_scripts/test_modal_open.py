# -*- coding: utf-8 -*-
import subprocess
import os

chrome_path = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
if not os.path.exists(chrome_path):
    chrome_path = r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"

# We create a tiny test page that automatically opens the modal on load
test_page = r"C:\__MEDLINK___\LABA\test_open_modal.html"
with open(test_page, "w", encoding="utf-8") as f:
    f.write("""<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <link rel="stylesheet" href="file_viewer_modal.css">
</head>
<body style="background: #0f172a; padding: 20px;">
  <button onclick="openMedlinkFileModal('sql/01_lis_schema_core.sql')" class="doc-file-btn">Open DDL</button>
  <script src="file_viewer_modal.js"></script>
  <script>
    window.addEventListener('load', () => {
      openMedlinkFileModal('sql/01_lis_schema_core.sql');
    });
  </script>
</body>
</html>
""")

screenshot_path = r"C:\__MEDLINK___\LABA\modal_open_screenshot.png"
cmd = [
    chrome_path,
    "--headless=new",
    "--disable-gpu",
    "--window-size=1600,1000",
    f"--screenshot={screenshot_path}",
    "http://localhost:8088/test_open_modal.html"
]

subprocess.run(cmd, capture_output=True)
print("Open modal screenshot captured:", os.path.exists(screenshot_path))
