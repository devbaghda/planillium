# Local Anthropic-messages stub (SSE). Delay/reply read per request from files next to the log. Scratch QA only.
import http.server, json, time, os, sys
D = sys.argv[1]; PORT = int(sys.argv[2])
class Hd(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        n = int(self.headers.get("Content-Length", 0)); body = self.rfile.read(n)
        with open(D + r"\stub_log.txt", "a", encoding="utf-8") as f:
            f.write(f"{time.strftime('%H:%M:%S')} POST {self.path} len={n} key={self.headers.get('x-api-key','')[:6]}\n")
        open(D + r"\stub_last_body.json", "wb").write(body)
        delay = float(open(D + r"\stub_delay.txt").read()) if os.path.exists(D + r"\stub_delay.txt") else 0
        reply = open(D + r"\stub_reply.txt", encoding="utf-8").read() if os.path.exists(D + r"\stub_reply.txt") else "OK"
        self.send_response(200); self.send_header("Content-Type", "text/event-stream"); self.end_headers()
        def ev(o): self.wfile.write(("data: " + json.dumps(o) + "\n\n").encode()); self.wfile.flush()
        try:
            ev({"type": "message_start", "message": {"usage": {"input_tokens": n // 4}}})
            time.sleep(delay)
            ev({"type": "content_block_delta", "delta": {"type": "text_delta", "text": reply}})
            ev({"type": "message_delta", "delta": {"stop_reason": "end_turn"}, "usage": {"output_tokens": len(reply) // 4}})
            ev({"type": "message_stop"})
        except Exception as ex:
            open(D + r"\stub_log.txt", "a").write(f"  client gone: {ex}\n")
    def log_message(self, *a): pass
http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Hd).serve_forever()
