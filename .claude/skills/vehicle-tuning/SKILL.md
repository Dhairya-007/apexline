---
name: vehicle-tuning
description: Diagnose and fix car handling in Apexline - understeer, oversteer, snap spins, rolling over, laggy or twitchy steering, braking problems, AI driving badly. Use whenever someone says the car or the AI "feels" wrong, or asks to make it grippier, looser, faster to react, or more stable. Turns a subjective complaint into a specific parameter change.
---

# Vehicle tuning

Handling complaints are a **diagnosis** task, not a coding task. The controller
is almost never the problem; a parameter is.

## Do not start by reading the controller

Start by narrowing the symptom. Ask, or infer from what was said:

1. **Which symptom** — understeer, oversteer, roll, vague steering, braking?
2. **At what speed** — this is the discriminator. The same complaint above
   180 km/h and below 60 km/h has different causes:
   - High speed only → downforce, or steering falloff
   - Low speed only → friction curves, anti-roll, torque curve
   - All speeds → friction stiffness, centre of mass
3. **Which corner** — a single-corner problem is usually a per-node
   `halfWidth`/`speedLimit` fix on `WaypointPath`, not a global change.
4. **Which input method** — tilt, on-screen wheel, gamepad? Twitchy on tilt and
   fine on a gamepad is `PlayerDriver.steerSmoothing` or `tiltDeadzoneDegrees`,
   not a physics problem at all.

## Then use the table

`docs/TUNING.md` maps every symptom above to its most likely parameter, and
holds the shipped reference values for reverting. Read it before proposing a
change.

## Rules for the change itself

- **One parameter at a time.** Two at once teaches nothing.
- **Move ±20%, not ±2%.** If 20% changes nothing, it is the wrong parameter.
- **Never rewrite `CarController` to fix a feel complaint.** If the structure
  genuinely needs to change, say so and stop — that is a separate conversation.
- **Keep the assists.** The counter-steer assist and speed-sensitive steering
  limit are the design, not a crutch; a touchscreen player has no force feedback
  and about two bits of steering precision. "Make it more realistic" by removing
  them makes the game worse. Say so.
- **Faster is not better.** A quicker but less controllable car is a regression.

## What you cannot do

You cannot drive the car. So never say a change "feels better", "fixes the
handling" or "should feel right now". State what you changed, which symptom it
targets, what to drive to check it, and what the opposite failure would look
like if you overshot.

## Reporting

Give the user:

- The parameter, old value → new value, and the one-line reason
- Which symptom it targets
- What to test: a specific corner and speed
- The overshoot signature — e.g. "if the rear now feels lazy, you went too far"
- A reminder to record the before/after lap time for the commit (the PR template
  asks for it)
