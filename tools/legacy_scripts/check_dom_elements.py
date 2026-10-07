# -*- coding: utf-8 -*-
import subprocess, re
chrome_path = r'C:\Program Files\Google\Chrome\Application\chrome.exe'
res = subprocess.run([chrome_path, '--headless=new', '--dump-dom', 'http://localhost:8085/medlink_lab_frontend/run_prototype.html'], capture_output=True, text=True, encoding='utf-8')
dom = res.stdout

for tag in re.findall(r'<[a-z]+[^>]*class="[^"]*(?:page-container|q-drawer)[^"]*"[^>]*>', dom):
    print("Found element:", tag[:120])
