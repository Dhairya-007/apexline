# Architecture

## The one idea that shapes everything

**Players and AI share the same vehicle physics.** `CarController` exposes a
single input surface — `CarInput` — and both `PlayerDriver` and `AIDriver` write
to it. Neither ever touches `Rigidbody` or `WheelCollider`.

```
PlayerDriver ─┐
              ├──> CarInput ──> CarController ──> Rigidbody + 4 WheelColliders
AIDriver ─────┘                      │
                                     └──> SpeedKph, EngineRpm01, Gear,
                                          MaxLateralSlip, IsGrounded
                                          (read by HUD, audio, VFX)
```

Why it matters: the moment AI gets its own movement code, opponents race on
different rules from the player. That is unfair in a way players detect
immediately and tuning cannot fix, and it doubles the amount of driving code
that must stay in sync. It is also the thing most easily broken by a
well-meaning change, which is why it is rule 1 in `CLAUDE.md`.

## Assemblies

One-directional, enforced by `.asmdef` files rather than discipline:

```
Apexline.Vehicle   (no project dependencies)
      ↑
Apexline.AI        (-> Vehicle)
      ↑
Apexline.Race      (-> Vehicle, AI)
```

`Apexline.Tests.EditMode` references all three, is Editor-only, and is
constrained to `UNITY_INCLUDE_TESTS` so it never enters a player build.

A reference pointing the other way means the layering is wrong. Raise it as an
issue; do not add the reference.

## Components

### Vehicle

**`CarController`** — semi-arcade handling on `WheelCollider`. Everything
physical happens in `FixedUpdate`; only wheel-mesh posing happens in `Update`,
so visuals run at display rate while physics runs at 50 Hz. The stability
assists (anti-roll bars, speed-squared downforce, slip-triggered traction
control, counter-steer) do the work that a simulation tyre model would, at a
fraction of the cost and with far more control over feel.

**`CarInput`** — a plain struct. Throttle, brake, steer, handbrake. The entire
contract between "something decided to drive" and "physics happened".

**`PlayerDriver`** — human input. Tilt or on-screen wheel for touch, gamepad and
keyboard for desktop iteration, auto-throttle on by default. Compiles with or
without the Input System package via `ENABLE_INPUT_SYSTEM`, so a fresh clone
builds before anyone opens Package Manager.

### AI

**`WaypointPath`** — the racing line, and the most load-bearing type in the
project. Four systems read it: AI steering, AI braking points, race position
sorting, and off-track respawn. `Progress()` returns fractional node units,
which is what reduces live position sorting to one numeric comparison.

**`AIDriver`** — pure pursuit. Steers at a lookahead point scaled by speed, and
sets target speed from *upcoming* path curvature so it brakes before a corner
rather than reacting inside one. Per-instance pace jitter of ±6% stops the field
forming a train. Avoidance nudges a lateral offset clamped to track width; a
stuck timer reverses out and then falls back to a path respawn.

### Race

**`RaceManager`** — state machine over Grid → Countdown → Racing → Finished.
Standings sort on `lap * nodeCount + progress`, so position stays correct
mid-corner rather than only at checkpoints. Finishing AI have their pace reduced
instead of being frozen, so they do not become track furniture.

**`RaceSector`** — trigger planes. Index 0 is start/finish; a lap only counts
when every other sector has been crossed, which is what stops corner cutting
from registering a valid lap.

## Decisions worth knowing

| Decision | Why | What it costs |
|---|---|---|
| `WheelCollider`, not a custom tyre model | Free, built in, well understood; good enough for arcade | Quirky API, hard to tune blind |
| Arcade with assists, not simulation | Touchscreens have no force feedback and ~2 bits of steering precision | Sim fans will not be the audience |
| Baked lighting only | The single largest mobile performance lever | Tracks cannot change time of day |
| URP, not HDRP | HDRP does not run on phones | Lower graphical ceiling on desktop |
| Fake gearbox | Audio and HUD need an RPM; physics does not need gears | `Gear` has no physical meaning |
| Waypoints, not a navmesh | Racing lines are 1-dimensional; navmesh solves a problem we do not have | Overtaking lines must be authored |
| CC0 assets only | Legal redistributability is the point of the project | No Asset Store convenience |

## Not built yet

UI and HUD, audio, VFX, the track itself, car select, and the credits screen
that CC-BY attribution requires. See [`ROADMAP.md`](ROADMAP.md).
