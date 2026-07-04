import ctypes
import ctypes.wintypes
import logging
import sqlite3
import threading
import time
from datetime import datetime, time as dtime, timedelta

_log = logging.getLogger(__name__)


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


_LTR_STRIP = str.maketrans("", "", "‎‏")
_BADGE_SEP_STRIP = str.maketrans("", "", ",.   '")


def _is_badge_number(s: str) -> bool:
    s = s.translate(_BADGE_SEP_STRIP)
    return s.isdigit() and s != ""


def _strip_unread_badge(title: str) -> str:
    """Remove leading/trailing unread-count badges like '(3) Chat', 'Chat (3)',
    'Chat (1,027)', or 'Chat (3) (2)' — loops until no more badges are found,
    since some window titles carry more than one trailing/leading paren group."""
    t = title.strip()
    changed = True
    while changed:
        changed = False
        if t.endswith(")"):
            inner_start = t.rfind("(")
            if inner_start != -1 and _is_badge_number(t[inner_start + 1:-1]):
                t = t[:inner_start].rstrip(" –—-").strip()
                changed = True
                continue
        if t.startswith("(") and ")" in t:
            inner = t[1:t.index(")")]
            if _is_badge_number(inner):
                t = t[t.index(")") + 1:].lstrip(" –—-").strip()
                changed = True
    return t


# Sticky per-PID cache: OpenProcess/QueryFullProcessImageNameW occasionally fail
# transiently for a foreground window even though the process is a known messenger
# (observed with Telegram) — without this, a single failed poll drops the "– AppName"
# suffix and the session gets misfiled as a standalone app instead of nested under it.
_pid_app_cache = {}


