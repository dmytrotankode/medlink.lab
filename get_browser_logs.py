import subprocess
import time
import urllib.request
import json
import asyncio
import os

# Check if websockets is available
try:
    import websockets
except ImportError:
    subprocess.run(["pip", "install", "websockets"])
    import websockets

async def capture_logs():
    chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    p = subprocess.Popen([chrome, "--headless=new", "--remote-debugging-port=9223", "http://localhost:8088/medlink_lab_frontend/run_prototype.html"])
    await asyncio.sleep(2)
    try:
        tabs = json.loads(urllib.request.urlopen("http://localhost:9223/json").read())
        page_tab = next(t for t in tabs if "run_prototype.html" in t.get("url", ""))
        ws_url = page_tab["webSocketDebuggerUrl"]
        
        async with websockets.connect(ws_url) as ws:
            # Enable Console and Log
            await ws.send(json.dumps({"id": 1, "method": "Console.enable"}))
            await ws.send(json.dumps({"id": 2, "method": "Log.enable"}))
            await ws.send(json.dumps({"id": 3, "method": "Runtime.enable"}))
            
            # Reload page to catch all errors during mount
            await ws.send(json.dumps({"id": 4, "method": "Page.reload"}))
            
            end_time = time.time() + 3
            while time.time() < end_time:
                try:
                    msg = await asyncio.wait_for(ws.recv(), timeout=1.0)
                    data = json.loads(msg)
                    method = data.get("method", "")
                    if "Console.messageAdded" in method or "Runtime.consoleAPICalled" in method or "Runtime.exceptionThrown" in method:
                        print("CONSOLE/ERROR:", json.dumps(data, indent=2, ensure_ascii=False))
                except asyncio.TimeoutError:
                    pass
    finally:
        p.kill()

asyncio.run(capture_logs())
