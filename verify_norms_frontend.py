import subprocess
import time
import urllib.request
import json
import asyncio

async def test_frontend():
    chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    p = subprocess.Popen([chrome, "--headless=new", "--remote-debugging-port=9225", "http://localhost:8088/medlink_lab_frontend/run_prototype.html"])
    await asyncio.sleep(2)
    try:
        import websockets
        tabs = json.loads(urllib.request.urlopen("http://localhost:9225/json").read())
        page_tab = next(t for t in tabs if "run_prototype.html" in t.get("url", ""))
        ws_url = page_tab["webSocketDebuggerUrl"]
        
        async with websockets.connect(ws_url) as ws:
            async def send_cmd(method, params=None):
                cmd_id = int(time.time() * 1000) % 100000
                await ws.send(json.dumps({"id": cmd_id, "method": method, "params": params or {}}))
                while True:
                    msg = await ws.recv()
                    data = json.loads(msg)
                    if data.get("id") == cmd_id:
                        return data.get("result", {})
            
            await send_cmd("Runtime.enable")
            
            # 1. DOM Count
            r1 = await send_cmd("Runtime.evaluate", {"expression": "document.querySelectorAll('*').length"})
            dom_count = r1.get("result", {}).get("value")
            print("DOM Elements:", dom_count)
            
            # 2. Vue Status
            r2 = await send_cmd("Runtime.evaluate", {"expression": "!!document.querySelector('#q-app').__vue__"})
            print("Vue mounted:", r2.get("result", {}).get("value"))
            
            # 3. Open Modal and trigger cascade
            r3 = await send_cmd("Runtime.evaluate", {"expression": """
                (function() {
                    var app = document.querySelector('#q-app').__vue__;
                    var srv = app.servicesCatalog[0];
                    app.openServiceDetail(srv);
                    return 'Opened ' + srv.name + ', layers count: ' + app.selectedServiceCombinations.length;
                })()
            """})
            print("Open Modal:", r3.get("result", {}).get("value"))
            
            # Wait for cascade resolution fetch to complete
            await asyncio.sleep(2)
            
            # 4. Check cascade results and audit trace
            r4 = await send_cmd("Runtime.evaluate", {"expression": """
                (function() {
                    var app = document.querySelector('#q-app').__vue__;
                    var cr = app.cascadeResult;
                    return {
                        has_cascadeResult: !!cr,
                        winning_layer: cr ? cr.winning_layer.norm_name : null,
                        winning_prio: cr ? cr.winning_layer.priority_order : null,
                        norm_low: cr ? cr.norm_low : null,
                        norm_high: cr ? cr.norm_high : null,
                        status_flag: cr ? cr.status_flag : null,
                        audit_trace_steps: cr ? cr.audit_trace.length : 0,
                        filtered_rules_count: app.filteredCombinations.length
                    };
                })()
            """, "returnByValue": True})
            print("Cascade Result Details:", json.dumps(r4.get("result", {}).get("value"), indent=2, ensure_ascii=False))

    finally:
        p.kill()

asyncio.run(test_frontend())
