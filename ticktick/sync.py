"""TickTick Open API v1 client — OAuth2 + task management."""
import base64
import http.server
import threading
import urllib.parse
import webbrowser
from datetime import date

try:
    import requests as _req
    HAS_REQUESTS = True
except ImportError:
    _req = None
    HAS_REQUESTS = False

REDIRECT_PORT = 8765
REDIRECT_URI = f"http://localhost:{REDIRECT_PORT}/callback"
AUTH_URL = "https://ticktick.com/oauth/authorize"
TOKEN_URL = "https://ticktick.com/oauth/token"
API_BASE = "https://api.ticktick.com/open/v1"
PROJECT_NAME = "Netherlands Plan"


class TickTickClient:
    def __init__(self, config: dict):
        tt = config.get("ticktick", {})
        self.client_id = tt.get("client_id", "").strip()
        self.client_secret = tt.get("client_secret", "").strip()
        self.access_token = tt.get("access_token", "").strip()
        self.refresh_token_val = tt.get("refresh_token", "").strip()
        self.on_tokens_updated = None  # callable(access_token, refresh_token)

    @property
    def is_configured(self):
        return bool(self.client_id and self.client_secret)

    @property
    def is_authorized(self):
        return bool(self.access_token)

    # ── OAuth2 ───────────────────────────────────────────────────────────────

    def authorize(self, on_success=None, on_error=None):
        """Start OAuth2 flow in background threads. Calls on_success() or on_error(msg)."""
        if not HAS_REQUESTS:
            if on_error:
                on_error("Install requests: pip install requests")
            return
        if not self.is_configured:
            if on_error:
                on_error("Set client_id and client_secret in config.json first.")
            return

        code_holder = {"code": None, "error": None}
        done = threading.Event()

        class _Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                qs = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
                if "code" in qs:
                    code_holder["code"] = qs["code"][0]
                elif "error" in qs:
                    code_holder["error"] = qs.get("error_description", ["Unknown error"])[0]
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.end_headers()
                self.wfile.write(
                    b"<html><body style='font-family:sans-serif;padding:40px'>"
                    b"<h2>Authorized!</h2>"
                    b"<p>Return to the Netherlands Mentor app.</p>"
                    b"</body></html>"
                )
                done.set()

            def log_message(self, *_):
                pass

        try:
            server = http.server.HTTPServer(("localhost", REDIRECT_PORT), _Handler)
        except OSError as exc:
            if on_error:
                on_error(f"Cannot start callback server on port {REDIRECT_PORT}: {exc}")
            return

        def _serve():
            while not done.is_set():
                server.handle_request()
            server.server_close()

        threading.Thread(target=_serve, daemon=True).start()

        url = AUTH_URL + "?" + urllib.parse.urlencode({
            "client_id": self.client_id,
            "response_type": "code",
            "redirect_uri": REDIRECT_URI,
            "scope": "tasks:read tasks:write",
        })
        webbrowser.open(url)

        def _wait():
            if not done.wait(timeout=120):
                if on_error:
                    on_error("Authorization timed out (2 minutes).")
                return
            if code_holder["error"]:
                if on_error:
                    on_error(f"TickTick error: {code_holder['error']}")
                return
            try:
                self._exchange_code(code_holder["code"])
                if on_success:
                    on_success()
            except Exception as exc:
                if on_error:
                    on_error(str(exc))

        threading.Thread(target=_wait, daemon=True).start()

    def _exchange_code(self, code):
        creds = base64.b64encode(
            f"{self.client_id}:{self.client_secret}".encode()
        ).decode()
        resp = _req.post(
            TOKEN_URL,
            headers={
                "Authorization": f"Basic {creds}",
                "Content-Type": "application/x-www-form-urlencoded",
            },
            data={
                "code": code,
                "grant_type": "authorization_code",
                "redirect_uri": REDIRECT_URI,
            },
            timeout=15,
        )
        resp.raise_for_status()
        data = resp.json()
        self.access_token = data["access_token"]
        self.refresh_token_val = data.get("refresh_token", "")
        if self.on_tokens_updated:
            self.on_tokens_updated(self.access_token, self.refresh_token_val)

    def refresh(self):
        if not self.refresh_token_val:
            raise RuntimeError("No refresh token — please reconnect TickTick.")
        creds = base64.b64encode(
            f"{self.client_id}:{self.client_secret}".encode()
        ).decode()
        resp = _req.post(
            TOKEN_URL,
            headers={
                "Authorization": f"Basic {creds}",
                "Content-Type": "application/x-www-form-urlencoded",
            },
            data={
                "grant_type": "refresh_token",
                "refresh_token": self.refresh_token_val,
            },
            timeout=15,
        )
        resp.raise_for_status()
        data = resp.json()
        self.access_token = data["access_token"]
        self.refresh_token_val = data.get("refresh_token", self.refresh_token_val)
        if self.on_tokens_updated:
            self.on_tokens_updated(self.access_token, self.refresh_token_val)

    # ── API core ─────────────────────────────────────────────────────────────

    def _request(self, method, path, _retry=True, **kwargs):
        if not HAS_REQUESTS:
            raise RuntimeError("requests library not installed — pip install requests")
        resp = _req.request(
            method,
            API_BASE + path,
            headers={"Authorization": f"Bearer {self.access_token}"},
            timeout=15,
            **kwargs,
        )
        if resp.status_code == 401 and _retry:
            self.refresh()
            return self._request(method, path, _retry=False, **kwargs)
        resp.raise_for_status()
        return resp.json() if resp.content else {}

    # ── projects ─────────────────────────────────────────────────────────────

    def get_projects(self):
        return self._request("GET", "/project")

    def get_or_create_project(self, name=PROJECT_NAME):
        for p in self.get_projects():
            if p.get("name") == name:
                return p["id"]
        result = self._request("POST", "/project", json={"name": name, "color": "#1a6e8e"})
        return result["id"]

    def get_project_tasks(self, project_id):
        data = self._request("GET", f"/project/{project_id}/data")
        return data.get("tasks", [])

    # ── tasks ────────────────────────────────────────────────────────────────

    def create_task(self, project_id, title, content="", due_date: date = None):
        payload = {
            "projectId": project_id,
            "title": title,
            "content": content,
            "priority": 1,
        }
        if due_date:
            payload["dueDate"] = due_date.strftime("%Y-%m-%dT23:59:00+0000")
            payload["startDate"] = due_date.strftime("%Y-%m-%dT00:00:00+0000")
        return self._request("POST", "/task", json=payload)

    def complete_task(self, project_id, task_id):
        self._request("POST", f"/project/{project_id}/task/{task_id}/complete")
