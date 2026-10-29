# Request -- G7 scratch-run safety (user, 2026-10-27)

Test copies of the app (Debug build started with a scratch MENTOR_ROOT, as QA and the user do) can currently reach
the user's real AI key and real TickTick credentials/tokens, and so could call the real services.
Required: when the app runs against a scratch data folder (MENTOR_ROOT set), it must never read or use the real AI
key, the real TickTick credentials/tokens, or contact the real AI or TickTick services. The PLANILLIUM_AI_BASE_URL
override (local stub server) keeps working. A normal run (no MENTOR_ROOT) behaves exactly as today.
Not to change: how credentials are stored or used in a normal run; any feature behaviour.
