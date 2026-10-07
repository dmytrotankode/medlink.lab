import subprocess
import os

chrome_paths = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Users\tanko\AppData\Local\Google\Chrome\Application\chrome.exe"
]

chrome = None
for p in chrome_paths:
    if os.path.exists(p):
        chrome = p
        break

if not chrome:
    print("Chrome not found")
    exit(1)

url = "http://localhost:8088/medlink_lab_frontend/run_prototype.html"
screenshot_path = r"C:\__MEDLINK___\LABA\prototype_screenshot.png"
dom_path = r"C:\__MEDLINK___\LABA\dumped_dom.html"

# Run chrome to dump DOM
cmd_dom = [chrome, "--headless=new", "--disable-gpu", "--dump-dom", url]
res_dom = subprocess.run(cmd_dom, capture_output=True, text=True, encoding="utf-8")
with open(dom_path, "w", encoding="utf-8") as f:
    f.write(res_dom.stdout)

# Run chrome to capture screenshot
cmd_shot = [chrome, "--headless=new", "--disable-gpu", "--window-size=1600,1000", f"--screenshot={screenshot_path}", url]
subprocess.run(cmd_shot, capture_output=True)

print(f"DOM length: {len(res_dom.stdout)}")
print(f"Contains 'q-layout': {'q-layout' in res_dom.stdout}")
print(f"Contains 'q-table': {'q-table' in res_dom.stdout}")
print(f"Contains '{{' (raw mustache): {'{{' in res_dom.stdout}")
print(f"Screenshot exists: {os.path.exists(screenshot_path)}, size: {os.path.getsize(screenshot_path) if os.path.exists(screenshot_path) else 0}")
