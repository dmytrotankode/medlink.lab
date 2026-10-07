import subprocess
import time
import urllib.request
import json
import asyncio

import websockets

async def test_page_render():
    chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    p = subprocess.Popen([chrome, "--headless=new", "--remote-debugging-port=9224", "http://localhost:8088/medlink_lab_frontend/run_prototype.html"])
    await asyncio.sleep(2)
    try:
        tabs = json.loads(urllib.request.urlopen("http://localhost:9224/json").read())
        page_tab = next(t for t in tabs if "run_prototype.html" in t.get("url", ""))
        ws_url = page_tab["webSocketDebuggerUrl"]
        
        async with websockets.connect(ws_url) as ws:
            await ws.send(json.dumps({"id": 1, "method": "Runtime.enable"}))
            # Wait for Vue to mount
            await asyncio.sleep(1)
            
            # Check title and rendered DOM
            eval_cmd = {
                "id": 2,
                "method": "Runtime.evaluate",
                "params": {
                    "expression": "document.title + ' | Elements in #q-app: ' + document.querySelectorAll('#q-app *').length + ' | Buttons: ' + document.querySelectorAll('button').length"
                }
            }
            await ws.send(json.dumps(eval_cmd))
            while True:
                msg = await ws.recv()
                data = json.loads(msg)
                if data.get("id") == 2:
                    print("PAGE EVALUATION RESULT:", json.dumps(data.get("result", {}), ensure_ascii=False))
                    break
    finally:
        p.kill()

asyncio.run(test_page_render())
