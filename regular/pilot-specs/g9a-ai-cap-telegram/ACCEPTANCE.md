# Acceptance -- G9a
1. Settings shows the monthly AI cap (default EUR 5) and this month's spend; editing the cap saves and survives restart;
   invalid input (empty, negative, text) is refused with a message.
2. With spend below the cap AI calls proceed as before; at or above the cap they are refused before any call is made,
   with the clear message; the stub server receives no request (scratch run, stub only, never a real key).
3. A new calendar month resets the spend to zero (code evidence plus a unit-level check of the month boundary).
4. Telegram diary rows expose the chat name from the title; titles without a recognisable chat name fall back
   gracefully (no crash, no empty label); non-Telegram rows unchanged.
5. Sibling check: list every place an AI call is made and confirm each goes through the cap.
6. Build clean (/warnaserror); existing tests pass.
