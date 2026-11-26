---
description: Walk through adding a new drivable car, including licence checks
argument-hint: <car name>
---

Add a new car: **$ARGUMENTS**

Work through these in order and stop at the first thing that needs a decision
from the user.

1. **Model provenance.** Use the `asset-sourcing` skill. CC0 preferred. No Asset
   Store content, no real manufacturer shapes or liveries. Add the
   `ASSET-LICENSES.md` row in the same commit as the model.
2. **Prefab.** `Rigidbody` + four `WheelCollider`s + four wheel meshes.
   `centerOfMassOffset` around `(0, -0.30, 0)`.
3. **`CarController` values.** Start from the `docs/TUNING.md` reference set and
   change only what should differ for this car's character — mass, torque, top
   speed, friction stiffness. Do not invent a whole new parameter set.
4. **Character.** Say in one line what makes this car distinct to drive, and
   which parameters express that. A new car that drives like the old one is
   content, not a car.
5. **Drivers.** `PlayerDriver` for the player; `AIDriver` + `WaypointPath`
   reference for opponents. Never both on one car.
6. **Verify.** Hygiene checks, EditMode tests, then the user drives it and
   records a lap time on the reference track.

Remember you cannot drive it, so do not assess how it handles.
