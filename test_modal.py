# -*- coding: utf-8 -*-
import subprocess
import os
import time

chrome_path = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
if not os.path.exists(chrome_path):
    chrome_path = r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"

test_url = "http://localhost:8088/ТЗ_ЛІС_MedLink_v3_Повне.html#sec-appendix"
screenshot_path = r"C:\__MEDLINK___\LABA\modal_test_screenshot.png"

# Dump DOM
cmd_dom = [chrome_path, "--headless=new", "--disable-gpu", "--dump-dom", test_url]
res_dom = subprocess.run(cmd_dom, capture_output=True, text=True, encoding="utf-8")

print("DOM length:", len(res_dom.stdout))
print("Contains 'medlinkFileModalOverlay':", "medlinkFileModalOverlay" in res_dom.stdout)
print("Contains 'openMedlinkFileModal':", "openMedlinkFileModal" in res_dom.stdout)
print("Contains '01_lis_schema_core.sql':", "01_lis_schema_core.sql" in res_dom.stdout)

# Take screenshot of section 15
cmd_shot = [chrome_path, "--headless=new", "--disable-gpu", "--window-size=1600,1200", f"--screenshot={screenshot_path}", test_url]
subprocess.run(cmd_shot, capture_output=True)
print("Screenshot generated:", os.path.exists(screenshot_path))
