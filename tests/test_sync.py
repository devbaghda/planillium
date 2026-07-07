"""TickTick client: the keyring migration safety (audit #6) and due-date
parsing. No network, no real keyring — everything faked in-process."""
from datetime import datetime, timezone

import ticktick.sync as sync
import main


def _client_with_fake_keyring(monkeypatch, store, set_works=True):
    monkeypatch.setattr(sync, "_kr_get", lambda k: store.get(k, ""))
    monkeypatch.setattr(
        sync, "_kr_set",
        (lambda k, v: store.__setitem__(k, v)) if set_works else (lambda k, v: None))
    cfg = {"ticktick": {"client_id": "cid", "client_secret": "s3cret",
                        "access_token": "tok3n", "refresh_token": ""}}
    return sync.TickTickClient(cfg)


class TestKeyringMigration:
    def test_cleanup_only_after_verified_write(self, monkeypatch):
        client = _client_with_fake_keyring(monkeypatch, {}, set_works=True)
        assert client.needs_config_cleanup is True

    def test_no_cleanup_when_keyring_write_noops(self, monkeypatch):
        """Regression (audit #6): a broken/missing keyring must NOT report the
        config as safe to strip — that would destroy the only credential copy."""
        client = _client_with_fake_keyring(monkeypatch, {}, set_works=False)
        assert client.needs_config_cleanup is False
        assert client.client_secret == "s3cret"  # still held in memory

    def test_no_migration_needed_when_config_clean(self, monkeypatch):
        monkeypatch.setattr(sync, "_kr_get", lambda k: "")
        client = sync.TickTickClient({"ticktick": {"client_id": "cid"}})
        assert client.needs_config_cleanup is False


class TestDueDateParsing:
    def test_utc_offset_is_converted_not_sliced(self):
        # All-day tasks come back as local-midnight-in-UTC; slicing the string
        # would be off by one day in any UTC+ timezone.
        task = {"dueDate": "2026-07-03T22:00:00.000+0000"}
        expected = datetime(2026, 7, 3, 22, 0, tzinfo=timezone.utc).astimezone().date()
        assert main.MentorApp._tt_task_local_date(task) == expected

    def test_second_format_without_millis(self):
        assert main.MentorApp._tt_task_local_date(
            {"dueDate": "2026-07-03T22:00:00+0000"}) is not None

    def test_missing_and_garbage_dates_are_none(self):
        assert main.MentorApp._tt_task_local_date({}) is None
        assert main.MentorApp._tt_task_local_date({"dueDate": "tomorrow-ish"}) is None
