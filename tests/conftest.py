"""Shared fixtures. Hard rule: tests NEVER touch the real data/ folder —
every fixture that needs a database gets a fresh one under tmp_path."""
import sys
import types
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT))

import main  # noqa: E402  (needs the path insert above)


@pytest.fixture
def data_store(tmp_path, monkeypatch):
    """A stub app with a real ensure_data_store() run against a scratch DB.
    Returns the stub; its .conn is the open connection."""
    db = tmp_path / "data" / "progress.db"
    monkeypatch.setattr(main, "DATA_DIR", str(db.parent))
    monkeypatch.setattr(main, "DB_PATH", str(db))
    app = types.SimpleNamespace()
    main.MentorApp.ensure_data_store(app)
    yield app
    app.conn.close()
