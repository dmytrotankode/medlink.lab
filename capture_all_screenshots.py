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

VIEWS_TO_CAPTURE = [
    {
        "name": "01_workstation",
        "action": "document.querySelector('#q-app').__vue__.setView('workstation', 'Робочий стіл лаборанта');",
        "desc": "Робочий стіл лаборанта: робочий журнал, статус аналізаторів, автоматичний прийом ASTM/HL7"
    },
    {
        "name": "02_qc_levey_jennings",
        "action": "document.querySelector('#q-app').__vue__.setView('qc', 'Контроль якості (ВКЯ)');",
        "desc": "Внутрішній контроль якості: Карта Леві-Дженнінгса, зони SD, порушення правила Вестгарда 1-3s, блокування (Lockout)"
    },
    {
        "name": "03_validation_kep",
        "action": "document.querySelector('#q-app').__vue__.setView('validation', 'Валідація результатів & КЕП');",
        "desc": "Валідація лікаря-лаборанта: критичні CITO значення, дельта-чек 72 год (+185%), накладення КЕП"
    },
    {
        "name": "04_patient_trend",
        "action": "document.querySelector('#q-app').__vue__.setView('patient', 'Кабінет пацієнта');",
        "desc": "Кабінет пацієнта: трекінг статусу замовлення, динаміка показника у часі з коридором референсної норми"
    },
    {
        "name": "05_biobank_8x12",
        "action": "document.querySelector('#q-app').__vue__.setView('biobank', 'Біобанк та кріо-архів');",
        "desc": "Біобанк та кріо-архів: 8х12 матриця (96 комірок) -80°C, паспорт зразка, журнал дефростацій"
    },
    {
        "name": "06_microbiology_eucast",
        "action": "document.querySelector('#q-app').__vue__.setView('microbiology', 'Бактеріологія & EUCAST');",
        "desc": "Бактеріологія: антибіотикограма за EUCAST v14.0, шкали зон затримки росту (S/I/R), фенотипи полірезистентності"
    },
    {
        "name": "07_tat_analytics",
        "action": "document.querySelector('#q-app').__vue__.setView('tat', 'Аналітика & TAT');",
        "desc": "Операційна аналітика: Turnaround Time (TAT), waterfall поетапної тривалості (SLA), брак біоматеріалу"
    },
    {
        "name": "08_logistics_coldchain",
        "action": "document.querySelector('#q-app').__vue__.setView('logistics', 'Логістика & Термоконтроль');",
        "desc": "Логістика та холодовий ланцюг: термоконтейнери, температурний графік з датчиків (+2..+8°C), фіксація перегріву"
    },
    {
        "name": "09_phlebotomy_barcode_modal",
        "action": "document.querySelector('#q-app').__vue__.setView('phlebotomy', 'Пункт забору'); document.querySelector('#q-app').__vue__.openSampleBarcode(document.querySelector('#q-app').__vue__.phlebotomyQueue[0]);",
        "desc": "Пункт забору: термостікер пробірки зі штрихкодом Code128, колірне маркування кришки та черговість взяття"
    },
    {
        "name": "10_patient_pdf_modal",
        "action": "document.querySelector('#q-app').__vue__.setView('patient', 'Кабінет пацієнта'); document.querySelector('#q-app').__vue__.openPdfReport(null);",
        "desc": "Офіційний клінічний лабораторний звіт (PDF): цифрова печатка лікаря КЕП, перевірочний QR-код, референси"
    },
    {
        "name": "11_panic_cito_modal",
        "action": "document.querySelector('#q-app').__vue__.setView('workstation', 'Робочий стіл'); document.querySelector('#q-app').__vue__.openPanicAlert(document.querySelector('#q-app').__vue__.worklist[0]);",
        "desc": "Журнал CITO: екстрене сповіщення про критичне значення, реєстрація телефонного дзвінка лікуючому лікарю"
    },
    {
        "name": "12_qc_lockout_modal",
        "action": "document.querySelector('#q-app').__vue__.setView('qc', 'Контроль якості'); document.querySelector('#q-app').__vue__.showQcUnlockDialog = true;",
        "desc": "Протокол розблокування аналізатора: фіксація коригувальних дій (Corrective Action Log) після збою Вестгарда"
    }
]

async def run():
    chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    p = subprocess.Popen([
        chrome,
        "--headless=new",
        "--disable-gpu",
        "--window-size=1600,1050",
        "--remote-debugging-port=9224",
        "http://localhost:8088/medlink_lab_frontend/run_prototype.html"
    ])
    await asyncio.sleep(2)

    try:
        tabs = json.loads(urllib.request.urlopen("http://localhost:9224/json").read())
        page_tab = next(t for t in tabs if "run_prototype.html" in t.get("url", ""))
        ws_url = page_tab["webSocketDebuggerUrl"]

        async with websockets.connect(ws_url, max_size=50*1024*1024) as ws:
            await ws.send(json.dumps({"id": 1, "method": "Runtime.enable"}))
            await ws.send(json.dumps({"id": 2, "method": "Page.enable"}))

            # Set viewport
            await ws.send(json.dumps({
                "id": 3,
                "method": "Emulation.setDeviceMetricsOverride",
                "params": {
                    "width": 1600,
                    "height": 1050,
                    "deviceScaleFactor": 1,
                    "mobile": False
                }
            }))

            msg_id = 10
            for item in VIEWS_TO_CAPTURE:
                msg_id += 1
                # Execute action to switch view or open modal
                await ws.send(json.dumps({
                    "id": msg_id,
                    "method": "Runtime.evaluate",
                    "params": {"expression": item["action"]}
                }))
                await asyncio.sleep(0.5)

                # Capture screenshot
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
print("All screenshots successfully captured in:", SCREENSHOT_DIR)
