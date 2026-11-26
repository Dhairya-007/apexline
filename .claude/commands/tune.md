---
description: Diagnose a handling complaint and propose one parameter change
argument-hint: <symptom, e.g. "oversteers on the hairpin above 150">
---

Handling complaint: **$ARGUMENTS**

Use the `vehicle-tuning` skill. Specifically:

1. Narrow the symptom — which behaviour, what speed range, which corner, which
   input method. If the speed range is not stated, ask, because it is what
   distinguishes otherwise identical causes.
2. Look the symptom up in `docs/TUNING.md`.
3. Propose **one** parameter change, moving ±20%.
4. Report: parameter, old → new, the symptom it targets, what to drive to check
   it, and what overshooting would feel like.

Do not rewrite `CarController`. Do not remove the driver assists. Do not claim
it feels better — you cannot drive it.
