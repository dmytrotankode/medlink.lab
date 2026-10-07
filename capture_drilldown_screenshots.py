import asyncio
import subprocess
import time
import urllib.request
import json
import os
import base64
import websockets

SCREENSHOT_DIR = r"C:\__MEDLINK___\LABA\screenshots"
os.makedirs(SCREENSHOT_DIR, exist_ok=True)

ITEMS_TO_CAPTURE = [
    {
        "name": "13_norms_services_catalog",
        "action": "document.querySelector('#q-app').__vue__.setView('norms', 'Довідник послуг, норми та методики');",
        "desc": "Каталог лабораторних послуг: картки з бейджами комбінацій, категоріями та дією провалювання (Drill-Down)"
    },
    {
        "name": "14_service_detail_modal_drilldown",
        "action": "document.querySelector('#q-app').__vue__.setView('norms', 'Довідник послуг'); document.querySelector('#q-app').__vue__.openServiceDetail(document.querySelector('#q-app').__vue__.servicesCatalog[1]);", # PROG
        "desc": "Детальна картка послуги Прогестерон: Матриця комбінацій норм (Delphi style) за фазами циклу та триместрами"
    },
    {
        "name": "15_service_resolver_playground",
        "action": "document.querySelector('#q-app').__vue__.serviceTab = 'calculator'; document.querySelector('#q-app').__vue__.executeSimulateResolve();",
        "desc": "Інтерактивний симулятор підбору референсу: живий розрахунок активного правила у реальному часі"
    }
]

async def run():
    chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    p = subprocess.Popen([
        chrome,
        "--headless=new",
        "--disable-gpu",
        "--window-size=1600,1050",
        "--remote-debugging-port=9225",
        "http://localhost:8088/medlink_lab_frontend/run_prototype.html"
    ])
    await asyncio.sleep(2)

    try:
        tabs = json.loads(urllib.request.urlopen("http://localhost:9225/json").read())
        page_tab = next(t for t in tabs if "run_prototype.html" in t.get("url", ""))
        ws_url = page_tab["webSocketDebuggerUrl"]

        async with websockets.connect(ws_url, max_size=50*1024*1024) as ws:
            await ws.send(json.dumps({"id": 1, "method": "Runtime.enable"}))
            await ws.send(json.dumps({"id": 2, "method": "Page.enable"}))

            msg_id = 100
            for item in ITEMS_TO_CAPTURE:
                msg_id += 1
                await ws.send(json.dumps({
                    "id": msg_id,
                    "method": "Runtime.evaluate",
                    "params": {"expression": item["action"]}
                }))
                await asyncio.sleep(0.8)

                msg_id += 1
                await ws.send(json.dumps({
                    "id": msg_id,
                    "method": "Page.captureScreenshot",
                    "params": {"format": "png"}
                }))

                while True:
                    resp = json.loads(await ws.recv())
                    if resp.get("id") == msg_id:
                        img_data = base64.b64decode(resp["result"]["data"])
                        file_path = os.path.join(SCREENSHOT_DIR, f"{item['name']}.png")
                        with open(file_path, "wb") as f:
                            f.write(img_data)
                        print(f"Captured: {item['name']}.png ({len(img_data)} bytes)")
                        break

    finally:
        p.kill()

asyncio.run(run())
print("All drill-down screenshots successfully captured!")
