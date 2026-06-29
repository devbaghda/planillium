import ctypes
import ctypes.wintypes
import threading
import time
from datetime import datetime, time as dtime, timedelta


class _LASTINPUTINFO(ctypes.Structure):
    _fields_ = [("cbSize", ctypes.c_uint), ("dwTime", ctypes.c_uint)]


# Declare explicit types so 64-bit HANDLEs aren't truncated to 32-bit int
_u32 = ctypes.windll.user32
_k32 = ctypes.windll.kernel32

_u32.GetForegroundWindow.restype      = ctypes.c_void_p
_u32.GetForegroundWindow.argtypes     = []
_u32.GetWindowTextLengthW.restype     = ctypes.c_int
_u32.GetWindowTextLengthW.argtypes    = [ctypes.c_void_p]
_u32.GetWindowTextW.restype           = ctypes.c_int
_u32.GetWindowTextW.argtypes          = [ctypes.c_void_p, ctypes.c_wchar_p, ctypes.c_int]
_u32.GetWindowThreadProcessId.restype  = ctypes.wintypes.DWORD
_u32.GetWindowThreadProcessId.argtypes = [ctypes.c_void_p,
                                           ctypes.POINTER(ctypes.wintypes.DWORD)]
_u32.GetLastInputInfo.restype          = ctypes.wintypes.BOOL
_u32.GetLastInputInfo.argtypes         = [ctypes.c_void_p]

_k32.OpenProcess.restype   = ctypes.c_void_p
_k32.OpenProcess.argtypes  = [ctypes.wintypes.DWORD, ctypes.wintypes.BOOL,
                               ctypes.wintypes.DWORD]
_k32.CloseHandle.restype   = ctypes.wintypes.BOOL
_k32.CloseHandle.argtypes  = [ctypes.c_void_p]
_k32.QueryFullProcessImageNameW.restype  = ctypes.wintypes.BOOL
_k32.QueryFullProcessImageNameW.argtypes = [ctypes.c_void_p,
                                             ctypes.wintypes.DWORD,
                                             ctypes.c_wchar_p,
                                             ctypes.POINTER(ctypes.wintypes.DWORD)]
_k32.GetTickCount.restype  = ctypes.wintypes.DWORD
_k32.GetTickCount.argtypes = []


_EXE_APP_NAMES = {
    "telegram.exe":  "Telegram",
    "whatsapp.exe":  "WhatsApp",
    "slack.exe":     "Slack",
    "discord.exe":   "Discord",
    "signal.exe":    "Signal",
    "viber.exe":     "Viber",
    "skype.exe":     "Skype",
}


def _active_window_title():
    hwnd = _u32.GetForegroundWindow()
    length = _u32.GetWindowTextLengthW(hwnd)
    buf = ctypes.create_unicode_buffer(max(length, 0) + 1)
    if length:
        _u32.GetWindowTextW(hwnd, buf, length + 1)
    title = buf.value

    # Messenger apps put only the chat/contact name in the window title with no app suffix.
    # Look up the process exe and inject " – AppName" so the display layer can identify it.
    if " – " not in title and " - " not in title and " — " not in title:
        try:
            pid = ctypes.wintypes.DWORD()
            _u32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            h = _k32.OpenProcess(0x1000, False, pid.value)  # PROCESS_QUERY_LIMITED_INFORMATION
            if h:
                ebuf = ctypes.create_unicode_buffer(260)
                sz   = ctypes.wintypes.DWORD(260)
                _k32.QueryFullProcessImageNameW(h, 0, ebuf, ctypes.byref(sz))
                _k32.CloseHandle(h)
                exe = ebuf.value.rsplit("\\", 1)[-1].lower() if ebuf.value else ""
                app = _EXE_APP_NAMES.get(exe, "")
                if app and title.lower() != app.lower():
                    title = f"{title} – {app}" if title else app
        except Exception:
            pass

    return title


def _idle_seconds():
    lii = _LASTINPUTINFO()
    lii.cbSize = ctypes.sizeof(lii)
    _u32.GetLastInputInfo(ctypes.byref(lii))
    millis = _k32.GetTickCount() - lii.dwTime
    return millis / 1000.0


