import subprocess
import time
import urllib.request
import json
import asyncio
import websockets

async def test_ui_interactions():
    chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    p = subprocess.Popen([chrome, "--headless=new", "--remote-debugging-port=9225", "http://localhost:8088/medlink_lab_frontend/run_prototype.html"])
    await asyncio.sleep(2)
    try:
        tabs = json.loads(urllib.request.urlopen("http://localhost:9225/json").read())
        page_tab = next(t for t in tabs if "run_prototype.html" in t.get("url", ""))
        ws_url = page_tab["webSocketDebuggerUrl"]
        
        async with websockets.connect(ws_url) as ws:
            await ws.send(json.dumps({"id": 1, "method": "Runtime.enable"}))
            await asyncio.sleep(1)
            
            async def evaluate_js(expr):
                cmd_id = int(time.time() * 1000) % 1000000
                await ws.send(json.dumps({
                    "id": cmd_id,
                    "method": "Runtime.evaluate",
                    "params": {"expression": expr, "returnByValue": True, "awaitPromise": True}
                }))
                while True:
                    msg = await ws.recv()
                    data = json.loads(msg)
                    if data.get("id") == cmd_id:
                        return data.get("result", {}).get("result", {}).get("value")

            # 1. Check current view
            v = await evaluate_js("window.app = document.querySelector('#q-app').__vue__; window.app.currentView")
            print("1. Current View:", v)

            # 2. Switch to Biomaterials dictionary
            await evaluate_js("window.app.setView('dict_biomaterials')")
            v2 = await evaluate_js("window.app.currentView")
            print("2. Switched View to:", v2)

            # 3. Test open Biomaterial Card
            bio_res = await evaluate_js("""
                window.app.openBiomaterialCard(window.app.biomaterialsList[0]);
                ({ open: window.app.showBiomaterialModal, name: window.app.activeBiomaterial.name, code: window.app.activeBiomaterial.code })
            """)
            print("3. Opened Biomaterial Card:", json.dumps(bio_res, ensure_ascii=True))

            # 4. Switch to Tubes and test open Tube Card
            await evaluate_js("window.app.setView('dict_tubes')")
            tube_res = await evaluate_js("""
                window.app.openTubeCard(window.app.tubesList[0]);
                ({ open: window.app.showTubeModal, name: window.app.activeTube.name, color: window.app.activeTube.colorName })
            """)
            print("4. Opened Tube Card:", json.dumps(tube_res, ensure_ascii=True))

            # 5. Switch to Analyzers and test open Analyzer Card
            await evaluate_js("window.app.setView('dict_analyzers')")
            an_res = await evaluate_js("""
                window.app.openAnalyzerCard(window.app.analyzerModelsList[0]);
                ({ open: window.app.showAnalyzerModal, name: window.app.activeAnalyzer.name, protocol: window.app.activeAnalyzer.interfaceType })
            """)
            print("5. Opened Analyzer Card:", json.dumps(an_res, ensure_ascii=True))

            # 6. Switch to Parameters and test open Parameter Card
            await evaluate_js("window.app.setView('dict_parameters')")
            param_res = await evaluate_js("""
                window.app.openParamCard(window.app.parametersList[0]);
                ({ open: window.app.showParamModal, name: window.app.activeParam.name, loinc: window.app.activeParam.loinc })
            """)
            print("6. Opened Parameter Card:", json.dumps(param_res, ensure_ascii=True))

            # 7. Open Delphi Norms Card directly from parameter or norms view
            delphi_res = await evaluate_js("""
                window.app.openNormsForParam(window.app.parametersList[0]);
                ({
                    modalOpen: window.app.showServiceDetailDialog,
                    tab: window.app.serviceTab,
                    selectedService: window.app.selectedService.name,
                    code: window.app.selectedService.code
                })
            """)
            print("7. Opened Delphi Service Card with Norms Constructor:", json.dumps(delphi_res, ensure_ascii=True))

            # 8. Test in-card resolver
            resolver_res = await evaluate_js("""
                (async () => {
                    window.app.inCardTest.value = 7.8;
                    window.app.testInCardResolver();
                    await new Promise(r => setTimeout(r, 800));
                    return window.app.inCardResult;
                })()
            """)
            print("8. In-Card Resolver Result for Value 7.8 (Hyperglycemia):", json.dumps(resolver_res, ensure_ascii=True))

            # 9. Test saving a new combination in constructor
            save_res = await evaluate_js("""
                (async () => {
                    window.app.newCombForm.norm_name = 'Вагітні 2 триместр (Тест UI)';
                    window.app.newCombForm.norm_low = 3.9;
                    window.app.newCombForm.norm_high = 5.6;
                    window.app.newCombForm.is_pregnancy = 1;
                    window.app.newCombForm.pregnancy_week_from = 14;
                    window.app.newCombForm.pregnancy_week_to = 28;
                    window.app.saveNewCombination();
                    await new Promise(r => setTimeout(r, 800));
                    return window.app.selectedServiceCombinations.length;
                })()
            """)
            print("9. New Combination Saved, Total Combinations in Card:", save_res)


    finally:
        p.kill()

asyncio.run(test_ui_interactions())
