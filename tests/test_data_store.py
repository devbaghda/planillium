"""ensure_data_store: schema creation, v1 migration, the double-credit net,
and retention — all against scratch databases only."""
import sqlite3
import types
from datetime import date

import pytest

import main


def test_fresh_db_boots_without_tracker_tables(data_store):
    """Regression: the retention pass must not assume activity_log/time_diary
    exist — they belong to the tracker and are missing on first launch."""
    tables = {r[0] for r in data_store.conn.execute(
        "SELECT name FROM sqlite_master WHERE type='table'").fetchall()}
    assert "task_completions" in tables and "score_ledger" in tables


def test_unique_index_blocks_double_credit(data_store):
    c = data_store.conn
    c.execute("INSERT INTO score_ledger (ts, date, delta, reason, detail) "
              "VALUES ('a', '2026-07-06', 5, 'daily_score', NULL)")
    with pytest.raises(sqlite3.IntegrityError):
        c.execute("INSERT INTO score_ledger (ts, date, delta, reason, detail) "
                  "VALUES ('b', '2026-07-06', 7, 'daily_score', NULL)")


def test_unique_index_ignores_other_reasons(data_store):
    c = data_store.conn
    for ts in ("a", "b"):
        c.execute("INSERT INTO score_ledger (ts, date, delta, reason, detail) "
                  "VALUES (?, '2026-07-06', -3, 'entertainment_purchase', NULL)", (ts,))
    n = c.execute("SELECT COUNT(*) FROM score_ledger").fetchone()[0]
    assert n == 2


def test_retention_prunes_old_keeps_recent(data_store, tmp_path, monkeypatch):
    c = data_store.conn
    c.execute("CREATE TABLE activity_log (id INTEGER PRIMARY KEY AUTOINCREMENT,"
              " logged_at TEXT NOT NULL, window TEXT NOT NULL, class TEXT NOT NULL)")
    c.execute("CREATE TABLE time_diary (id INTEGER PRIMARY KEY AUTOINCREMENT,"
              " date TEXT NOT NULL, start_time TEXT NOT NULL, end_time TEXT NOT NULL,"
              " duration_min INTEGER NOT NULL, category TEXT NOT NULL,"
              " window TEXT NOT NULL, description TEXT)")
    c.execute("INSERT INTO activity_log (logged_at, window, class) "
              "VALUES ('2024-01-01 10:00:00', 'ancient', 'neutral')")
    c.execute("INSERT INTO activity_log (logged_at, window, class) "
              "VALUES (datetime('now', 'localtime'), 'recent', 'neutral')")
    c.execute("INSERT INTO time_diary (date, start_time, end_time, duration_min,"
              " category, window) VALUES ('2023-01-01', '10:00', '10:30', 30,"
              " 'neutral', 'ancient')")
    c.commit()
    main.MentorApp.ensure_data_store(data_store)  # idempotent re-run = startup
    rows = [r[0] for r in data_store.conn.execute(
        "SELECT window FROM activity_log").fetchall()]
    assert rows == ["recent"]
    assert data_store.conn.execute(
        "SELECT COUNT(*) FROM time_diary").fetchone()[0] == 0


def test_v1_table_migrates_off_global_unique(tmp_path, monkeypatch):
    """The v1 inline UNIQUE(plan_day, task_text) spanned ALL plans; the store
    must rebuild it so two plans can share a (day, text) pair."""
    db = tmp_path / "data" / "progress.db"
    db.parent.mkdir(parents=True)
    with sqlite3.connect(db) as seed:
        seed.execute("CREATE TABLE task_completions ("
                     " id INTEGER PRIMARY KEY AUTOINCREMENT,"
                     " plan_day INTEGER NOT NULL, task_text TEXT NOT NULL,"
                     " completed INTEGER NOT NULL, completed_at TEXT,"
                     " last_updated TEXT NOT NULL,"
                     " UNIQUE(plan_day, task_text))")
        seed.execute("INSERT INTO task_completions (plan_day, task_text, completed,"
                     " last_updated) VALUES (1, 'shared task', 1, 'x')")
    monkeypatch.setattr(main, "DATA_DIR", str(db.parent))
    monkeypatch.setattr(main, "DB_PATH", str(db))
    app = types.SimpleNamespace()
    main.MentorApp.ensure_data_store(app)
    try:
        # old row survived the rebuild...
        assert app.conn.execute("SELECT COUNT(*) FROM task_completions").fetchone()[0] == 1
        # ...and the same (day, text) under a different plan_id now inserts fine
        app.conn.execute(
            "INSERT INTO task_completions (plan_id, plan_day, task_text, completed,"
            " last_updated) VALUES ('other', 1, 'shared task', 0, 'y')")
    finally:
        app.conn.close()
