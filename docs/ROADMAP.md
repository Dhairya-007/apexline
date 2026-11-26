# Roadmap

## Scope, honestly

Real Racing 3 was built by 60+ people over years. In a week, solo, the
achievable target is a **vertical slice**: one track, two cars, five AI, a full
race loop, good-looking, running at 60 fps on a phone. That is the thing worth
expanding for months afterwards — not a content library.

What is explicitly *not* in the week: multiplayer, career progression,
monetisation, more than one track, car upgrades, damage, weather.

## Done

- [x] Repository scaffolding, MIT licence, asset licence policy
- [x] Git LFS routing, `.editorconfig`, hygiene script
- [x] CI with a fork-safe job split; tag-triggered platform builds
- [x] Issue forms, PR template, contributing guide, code of conduct
- [x] Assembly definitions with one-directional layering
- [x] `CarController` with stability assists
- [x] `CarInput`, `PlayerDriver` (tilt / wheel / gamepad)
- [x] `WaypointPath`, `AIDriver` (pure pursuit, avoidance, stuck recovery)
- [x] `RaceManager`, `RaceSector` (laps, sectors, standings)
- [x] EditMode tests for path maths and lap-time formatting
- [x] Tuning, performance, architecture and setup docs
- [x] Claude workflow: `CLAUDE.md`, project skills, slash commands

**Not done: the Unity project itself.** No `ProjectSettings/`, so none of the
above has compiled in-engine. That is step one of Day 1.

## Day 1 — Get it compiling and driving

Follow [`SETUP.md`](SETUP.md): install the editor, create and merge the project,
fix first-compile errors, commit the generated `.meta` files.

Then build the car prefab: `Rigidbody` (mass 1300), four `WheelCollider`s, four
visual wheels, wire up `CarController` with the reference values from
[`TUNING.md`](TUNING.md). Flat plane, a few cones.

**Done when:** you drive around a plane with a keyboard without flipping over.

## Day 2 — Feel and camera

The day that decides whether the game is good. Spend all of it here.

Tune against the `TUNING.md` symptom table. Add a Cinemachine chase camera with
damping, speed-based FOV (60→75), and a small kerb shake.

**Done when:** you can hold a four-wheel drift through a cone slalom and it
feels deliberate. Have someone else drive it; you have lost objectivity by now.

## Day 3 — The track

Centreline spline in Blender, bevel a road profile along it, add banking and
elevation — elevation is the cheapest drama available. One closed loop, ~2.5 km,
10–12 corners. Kerbs and barriers with ProBuilder.

Lay the `WaypointPath` nodes with per-node `halfWidth` and `speedLimit`, and the
`RaceSector` triggers.

**Done when:** you complete a lap and cannot escape the track.

## Day 4 — Graphics

The highest-payoff day after Day 2. In order:

1. Bake lighting (20–40 texels/unit, 2 bounces, denoised). Everything static
   except cars.
2. Post-processing volume: ACES tonemapping, bloom, vignette, slight
   teal/warm grade, chromatic aberration, camera motion blur.
3. Car material: metallic 0.9, smoothness 0.85, clearcoat.
4. **Box-projected reflection probes per track region** — this is what stops the
   car looking grey and plastic.
5. SSAO on Medium. Baked contact shadow under each car.
6. HDRI sky from Poly Haven, matched to the directional light angle.

**Done when:** a screenshot looks like something you would tap in a store
listing.

## Day 5 — Racing

Lay out the grid, wire `RaceManager`, place five AI cars with varied `skill`,
tune `AIDriver` against the AI table in `TUNING.md`.

**Done when:** a three-lap race against five AI finishes with correct standings
and nobody stuck on a barrier.

## Day 6 — UI, audio, VFX

Touch controls (tilt default, auto-throttle, brake button). HUD: speed, gear,
lap, position, minimap. Screens: menu → car select → race → results. Engine
audio as RPM-crossfaded loops driven by `EngineRpm01`, tyre squeal from
`MaxLateralSlip`. Tyre smoke, sparks, brake glow, speed lines.

**Plus the credits screen**, if any asset is CC-BY rather than CC0. That is a
licence obligation, not a nice-to-have.

**Done when:** playable on your phone, thumbs only, and it sounds like a car.

## Day 7 — Performance and polish

Profile on device per [`PERFORMANCE.md`](PERFORMANCE.md) — not in the editor,
which on an M5 Pro will tell you everything is fine. LODs, occlusion culling,
ASTC 6x6. Countdown animation, finish-line slow-mo, impact haptics. Icon,
signed build, a 30-second capture.

**Done when:** 60 fps at the 90th percentile on a real mid-range phone.

## If the week runs short

Cut in this order: car select → one car, five AI → three, VFX polish, minimap.

**Never cut Day 2 or Day 4.** Feel and lighting are 80% of whether this reads as
a real game.

## After the week

Roughly in value order:

1. A second track — proves the track pipeline is actually a pipeline
2. Ghost replays — cheap, and the single best driver-improvement feature
3. Time trial with leaderboards
4. Car upgrades and an economy
5. Controller support and a desktop build
6. Multiplayer — by far the largest item; do not start it early
