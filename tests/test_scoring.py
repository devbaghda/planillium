"""Score economy v2 — the money path. A wrong number here silently corrupts
the balance the user spends, so these tests pin the formula and both guards."""
import sqlite3
import types
from datetime import date, timedelta

import main


def _stub(config=None):
    return types.SimpleNamespace(config=config or {})


class TestDayScore:
    def test_disaster_day_hits_the_floor(self):
        # 0/5 tasks, 10h off-plan: raw = -25 - 20 = -45 -> floored
        assert main.MentorApp._day_score(_stub(), 0, 5, 0, 600) == main.DAILY_SCORE_FLOOR

    def test_normal_day_is_not_floored(self):
        # 5/5 tasks + 5h on-plan = 50 + 15
        assert main.MentorApp._day_score(_stub(), 5, 5, 300, 0) == 65

    def test_streak_bonus_applies(self):
        base = main.MentorApp._day_score(_stub(), 3, 3, 0, 0)
        assert main.MentorApp._day_score(_stub(), 3, 3, 0, 0, streak=2) == base + 10

    def test_rates_come_from_config(self):
        cfg = {"scoring": {"task_completed": 7}}
        assert main.MentorApp._day_score(_stub(cfg), 1, 1, 0, 0) == 7

    def test_floor_matches_winui_constant(self):
        assert main.DAILY_SCORE_FLOOR == -10


def _plan(start: date, tasks: dict[int, list[str]]):
    return {
        "id": "p1",
        "name": "Test plan",
        "start_date": start.isoformat(),
        "phases": [{
            "tasks": [{"day": d, "task": t} for d, ts in tasks.items() for t in ts]
        }],
    }


def _accrual_app(data_store, plan, completions=None):
    """Wire the real accrual/credit methods onto the data_store stub."""
    app = data_store
    app.config = {}
    app.plans = [plan]
    app.completions = completions or {}
    app._tasks_by_day_for = types.MethodType(main.MentorApp._tasks_by_day_for, app)
    app._load_overrides = types.MethodType(main.MentorApp._load_overrides, app)
    app._score_add_ledger = types.MethodType(main.MentorApp._score_add_ledger, app)
    app._credit_overdue_accrual_if_missing = types.MethodType(
        main.MentorApp._credit_overdue_accrual_if_missing, app)
    return app


class TestOverdueAccrual:
    def test_task_within_cap_bleeds_points(self, data_store):
        start = date.today() - timedelta(days=10)
        app = _accrual_app(data_store, _plan(start, {9: ["late task"]}))
        d = start + timedelta(days=10)  # day 11; task 2 days overdue
        assert app._credit_overdue_accrual_if_missing(d) == -5

    def test_task_past_cap_is_stale_and_free(self, data_store):
        start = date.today() - timedelta(days=10)
        app = _accrual_app(data_store, _plan(start, {1: ["ancient task"]}))
        d = start + timedelta(days=10)  # day 11; task 10 days overdue > cap 3
        assert app._credit_overdue_accrual_if_missing(d) == 0

    def test_completed_task_never_bleeds(self, data_store):
        start = date.today() - timedelta(days=10)
        app = _accrual_app(data_store, _plan(start, {9: ["done task"]}),
                           completions={("p1", 9, "done task"): True})
        assert app._credit_overdue_accrual_if_missing(start + timedelta(days=10)) == 0

    def test_once_per_date_guard(self, data_store):
        start = date.today() - timedelta(days=10)
        app = _accrual_app(data_store, _plan(start, {9: ["late task"]}))
        d = start + timedelta(days=10)
        assert app._credit_overdue_accrual_if_missing(d) == -5
        assert app._credit_overdue_accrual_if_missing(d) is None  # already credited

    def test_race_hits_index_not_double_credit(self, data_store):
        """Simulate the other app winning the check-then-act race: the row
        appears after our SELECT guard would have run — the UNIQUE index must
        turn the duplicate into a None, not a second row."""
        start = date.today() - timedelta(days=10)
        app = _accrual_app(data_store, _plan(start, {9: ["late task"]}))
        d = start + timedelta(days=10)
        app.conn.execute(
            "INSERT INTO score_ledger (ts, date, delta, reason, detail) "
            "VALUES ('x', ?, -5, 'overdue_accrual', 'other app')", (d.isoformat(),))
        app.conn.commit()
        assert app._credit_overdue_accrual_if_missing(d) is None
        n = app.conn.execute(
            "SELECT COUNT(*) FROM score_ledger WHERE reason='overdue_accrual' AND date=?",
            (d.isoformat(),)).fetchone()[0]
        assert n == 1
