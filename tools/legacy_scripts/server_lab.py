# -*- coding: utf-8 -*-
"""
MedLink Laboratory Dedicated Web & REST API Server
Port: 8088 (guaranteed clean, no conflict with PMG 8085)
Serves static files and real SQLite-backed REST API endpoints (/api/laboratory/*)
"""

import http.server
import socketserver
import os
import sys
import json
import urllib.parse

from api_backend import handle_api_request

PORT = 8088
DIRECTORY = r"C:\__MEDLINK___\LABA"

class LabApiAndStaticServer(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DIRECTORY, **kwargs)

    def log_message(self, format, *args):
        if "favicon" not in str(args[0]):
            sys.stderr.write(f"[{self.log_date_time_string()}] {format % args}\n")
            sys.stderr.flush()

    def send_json(self, data, status=200):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()

    def do_GET(self):
        url_parsed = urllib.parse.urlparse(self.path)
        clean_path = url_parsed.path

        if clean_path.startswith("/api/laboratory/"):
            res = handle_api_request("GET", self.path)
            self.send_json(res)
            return

        super().do_GET()

    def do_POST(self):
        url_parsed = urllib.parse.urlparse(self.path)
        clean_path = url_parsed.path

        if clean_path.startswith("/api/laboratory/"):
            length = int(self.headers.get("Content-Length", 0))
            body_bytes = self.rfile.read(length) if length > 0 else b"{}"
            res = handle_api_request("POST", self.path, body_bytes)
            self.send_json(res)
            return

        self.send_response(404)
        self.end_headers()

    def do_PUT(self):
        url_parsed = urllib.parse.urlparse(self.path)
        clean_path = url_parsed.path

        if clean_path.startswith("/api/laboratory/"):
            length = int(self.headers.get("Content-Length", 0))
            body_bytes = self.rfile.read(length) if length > 0 else b"{}"
            res = handle_api_request("POST", self.path, body_bytes)
            self.send_json(res)
            return

        self.send_response(404)
        self.end_headers()

    def do_DELETE(self):
        url_parsed = urllib.parse.urlparse(self.path)
        clean_path = url_parsed.path

        if clean_path.startswith("/api/laboratory/"):
            res = handle_api_request("DELETE", self.path)
            self.send_json(res)
            return

        self.send_response(404)
        self.end_headers()

class SafeLabServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    allow_reuse_address = True
    daemon_threads = True

    def handle_error(self, request, client_address):
        pass

def run():
    os.chdir(DIRECTORY)
    with SafeLabServer(("0.0.0.0", PORT), LabApiAndStaticServer) as httpd:
        print(f"MedLink LIS Web & Real SQLite API Server running on http://0.0.0.0:{PORT}...")
        print(f"Serving files from: {DIRECTORY}")
        sys.stdout.flush()
        httpd.serve_forever()

if __name__ == "__main__":
    run()
