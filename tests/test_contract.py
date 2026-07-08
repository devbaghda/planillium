"""Cross-app contract tests: the Python and WinUI apps share progress.db,
config.json, and a named mutex. These tests fail the moment one side drifts —
the audit found exactly such a drift (v1 vs v2 scoring) once already."""
import ast
import re
from pathlib import Path

import main

REPO = Path(__file__).resolve().parents[1]
SCORE_CS = (REPO / "winui/MentorOverseer.App/Services/ScoreService.cs").read_text(encoding="utf-8")
TRACKER_CS = (REPO / "winui/MentorOverseer.App/Services/ActivityTracker.cs").read_text(encoding="utf-8")
MAIN_SRC = (REPO / "main.py").read_text(encoding="utf-8")


class TestScoringContract:
    def test_daily_floor_matches(self):
        m = re.search(r"DailyFloor\s*=\s*(-?\d+)", SCORE_CS)
        assert m and int(m.group(1)) == main.DAILY_SCORE_FLOOR

    def test_overdue_cap_matches(self):
        m = re.search(r"OverdueAccrualCapDays\s*=\s*(\d+)", SCORE_CS)
        assert m and int(m.group(1)) == main.OVERDUE_ACCRUAL_CAP_DAYS

    def test_both_sides_create_the_same_ledger_index(self):
        for src in (SCORE_CS, MAIN_SRC):
            assert "sl_reason_date" in src
            assert re.search(
                r"WHERE reason IN \('daily_score',\s*'overdue_accrual',\s*'weekly_comeback_bonus'\)",
                src,
            )


class TestCoexistenceContract:
    def test_winui_probes_the_python_mutex_by_its_real_name(self):
        # If the Python mutex is ever renamed, the WinUI pause guard silently
        # stops working — this pins the two names together.
        py = re.search(r'_MUTEX_NAME\s*=\s*"([^"]+)"', MAIN_SRC)
        assert py and f'"{py.group(1)}"' in TRACKER_CS


class TestSourceInvariants:
    def test_no_setlocale_anywhere(self):
        """Persisted dates are fixed-format; a setlocale call would make
        strftime/strptime locale-dependent on this Russian-locale OS."""
        for name in ("main.py", "tracker/activity.py", "ticktick/sync.py"):
            tree = ast.parse((REPO / name).read_text(encoding="utf-8"))
            calls = [n for n in ast.walk(tree)
                     if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute)
                     and n.func.attr == "setlocale"]
            assert calls == [], f"setlocale call found in {name}"

    def test_log_setup_forces_utf8(self):
        # Bounded window instead of [^)]* — the call contains comments with parens.
        assert re.search(r'basicConfig\(.{0,500}?encoding="utf-8"', MAIN_SRC, re.S)

    def test_report_hints_are_escaped(self):
        # Audit #7: hints embed window-title-derived text into the HTML report.
        m = re.search(r"hint_items\s*=\s*\"\"\.join\((.+)\)", MAIN_SRC)
        assert m and "_html.escape(h)" in m.group(1)

    def test_persisted_dates_use_fixed_formats(self):
        # %b/%B/%a are for on-screen text only; anything written to disk or DB
        # must be %Y-%m-%d. Guard the two writers that matter.
        assert 'isoformat()' in MAIN_SRC
        assert re.search(r'strptime\(plan\["start_date"\], "%Y-%m-%d"\)', MAIN_SRC)
