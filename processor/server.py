"""Private file processor. No database, financial permissions, remote URLs or executable input."""
from __future__ import annotations
import hmac, json, os, socket, struct, subprocess, sys, tempfile, threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
MAX_BYTES = 10 * 1024 * 1024
SLOTS = threading.BoundedSemaphore(2)
KEY = os.environ.get('PROCESSOR_KEY', '')

def scan(data: bytes) -> None:
    # ClamAV INSTREAM: no client-controlled filenames, paths or destination hosts.
    with socket.create_connection((os.environ.get('CLAMAV_HOST', 'antivirus'), 3310), timeout=20) as conn:
        conn.sendall(b'zINSTREAM\0')
        for offset in range(0, len(data), 65536):
            chunk = data[offset:offset+65536]
            conn.sendall(struct.pack('!I', len(chunk)) + chunk)
        conn.sendall(struct.pack('!I', 0))
        response = b''
        while b'\0' not in response and len(response) < 4096:
            chunk = conn.recv(4096)
            if not chunk: break
            response += chunk
        if b'FOUND' in response: raise ValueError('blocked')
        if not response.rstrip(b'\0\n').endswith(b'OK'): raise RuntimeError('scanner unavailable')

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args): pass  # Do not log data, credentials or original filenames.
    def reply(self, status: int, payload: dict):
        body = json.dumps(payload, ensure_ascii=False).encode()
        self.send_response(status); self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(body))); self.send_header('Cache-Control', 'no-store')
        self.end_headers(); self.wfile.write(body)
    def do_GET(self):
        self.reply(200 if self.path == '/health' else 404, {'status': 'ok' if self.path == '/health' else 'not_found'})
    def do_POST(self):
        if self.path != '/inspect' or not KEY or not hmac.compare_digest(self.headers.get('X-Processor-Key', ''), KEY):
            self.reply(403, {'error': 'forbidden'}); return
        try: length = int(self.headers.get('Content-Length', '0'))
        except ValueError: length = 0
        if not 0 < length <= MAX_BYTES: self.reply(413, {'error': 'size'}); return
        if not SLOTS.acquire(blocking=False): self.reply(503, {'error': 'busy'}); return
        try:
            self.connection.settimeout(25)
            data = self.rfile.read(length)
            if len(data) != length: raise ValueError('incomplete')
            scan(data)
            with tempfile.TemporaryDirectory(prefix='document-') as tmp:
                path = Path(tmp)/'input.bin'; path.write_bytes(data)
                result = subprocess.run([sys.executable, '-I', str(Path(__file__).with_name('inspect_file.py')), str(path)], capture_output=True, timeout=30, check=False)
                if result.returncode != 0: raise ValueError('invalid_document')
                payload = json.loads(result.stdout)
                self.reply(200, payload)
        except ValueError:
            self.reply(200, {'clean': False, 'analysisState': 'Blocked', 'note': 'Arquivo não liberado; envie outro documento.'})
        except (OSError, RuntimeError, subprocess.TimeoutExpired, json.JSONDecodeError):
            self.reply(503, {'error': 'processor_unavailable'})
        finally: SLOTS.release()

if __name__ == '__main__':
    if len(KEY) < 32: raise RuntimeError('PROCESSOR_KEY must be configured')
    ThreadingHTTPServer(('0.0.0.0', 8090), Handler).serve_forever()
