# Acceptance -- G7 (scratch MENTOR_ROOT only; never real data, never a real key)

1. Under a scratch MENTOR_ROOT the app reads no real AI key and no real TickTick credential/token (show where each is
   read and the guard; code evidence is enough, a live read of the real secret store is NOT allowed).
2. Under a scratch MENTOR_ROOT, AI calls go only to PLANILLIUM_AI_BASE_URL (stub) or fail cleanly with a clear message
   when it is unset; TickTick calls are refused cleanly. No crash, no hang.
3. A run without MENTOR_ROOT is unchanged (code evidence: guard keyed only on the scratch condition).
4. Sibling check: every other place that reads a secret or calls an external service is listed and covered or
   explicitly justified.
5. Build clean (/warnaserror); existing tests pass.
