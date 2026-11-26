# Apexline — working notes for Claude

A free, open-source mobile racing game in Unity 6 (URP). Target: 60 fps on a
mid-range Android phone. See [`README.md`](README.md) for what it is,
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for how it fits together.

## Non-negotiables

These are the rules that are expensive or impossible to fix later.

1. **AI drives through `CarInput`.** Never let `AIDriver` touch `Rigidbody` or
   `WheelCollider` directly. Opponents must obey identical physics to the
   player, or racing is unfair in ways no amount of tuning repairs.
2. **Physics in `FixedUpdate`, visuals in `Update`.** Reading or writing a
   `Rigidbody` or `WheelCollider` outside `FixedUpdate` produces jitter that
   looks like a tuning problem and is not.
3. **Assembly layering is one-directional:** `Vehicle` ← `AI` ← `Race`. A needed
   reference in the other direction is a design problem. Say so; do not add it.
4. **Never commit Unity Asset Store assets, free ones included.** The EULA is
   per-account with no redistribution right. This would make the project legally
   unshippable. CC0 or CC-BY only — see [`ASSET-LICENSES.md`](ASSET-LICENSES.md).
5. **`.meta` files are committed with their asset.** A missing `.meta` makes
   Unity regenerate the GUID and silently break every reference to that asset
   for everyone else who pulls.
6. **Never move a licensed CI job into `pull_request_target`.** That hands
   repository secrets to untrusted fork code. The fork skip in
   [`.github/workflows/ci.yml`](.github/workflows/ci.yml) is deliberate.
7. **Do not commit, push or open a pull request unless asked.**

## Layout

```
Assets/_Game/
  Scripts/Vehicle/   CarController, CarInput, PlayerDriver   (no dependencies)
  Scripts/AI/        WaypointPath, AIDriver                  (-> Vehicle)
  Scripts/Race/      RaceManager, RaceSector                 (-> Vehicle, AI)
  Scripts/UI/        HUD, menus                              (not written yet)
  Scripts/Util/      shared helpers                          (not written yet)
  Tests/EditMode/    engine-free unit tests
  Art/ Audio/        CC0 / CC-BY assets, each with a licence row
scripts/             repo hygiene and hooks
docs/                setup, architecture, tuning, roadmap
```

Each `Scripts/` subfolder owns an `.asmdef`. New top-level systems get their own
assembly; do not dump code into an existing one for convenience.

## Commands

```bash
./scripts/check-repo-hygiene.sh    # licensing, LFS and .meta checks (CI runs this)
shellcheck -S warning scripts/*.sh

# Unity tests: Window > General > Test Runner > EditMode > Run All
# There is no CLI test path configured; GameCI runs them on Linux in CI.

adb devices                                   # confirm a device before profiling
adb logcat -s Unity                           # Unity log from the device
adb shell dumpsys gfxinfo <package> framestats # frame timing on device
```

## Conventions

- 4-space indent, LF, UTF-8 ([`.editorconfig`](.editorconfig)).
- `_camelCase` private fields, `PascalCase` public, one type per file.
- Namespaces match assemblies: `Apexline.Vehicle`, `Apexline.AI`, `Apexline.Race`.
- Conventional Commits with scopes `vehicle`, `ai`, `race`, `ui`, `audio`,
  `render`, `track`, `input`, `build`, `ci`, `docs`.
- **Commit bodies say why, and what was rejected.** A one-line commit on a
  tuning change is a commit nobody can safely revert.
- Explain why a magic number is that number. Especially physics constants.

## How to help well here

**Tuning requests.** "The car feels wrong" is a diagnosis task, not a coding
task. Ask which symptom, at what speed, in which corner, then consult
[`docs/TUNING.md`](docs/TUNING.md) — it maps symptom to parameter. Change one
parameter at a time and report the lap time. Do not rewrite the controller.

**Performance requests.** Do not optimise from inspection. Get a number from a
real device first (`adb shell dumpsys gfxinfo`), change one thing, measure
again. The budget is in [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md). On this
machine — an M5 Pro — the editor will hide every performance problem the target
phone has; the editor frame rate means nothing.

**Asset requests.** Source from the approved CC0 list only, add the
`ASSET-LICENSES.md` row in the same commit, and never suggest the Asset Store.

**What you cannot verify.** You cannot run Unity, drive the car, or see the
screen. So do not claim handling feels right, that a scene renders, or that
frame rate improved. State what you changed and what the user needs to check.
This matters more here than in most projects: the output of this codebase is a
feel, and feel is not inspectable from source.

## Project state

Scaffolding and core gameplay scripts exist and are reviewed, but **the Unity
project itself has not been created yet** — there is no `ProjectSettings/` or
`Packages/`, so nothing has ever compiled in-engine. Expect first-compile
fixes. [`docs/SETUP.md`](docs/SETUP.md) has the creation steps;
[`docs/ROADMAP.md`](docs/ROADMAP.md) has the day plan and what is done.
