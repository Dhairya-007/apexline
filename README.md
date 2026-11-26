# Apexline

A free, open-source mobile racing game built in Unity 6 — arcade handling with
weight, baked lighting, and a licence you can actually ship.

[![CI](https://github.com/Dhairya-007/apexline/actions/workflows/ci.yml/badge.svg)](https://github.com/Dhairya-007/apexline/actions/workflows/ci.yml)
[![Code licence: MIT](https://img.shields.io/badge/code-MIT-blue.svg)](LICENSE)
[![Assets: CC0 / CC-BY](https://img.shields.io/badge/assets-CC0%20%2F%20CC--BY-green.svg)](ASSET-LICENSES.md)
[![Unity 6 LTS](https://img.shields.io/badge/Unity-6%20LTS-black.svg)](https://unity.com)

> **Status: pre-alpha.** The Unity 6 project is set up and compiling — all four
> assemblies build with zero errors and all 13 EditMode tests pass. There is no
> track, car prefab, UI or audio yet, so it is not playable.
> See [`docs/ROADMAP.md`](docs/ROADMAP.md) for exactly what exists.

## Why this exists

Most open-source Unity games are unshippable for a reason nobody notices until
late: they contain Unity Asset Store content, which is licensed per-account and
carries no redistribution right. Apexline is built to be genuinely forkable —
**MIT code, CC0 or CC-BY assets only, enforced in CI**.

It is also built to be *free to make*. Unity Personal, Blender, Audacity, GIMP,
and CC0 asset libraries. Total cost: £0, plus $25 if you ever publish to Google
Play.

## Design

**Arcade with weight**, not simulation. A touchscreen player has no force
feedback and roughly two bits of steering precision, so the driver assists —
speed-sensitive steering limit, counter-steer assist, anti-roll bars,
slip-triggered traction control — are the design rather than a crutch.

**Players and AI share one physics path.** Both write the same `CarInput`
struct; neither touches `Rigidbody` directly. Opponents therefore race on
identical rules to the player, which is not something tuning can fix after the
fact. See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## What works

- Semi-arcade `WheelCollider` vehicle physics with five stability assists
- Tilt, on-screen wheel, gamepad and keyboard input with auto-throttle
- Pure-pursuit AI that brakes for upcoming corners, avoids contact, varies pace
  per driver, and recovers when stuck
- Race state machine: grid, countdown, lap timing, live standings, sector-
  validated laps that cannot be cut
- Repo hygiene CI that blocks licence and GUID mistakes before merge

## Quick start

```bash
git clone https://github.com/Dhairya-007/apexline.git
cd apexline
git lfs install && git lfs pull
./scripts/check-repo-hygiene.sh
```

Then follow [`docs/SETUP.md`](docs/SETUP.md) — it covers installing the Unity
editor, creating the project, the settings that are painful to change later, and
getting a build onto an Android phone.

## Documentation

| Document | What it covers |
|---|---|
| [`docs/SETUP.md`](docs/SETUP.md) | Editor install → project creation → device build |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | How the systems fit, and why |
| [`docs/TUNING.md`](docs/TUNING.md) | Handling symptom → parameter, with reference values |
| [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md) | 60 fps budget, device profiling, known hot paths |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Honest scope and the day-by-day plan |
| [`docs/WORKFLOW.md`](docs/WORKFLOW.md) | The day-to-day loop and daily commands |
| [`docs/ALLOWED.md`](docs/ALLOWED.md) | What may and may not go in the repo |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Conventions, branches, review expectations |
| [`ASSET-LICENSES.md`](ASSET-LICENSES.md) | Asset policy and the licence registry |

## Layout

```
Assets/_Game/Scripts/Vehicle/   CarController, CarInput, PlayerDriver
Assets/_Game/Scripts/AI/        WaypointPath, AIDriver
Assets/_Game/Scripts/Race/      RaceManager, RaceSector
Assets/_Game/Tests/EditMode/    engine-free unit tests
scripts/                        hygiene checks and git hooks
docs/                           everything above
.claude/                        project skills and slash commands
```

Each script folder is its own assembly, layered one-directionally
(`Vehicle` ← `AI` ← `Race`), so a change recompiles one assembly instead of the
project and the layering cannot be violated by accident.

## Security guard rails

No credential has ever been committed, and the repo is built so it stays that
way:

- **`scripts/scan-secrets.sh`** — rejects forbidden filenames and
  high-confidence secret values (AWS, GitHub, Google, Slack, OpenAI/Anthropic,
  GitLab, JWTs, Azure, PEM keys, Unity `.ulf`)
- **Pre-commit hook** — blocks the commit
- **Pre-push hook** — re-audits the *entire* history, because a public repo's
  history is public permanently
- **Security workflow** — full-history scan, plus audits that forbid
  `pull_request_target`, require an explicit `permissions:` block in every
  workflow, and require third-party actions to be SHA-pinned
- **Hardened `.gitignore`** covering keystores, `.ulf`, `.env`, certificates,
  SSH keys, service accounts and `gradle.properties`

Enable the hooks after cloning — git will not do it for you:

```bash
git config core.hooksPath .githooks
```

## Contributing

Contributions are welcome — see [`CONTRIBUTING.md`](CONTRIBUTING.md).

Handling complaints belong in the **Handling feedback** issue form rather than a
bug report; "the car feels bad" needs a symptom, a speed range and a corner to
be actionable. Read [`docs/TUNING.md`](docs/TUNING.md) first.

AI-assisted contribution is supported and set up for — see
[`CLAUDE.md`](CLAUDE.md) and [`.claude/`](.claude). You remain the author:
review every line, and be able to explain why it is correct.

## Licence

Code is [MIT](LICENSE). Assets keep their own licences, recorded in
[`ASSET-LICENSES.md`](ASSET-LICENSES.md) — CC0 or CC-BY only, never Unity Asset
Store content.