def _active_window_title():
    hwnd = _u32.GetForegroundWindow()
    length = _u32.GetWindowTextLengthW(hwnd)
    buf = ctypes.create_unicode_buffer(max(length, 0) + 1)
    if length:
        _u32.GetWindowTextW(hwnd, buf, length + 1)
    title = buf.value

    # Always look up exe for known messenger apps — they use chat name as window title.
    try:
        pid = ctypes.wintypes.DWORD()
        _u32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        pid_val = pid.value
        h = _k32.OpenProcess(0x1000, False, pid_val)
        exe = ""
        if h:
            ebuf = ctypes.create_unicode_buffer(260)
            sz   = ctypes.wintypes.DWORD(260)
            _k32.QueryFullProcessImageNameW(h, 0, ebuf, ctypes.byref(sz))
            _k32.CloseHandle(h)
            exe = ebuf.value.rsplit("\\", 1)[-1].lower() if ebuf.value else ""
        app = _EXE_APP_NAMES.get(exe, "")
        if app:
            _pid_app_cache[pid_val] = app
        elif pid_val in _pid_app_cache:
            app = _pid_app_cache[pid_val]
        if app:
            # Strip Unicode direction marks then unread-count badge before injecting app name
            clean = title.translate(_LTR_STRIP).strip()
            clean = _strip_unread_badge(clean)
            if clean.lower() != app.lower():
                title = f"{clean} – {app}" if clean else app
            else:
                title = app
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

    def __init__(self, config, db_path, on_alert, on_idle=None):
        self.config   = config
        self._db_path = db_path
        self.conn     = None  # opened inside poll thread; each thread owns its connection
        self.on_alert = on_alert
        self.on_idle  = on_idle  # callable(idle_minutes, idle_start_str)

        rules = config.get("activity_rules", {})
        self._on_plan  = [k.lower() for k in rules.get("on_plan",  [])]
        self._off_plan = [k.lower() for k in rules.get("off_plan", [])]

        idle_rules = config.get("idle_activity_rules", {})
        self._idle_on_plan  = [k.lower() for k in idle_rules.get("on_plan",  [])]
        self._idle_off_plan = [k.lower() for k in idle_rules.get("off_plan", [])]
        self._idle_neutral  = [k.lower() for k in idle_rules.get("neutral",  [])]

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

        # Paid entertainment window (set by the main thread via set_paid_until).
        # Plain attribute, no lock — same lock-free cross-thread pattern already
        # used for _off_since/_current_class above; reads/writes of a single
        # reference are atomic enough under the GIL for this use.
        self._paid_until = None

        # Diary session state
        self._session_start = None
        self._session_app   = None
        self._session_class = None
        self._idle_notified = False
        self._idle_since    = None
        self._last_poll_at  = None   # wall-clock time of previous poll (sleep detection)

        self._running = False
        # Create tables now (main thread, short-lived connection)
        with sqlite3.connect(db_path) as _setup:
            self._ensure_tables(_setup)

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

    def classify_idle_text(self, description):
        """Match a typed idle-answer description against the idle activity
        library (substring match, same mechanism as classify() above). Falls
        back to 'idle' (unclassified) when nothing matches."""
        if not description:
            return "idle"
        t = description.lower()
        for kw in self._idle_on_plan:
            if kw in t:
                return "on_plan"
        for kw in self._idle_off_plan:
            if kw in t:
                return "off_plan"
        for kw in self._idle_neutral:
            if kw in t:
                return "neutral"
        return "idle"

    def set_paid_until(self, dt):
        """Called from the main thread when a score-bought entertainment window
        opens (dt) or is cancelled/expires (None)."""
        self._paid_until = dt

    def _effective_class(self, cls):
        """Relabels off_plan as 'paid' while a bought entertainment window is
        open. On-plan/neutral time is never affected — paid time only ever
        covers off-plan activity."""
        if cls == "off_plan" and self._paid_until and datetime.now() < self._paid_until:
            return "paid"
        return cls

    # ── database ──────────────────────────────────────────────────────────────

    def _ensure_tables(self, conn):
        conn.execute(
            "CREATE TABLE IF NOT EXISTS activity_log ("
            "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
            "  logged_at TEXT NOT NULL,"
            "  window    TEXT NOT NULL,"
            "  class     TEXT NOT NULL"
            ")"
        )
        conn.execute(
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
        conn.commit()

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
        """Called from main thread — opens its own connection to avoid cross-thread use."""
        try:
            start = datetime.strptime(idle_start_str, "%Y-%m-%d %H:%M:%S")
        except Exception:
            start = datetime.now() - timedelta(minutes=idle_minutes)
        end = start + timedelta(minutes=idle_minutes)
        duration = max(1, int((end - start).total_seconds() / 60))
        category = self.classify_idle_text(description)
        with sqlite3.connect(self._db_path) as conn:
            conn.execute(
                "INSERT INTO time_diary "
                "(date, start_time, end_time, duration_min, category, window, description) "
                "VALUES (?, ?, ?, ?, ?, ?, ?)",
                (
                    start.date().isoformat(),
                    start.strftime("%H:%M"),
                    end.strftime("%H:%M"),
                    duration,
                    category,
                    "idle",
                    description,
                )
            )

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

    def _poll(self):
        self.conn = sqlite3.connect(self._db_path)
        try:
            while self._running:
                try:
                    self._poll_once()
                except Exception:
                    _log.exception("ActivityTracker poll error")
                time.sleep(self.POLL_SECONDS)
        finally:
            self.conn.close()
            self.conn = None

    def _diary_end_today(self):
        return datetime.combine(datetime.now().date(), self._diary_end)

    def _poll_once(self):
        now    = datetime.now()
        title  = _active_window_title()
        idle_s = _idle_seconds()
        cls    = self._effective_class(self.classify(title))

        # Sleep detection: if wall-clock gap >> POLL_SECONDS the PC was sleeping
        if self._last_poll_at is not None:
            gap_s   = (now - self._last_poll_at).total_seconds()
            sleep_s = gap_s - self.POLL_SECONDS
            if sleep_s >= self._idle_threshold * 60 and not self._idle_notified:
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
                # on_idle fired when user actually returns (see below)
        self._last_poll_at = now

        # Focus alert tracking (working hours only)
        self._current_window = title
        self._current_class  = cls
        self._log(title, cls)
        self._check_alert(cls)

        # Diary session tracking (06:00–20:00)
        if self._idle_notified and idle_s < self._idle_threshold * 60:
            # User has returned from idle/sleep — fire dialog with actual duration
            diary_end = self._diary_end_today()
            idle_end  = min(now, diary_end)
            idle_start = self._idle_since or (now - timedelta(seconds=idle_s))
            idle_start = max(idle_start,
                             datetime.combine(now.date(), self._diary_start))

            if idle_start < idle_end:
                actual_min = max(1, int((idle_end - idle_start).total_seconds() / 60))
                if self._in_diary_hours() and self.on_idle:
                    # Still within tracking hours → ask user
                    self.on_idle(actual_min, idle_start.strftime("%Y-%m-%d %H:%M:%S"))
                elif idle_start < diary_end:
                    # Returned after 20:00 — auto-log the diary-hours portion silently
                    self._log_diary_session(idle_start, idle_end, "idle", "idle")

            self._idle_notified = False
            self._idle_since    = None
            if self._in_diary_hours():
                self._session_start = now
                self._session_app   = title
                self._session_class = cls

        elif self._in_diary_hours():
            if idle_s >= self._idle_threshold * 60:
                if not self._idle_notified:
                    # Close active session, mark idle start
                    if self._session_start and self._session_app:
                        self._log_diary_session(
                            self._session_start, now,
                            self._session_class, self._session_app,
                        )
                        self._session_start = None
                        self._session_app   = None
                        self._session_class = None
                    self._idle_since    = now - timedelta(seconds=idle_s)
                    self._idle_notified = True
                    # on_idle will fire when user returns
            else:
                # Active
                if self._session_start is None:
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

        else:
            # Outside diary hours — close any open session capped at diary_end
            diary_end = self._diary_end_today()
            if self._session_start and self._session_app:
                end = min(now, diary_end)
                if end > self._session_start:
                    self._log_diary_session(
                        self._session_start, end,
                        self._session_class, self._session_app,
                    )
                self._session_start = None
                self._session_app   = None
                self._session_class = None

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
