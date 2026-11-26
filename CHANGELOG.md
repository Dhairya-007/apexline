# Changelog

Notable changes to this project. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

**Gameplay**
- `CarController` — semi-arcade `WheelCollider` vehicle physics with
  speed-sensitive steering, counter-steer assist, front/rear anti-roll bars,
  speed-squared downforce, slip-triggered traction control, and a fake 6-speed
  gearbox for audio and HUD
- `CarInput` — the single control contract shared by player and AI
- `PlayerDriver` — tilt, on-screen wheel, gamepad and keyboard input with
  auto-throttle; compiles with or without the Input System package
- `WaypointPath` — racing line with per-node width and speed limits, fractional
  progress, curvature lookahead and editor gizmos
- `AIDriver` — pure-pursuit AI with corner braking, per-driver pace variance,
  contact avoidance and stuck recovery
- `RaceManager` / `RaceSector` — grid, countdown, lap timing, live standings and
  sector-validated laps

**Engineering**
- Assembly definitions enforcing one-directional layering
  (`Vehicle` ← `AI` ← `Race`)
- EditMode tests for waypoint path maths and lap-time formatting
- `scripts/check-repo-hygiene.sh` — six licence, LFS and `.meta` checks that run
  without a Unity licence
- CI with a fork-safe job split; tag-triggered and manual platform builds
- Git LFS routing for binary assets, with Unity YAML kept mergeable
- Issue forms, PR template, CODEOWNERS template, Dependabot

**Documentation**
- Setup, architecture, tuning, performance and roadmap guides
- `ASSET-LICENSES.md` — CC0/CC-BY-only policy and registry
- `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`
- `CLAUDE.md` plus three project skills and four slash commands

### Not yet done

- The Unity project itself: no `ProjectSettings/` or `Packages/`, so none of the
  gameplay code has compiled in-engine
- Track, UI, HUD, audio, VFX, car select, credits screen

[Unreleased]: https://github.com/Dhairya-007/apexline/commits/main