class ActivityTracker:
    POLL_SECONDS = 60

    def __init__(self, config, conn, on_alert, on_idle=None):
        self.config   = config
        self.conn     = conn
        self.on_alert = on_alert
        self.on_idle  = on_idle  # callable(idle_minutes, idle_start_str)

        rules = config.get("activity_rules", {})
        self._on_plan  = [k.lower() for k in rules.get("on_plan",  [])]
        self._off_plan = [k.lower() for k in rules.get("off_plan", [])]

        wh = config.get("working_hours", {})
        self._work_start  = self._t(wh.get("start", "08:00"))
        self._work_end    = self._t(wh.get("end",   "20:00"))
        self._diary_start = dtime(6, 0)
        self._diary_end   = dtime(20, 0)

        self._grace_min      = config.get("reminder_grace_minutes",    15)
        self._repeat_min     = config.get("reminder_interval_minutes",  5)
        self._idle_threshold = config.get("idle_threshold_minutes",    10)

        # Focus alert state
        self._current_class  = "neutral"
        self._current_window = ""
        self._off_since      = None
        self._last_alert     = None

        # Diary session state
        self._session_start = None
        self._session_app   = None
        self._session_class = None
        self._idle_notified = False
        self._idle_since    = None
        self._last_poll_at  = None   # wall-clock time of previous poll (sleep detection)

        self._running = False
        self._ensure_tables()

    @staticmethod
    def _t(s):
        h, m = (int(x) for x in s.split(":"))
        return dtime(h, m)

    def _in_working_hours(self):
        return self._work_start <= datetime.now().time() <= self._work_end

    def _in_diary_hours(self):
        return self._diary_start <= datetime.now().time() <= self._diary_end

    def classify(self, title):
        t = title.lower()
        for kw in self._on_plan:
            if kw in t:
                return "on_plan"
        for kw in self._off_plan:
            if kw in t:
                return "off_plan"
        return "neutral"

    # ── database ──────────────────────────────────────────────────────────────

    def _ensure_tables(self):
        self.conn.execute(
            "CREATE TABLE IF NOT EXISTS activity_log ("
            "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
            "  logged_at TEXT NOT NULL,"
            "  window    TEXT NOT NULL,"
            "  class     TEXT NOT NULL"
            ")"
        )
        self.conn.execute(
            "CREATE TABLE IF NOT EXISTS time_diary ("
            "  id           INTEGER PRIMARY KEY AUTOINCREMENT,"
            "  date         TEXT NOT NULL,"
            "  start_time   TEXT NOT NULL,"
            "  end_time     TEXT NOT NULL,"
            "  duration_min INTEGER NOT NULL,"
            "  category     TEXT NOT NULL,"
            "  window       TEXT NOT NULL,"
            "  description  TEXT"
            ")"
        )
        self.conn.commit()

    def _log(self, window, cls):
        now = datetime.now().isoformat(sep=" ", timespec="seconds")
        self.conn.execute(
            "INSERT INTO activity_log (logged_at, window, class) VALUES (?, ?, ?)",
            (now, window[:240], cls),
        )
        self.conn.commit()

    def _log_diary_session(self, start, end, category, window, description=None):
        duration = max(1, int((end - start).total_seconds() / 60))
        self.conn.execute(
            "INSERT INTO time_diary "
            "(date, start_time, end_time, duration_min, category, window, description) "
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            (
                start.date().isoformat(),
                start.strftime("%H:%M"),
                end.strftime("%H:%M"),
                duration,
                category,
                window[:240],
                description,
            )
        )
        self.conn.commit()

    def log_idle_answer(self, idle_start_str, idle_minutes, description):
        """Called from main thread after user submits idle dialog."""
        try:
            start = datetime.strptime(idle_start_str, "%Y-%m-%d %H:%M:%S")
        except Exception:
            start = datetime.now() - timedelta(minutes=idle_minutes)
        end = start + timedelta(minutes=idle_minutes)
        self._log_diary_session(start, end, "idle", "idle", description)

    # ── focus alert logic ─────────────────────────────────────────────────────

    def _check_alert(self, cls):
        now = datetime.now()
        if cls != "off_plan" or not self._in_working_hours():
            self._off_since  = None
            self._last_alert = None
            return
        if self._off_since is None:
            self._off_since = now
            return
        off_min = (now - self._off_since).total_seconds() / 60
        if off_min < self._grace_min:
            return
        if self._last_alert is None:
            self._last_alert = now
            self.on_alert(
                "Focus check",
                f"You've been off-plan for {int(off_min)} min. Get back to the Netherlands plan!",
            )
        elif (now - self._last_alert).total_seconds() / 60 >= self._repeat_min:
            self._last_alert = now
            self.on_alert(
                "Still off-plan",
                f"{int(off_min)} min off-plan. Return to your work, the user.",
            )

    # ── poll loop ─────────────────────────────────────────────────────────────

    # Telegram prefixes titles with U+200E Left-to-Right Mark
    _LTR_MARKS = ('‎', '‏')

    @staticmethod
    def _normalise_title(title: str) -> str:
        """Normalise so unread-count changes don't split diary sessions."""
        if not title:
            return title
        # Telegram: ‎CHAT_NAME – (N)  →  CHAT_NAME
        if title.startswith(ActivityTracker._LTR_MARKS):
            t = title.lstrip('‎‏').strip()
            for sep in (" – (", " — (", " - ("):
                idx = t.rfind(sep)
                if idx != -1:
                    candidate = t[idx + len(sep):]
                    if candidate.rstrip().rstrip(")").isdigit() and candidate.rstrip().endswith(")"):
                        return t[:idx].strip()
            return t
        # Leading badge "(N) ..."
        t = title.strip()
        if t.startswith("(") and ")" in t:
            inner = t[1:t.index(")")]
            after = t[t.index(")") + 1:].strip()
            if inner.isdigit():
                for sep in ("– ", "— ", "- "):
                    if after.startswith(sep):
                        after = after[len(sep):].strip()
                        break
                return after
        return t

    def _poll(self):
        while self._running:
            now    = datetime.now()
            title  = _active_window_title()
            title  = self._normalise_title(title) or title
            idle_s = _idle_seconds()
            cls    = self.classify(title)

            # Sleep detection: if wall-clock gap >> POLL_SECONDS the PC was sleeping
            if self._last_poll_at is not None:
                gap_s = (now - self._last_poll_at).total_seconds()
                sleep_s = gap_s - self.POLL_SECONDS
                if sleep_s >= self._idle_threshold * 60 and not self._idle_notified:
                    # Close current active session up to when sleep started
                    sleep_start = self._last_poll_at
                    if self._session_start and self._session_app:
                        self._log_diary_session(
                            self._session_start, sleep_start,
                            self._session_class, self._session_app,
                        )
                        self._session_start = None
                        self._session_app   = None
                        self._session_class = None
                    self._idle_since    = sleep_start
                    self._idle_notified = True
                    if self.on_idle:
                        self.on_idle(
                            int(sleep_s / 60),
                            sleep_start.strftime("%Y-%m-%d %H:%M:%S"),
                        )
            self._last_poll_at = now

            # Focus alert tracking (working hours)
            self._current_window = title
            self._current_class  = cls
            self._log(title, cls)
            self._check_alert(cls)

            # Diary session tracking (06:00–20:00)
            if self._in_diary_hours():
                if idle_s >= self._idle_threshold * 60:
                    if not self._idle_notified:
                        # Close current active session before going idle
                        if self._session_start and self._session_app:
                            self._log_diary_session(
                                self._session_start, now,
                                self._session_class, self._session_app,
                            )
                            self._session_start = None
                            self._session_app   = None
                            self._session_class = None
                        idle_start = now - timedelta(seconds=idle_s)
                        self._idle_since    = idle_start
                        self._idle_notified = True
                        if self.on_idle:
                            self.on_idle(
                                int(idle_s / 60),
                                idle_start.strftime("%Y-%m-%d %H:%M:%S"),
                            )
                else:
                    # Active — idle just ended or never started
                    if self._idle_notified:
                        self._idle_notified = False
                        self._idle_since    = None
                        self._session_start = now
                        self._session_app   = title
                        self._session_class = cls
                    elif self._session_start is None:
                        self._session_start = now
                        self._session_app   = title
                        self._session_class = cls
                    elif title != self._session_app:
                        self._log_diary_session(
                            self._session_start, now,
                            self._session_class, self._session_app,
                        )
                        self._session_start = now
                        self._session_app   = title
                        self._session_class = cls

            time.sleep(self.POLL_SECONDS)

    def start(self):
        self._running = True
        threading.Thread(target=self._poll, daemon=True).start()

    def stop(self):
        self._running = False

    @property
    def status(self):
        return self._current_class, self._current_window

    @property
    def off_plan_minutes(self):
        if self._off_since is None or self._current_class != "off_plan":
            return 0
        return int((datetime.now() - self._off_since).total_seconds() / 60)
