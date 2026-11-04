# Acceptance — AI layer (run only against a scratch MENTOR_ROOT; AI calls go to a local stub server, never the
# real API; the stub address is set by environment variable PLANILLIUM_AI_BASE_URL for testing)

1. Settings has an "AI" section: masked API key field, model field (default a current Sonnet id), Save, Remove key,
   Test connection.
2. After Save, the key is not found in plain text in config.json, progress.db, plans/, logs or the repo tree.
3. Remove key deletes the stored secret; the app behaves as "no key" afterwards.
4. Test connection reports success against a stub that returns 200, and a readable error for 401, 429, 500 and timeout.
5. Replace remaining, with key: an "Ask Claude" action sends the prompt and, on success, fills the reply and moves
   to the same preview as a pasted reply.
6. Replace remaining, no key: the manual copy/paste screen shows, with a hint pointing to Settings -> AI.
7. Add plan, with key and no key: same two behaviours as lines 5 and 6.
8. Stub returns 401 / 500 / garbage / non-JSON text: dialog shows a plain-language reason and the manual screen;
   plan file byte-identical.
9. Cancel during a slow call: call stops, nothing changes, dialog usable.
10. A call exceeding the timeout ends with a clear message; nothing changes.
11. The first send shows the one-time notice stating what is sent; Decline sends nothing; Accept is remembered
    (not asked again).
12. The request body the stub receives contains the prompt and nothing else from the app's data: no diary rows,
    scores, earnings, tags, config values or the API key beyond the auth header.
13. The key is never written to the log, even on errors.
14. Reply from the stub with a bad shape (missing phases) is rejected by the same validation as a manual paste.
15. Confirm at the preview applies the change exactly as a pasted reply would (identical plan file result for
    identical reply text).
16. All acceptance lines of replace-remaining-tasks and replace-remaining-v2 still pass.
17. Build clean (0 warnings with /warnaserror); existing tests pass; new tests cover the AI client against the stub.
