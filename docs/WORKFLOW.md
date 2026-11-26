# Workflow

The day-to-day loop. [`CONTRIBUTING.md`](../CONTRIBUTING.md) holds the rules;
this holds the sequence.

## One-time setup

```bash
git clone https://github.com/Dhairya-007/apexline.git
cd apexline
git lfs install && git lfs pull
git config core.hooksPath .githooks    # REQUIRED - enables the secret guard
./scripts/check-repo-hygiene.sh
./scripts/scan-secrets.sh
```

**Do not skip the `core.hooksPath` line.** Git does not install committed hooks
automatically, so without it the pre-commit and pre-push secret guards do
nothing. Everything else is recoverable; a leaked credential is not.

Then [`SETUP.md`](SETUP.md) for the Unity side.

## The loop

```
┌─ 1. Branch ───────────────────────────────────────────────┐
│  git switch -c feat/ai-overtaking                         │
├─ 2. Change ONE thing ─────────────────────────────────────┤
│  Tuning? one parameter. Perf? one optimisation.           │
├─ 3. Verify in the editor ─────────────────────────────────┤
│  Drive it. Run EditMode tests. Numbers, not impressions.  │
├─ 4. Commit (hooks run automatically) ─────────────────────┤
│  Conventional Commits. Body says WHY.                     │
├─ 5. Push + PR ────────────────────────────────────────────┤
│  pre-push audits full history. Fill in the template.      │
├─ 6. CI ───────────────────────────────────────────────────┤
│  Hygiene + Security always. Unity tests on non-fork PRs.  │
└─ 7. Merge, delete branch ─────────────────────────────────┘
```

## Branch names

```
feat/ai-overtaking         fix/wheel-sink-on-kerbs
perf/lod-cull-distance     tune/reduce-understeer-high-speed
docs/tuning-guide          test/race-standings
ci/cache-library
```

## Commits

```bash
git commit   # hooks run: secret scan, then hygiene checks
```

Format: `type(scope): subject`, then a body explaining **why** and what you
rejected. Scopes: `vehicle`, `ai`, `race`, `ui`, `audio`, `render`, `track`,
`input`, `build`, `ci`, `docs`.

A commit the hooks reject is telling you something real. `--no-verify` exists,
but if you use it on a secret-scan failure you have published a credential.

## Daily commands

```bash
./scripts/check-repo-hygiene.sh      # licensing, LFS, .meta
./scripts/scan-secrets.sh            # tree + index
./scripts/scan-secrets.sh --history  # every commit (pre-push does this)
shellcheck -S warning scripts/*.sh scripts/hooks/*.sh .githooks/*

./scripts/unity-status.sh            # editor + Android module + project state
./scripts/unity-log.sh               # Editor.log, credentials redacted
./scripts/unity-log.sh --list        # which Unity logs exist

adb devices
adb logcat -s Unity
adb shell dumpsys gfxinfo <package> framestats

gh run list --limit 5                # CI status
gh pr create                         # uses the template
```

Unity tests: **Window → General → Test Runner → EditMode → Run All.**

## Working with Claude

Slash commands, each wired to a project skill:

| Command | Use it for |
|---|---|
| `/tune <symptom>` | Handling complaints — diagnoses to one parameter |
| `/perf [symptom]` | Frame rate — insists on a device measurement first |
| `/hygiene` | Run the checks and explain failures |
| `/newcar <name>` | Add a drivable car, licence checks included |

[`CLAUDE.md`](../CLAUDE.md) carries the rules Claude must not break. Two habits
that make this work:

- **Give it the symptom, not the fix.** "Oversteers out of turn 3 above 120" is
  a solvable problem; "make the handling better" is not.
- **Distrust any claim about feel or frame rate.** Claude cannot drive the car or
  see the screen. If it says something feels right, it is guessing — the skills
  tell it to say so, but verify anyway.

## Review

Self-review before requesting one:

```bash
git diff main...HEAD          # read your own diff
./scripts/scan-secrets.sh --history
```

- Tuning change? Before/after table + lap time, or it cannot be reviewed.
- Perf change? Device, build type, before/after 90th-percentile frame time.
- New asset? `ASSET-LICENSES.md` row in the same commit, `.meta` included.

`/code-review` is available in Claude Code for a correctness pass on the diff.

## Releases

```bash
git tag -a v0.2.0 -m "Vertical slice: one track, two cars, five AI"
git push origin v0.2.0        # triggers the Android build workflow
```

Update [`CHANGELOG.md`](../CHANGELOG.md) in the same commit as the tag, moving
items out of `Unreleased`.
