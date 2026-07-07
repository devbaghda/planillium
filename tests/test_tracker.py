"""ActivityTracker pure logic — classification, badge stripping, paid time.
The tracker is constructed but never start()ed: no poll thread, no real DB."""
from datetime import datetime, time as dtime, timedelta

import pytest

from tracker.activity import ActivityTracker, _strip_unread_badge, _is_badge_number


@pytest.fixture
def tracker(tmp_path):
    cfg = {
        "activity_rules": {"on_plan": ["Power BI", "Excel"],
                           "off_plan": ["YouTube", "Steam"]},
        "idle_activity_rules": {"on_plan": ["course"], "off_plan": ["nap"],
                                "neutral": ["lunch"]},
        "working_hours": {"start": "08:00", "end": "20:00"},
    }
    return ActivityTracker(cfg, str(tmp_path / "t.db"), on_alert=lambda *a: None)


class TestBadgeStrip:
    @pytest.mark.parametrize("raw,clean", [
        ("(3) Chat", "Chat"),
        ("Chat (3)", "Chat"),
        ("Chat (1,027)", "Chat"),
        ("Chat (3) (2)", "Chat"),
        ("Plain title", "Plain title"),
        ("Results (draft)", "Results (draft)"),  # words in parens are content
    ])
    def test_cases(self, raw, clean):
        assert _strip_unread_badge(raw) == clean

    def test_badge_number_detector(self):
        assert _is_badge_number("1,027") and _is_badge_number("3")
        assert not _is_badge_number("draft") and not _is_badge_number("")


class TestClassify:
    def test_on_plan_wins_over_off_plan(self, tracker):
        # "Power BI tutorial - YouTube" matches both lists; on_plan is checked first
        assert tracker.classify("Power BI tutorial - YouTube") == "on_plan"

    def test_case_insensitive(self, tracker):
        assert tracker.classify("watching YOUTUBE now") == "off_plan"

    def test_unknown_is_neutral(self, tracker):
        assert tracker.classify("Some Unknown App") == "neutral"

    def test_idle_text_falls_back_to_idle(self, tracker):
        assert tracker.classify_idle_text("went for a walk") == "idle"
        assert tracker.classify_idle_text("online course session") == "on_plan"
        assert tracker.classify_idle_text("") == "idle"


class TestPaidWindow:
    def test_off_plan_relabelled_while_paid(self, tracker):
        tracker.set_paid_until(datetime.now() + timedelta(minutes=30))
        assert tracker._effective_class("off_plan") == "paid"
        assert tracker._effective_class("on_plan") == "on_plan"  # never affected

    def test_expired_window_stops_paying(self, tracker):
        tracker.set_paid_until(datetime.now() - timedelta(minutes=1))
        assert tracker._effective_class("off_plan") == "off_plan"


def test_working_hours_parse(tracker):
    assert tracker._work_start == dtime(8, 0)
    assert tracker._work_end == dtime(20, 0)
