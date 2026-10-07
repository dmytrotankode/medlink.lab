# -*- coding: utf-8 -*-
import http.server
import socketserver
import os
import sys

PORT = 8085
DIRECTORY = r"C:\__MEDLINK___\LABA"

class QuietHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DIRECTORY, **kwargs)

    def log_message(self, format, *args):
        if "favicon" not in str(args[0]):
            sys.stderr.write(f"[{self.log_date_time_string()}] {format % args}\n")
            sys.stderr.flush()

class SafeServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    allow_reuse_address = True
    daemon_threads = True

    def handle_error(self, request, client_address):
        # Ignore client abrupt disconnects
        pass

def run():
    os.chdir(DIRECTORY)
    with SafeServer(("0.0.0.0", PORT), QuietHandler) as httpd:
        print(f"MedLink LIS Safe Server running on http://0.0.0.0:{PORT}...")
        sys.stdout.flush()
        httpd.serve_forever()

if __name__ == "__main__":
    run()
