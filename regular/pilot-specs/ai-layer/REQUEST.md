# Feature request: AI layer (no more manual copy-paste to claude.ai)

User (2026-10-13): "let's add the AI layer into Planillium, to eliminate the manual copy."

**Settled decisions (user, 2026-10-13):**
- Connection: the Anthropic API with the user's own API key (not Claude Code, not a subscription).
- Scope of this first version: "Replace remaining tasks" and "Add plan" — the two places that today make the
  user copy a prompt to claude.ai and paste the reply back.
- The manual copy-paste screen stays as the fallback: no key, no internet or an API error brings it back
  with a clear reason; nothing breaks.
- Key handling: entered once in Settings (new "AI" section), masked, stored encrypted for the current Windows
  user. Never in config.json, the database, plan files, logs, the git repo or the installer. Can be removed.
- Model name is a Settings field with a sensible default (the current Sonnet), so it can be changed without a release.
- Only the prompt text for that one action is sent (plan name, task content, the user's instruction). No diary,
  scores, earnings, tags or config. First use shows a one-time notice saying exactly this; the user must accept.
- The reply goes through the same validation and the same preview/confirm as a pasted reply. Nothing is changed
  by the AI call itself; a failed or cancelled call changes nothing.
- The call shows progress, can be cancelled, and times out (about 2 minutes) with a clear message.
