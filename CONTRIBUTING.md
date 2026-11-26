# Contributing to Apexline

Thanks for looking. This project aims to be a genuinely good mobile racing game
that is free to build, free to fork, and free of licensing landmines.

## Before you start

- **Handling complaints go in the Handling feedback issue form**, not a bug
  report, and read [`docs/TUNING.md`](docs/TUNING.md) first.
- **Open an issue before a large change.** A refactor of the vehicle controller
  is a conversation, not a surprise pull request.
- Small, obvious fixes need no issue. Send the pull request.

## Setup

See [`docs/SETUP.md`](docs/SETUP.md). The short version:

```bash
git clone <your fork>
cd apexline
git lfs install && git lfs pull
# Open the folder in Unity Hub with the version in ProjectSettings/ProjectVersion.txt
```

## Branches and commits

Branch off `main`:

```
feat/ai-overtaking    fix/wheel-sink-on-kerbs    docs/tuning-guide
perf/lod-cull-distance    test/race-standings
```

Commits follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(vehicle): add speed-sensitive steering limit
fix(ai): stop drivers braking mid-corner on banked turns
perf(render): cull grandstand props beyond 150 m
```

Scopes in use: `vehicle`, `ai`, `race`, `ui`, `audio`, `render`, `track`,
`input`, `build`, `ci`, `docs`.

**Write the body for the next person, not for yourself.** Say *why*, and what
you decided against. A one-line commit explaining a tuning change is a commit
nobody can safely revert.

## Code conventions

Enforced by [`.editorconfig`](.editorconfig); your IDE should pick it up.

- 4-space indent, LF endings, UTF-8.
- `_camelCase` for private fields, `PascalCase` for public members and types.
- One type per file, named after the file.
- **Namespaces match assemblies**: `Apexline.Vehicle`, `Apexline.AI`,
  `Apexline.Race`.
- Respect the assembly layering — `Vehicle` ← `AI` ← `Race`. If you need a
  reference pointing the other way, you have found a design problem, not a
  missing reference. Raise it in an issue.
- XML doc comments on public types and on anything non-obvious. Explain *why* a
  magic number is that number.

### Physics code

- Anything reading or writing `Rigidbody` or `WheelCollider` belongs in
  `FixedUpdate`, never `Update`.
- Visual-only work (wheel meshes, camera, UI) belongs in `Update` or
  `LateUpdate`, so it runs at display rate.
- Never set `transform.position` on a car directly during a race. Use
  `CarController.ResetTo`.
- AI must drive through `CarInput`. An AI that calls physics directly is an AI
  racing on different rules from the player, and that is unfixable later.

## Testing

```bash
./scripts/check-repo-hygiene.sh      # licensing, LFS and .meta checks
# Unity: Window > General > Test Runner > EditMode > Run All
```

Logic that can be tested without the engine should have a test: path maths,
standings sorting, timing, formatting. Do not try to unit-test feel — verify
that by driving it, and report the lap time.

## Assets

Read [`ASSET-LICENSES.md`](ASSET-LICENSES.md). The rules that get pull requests
closed:

1. **No Unity Asset Store content, free or paid.** The EULA licenses it to your
   account and grants no redistribution right.
2. **No content ripped from commercial games.** No real manufacturer car shapes,
   team liveries, logos, or recorded engine audio from other games.
3. CC0 preferred, CC-BY accepted with attribution added to the credits screen.
4. Commit `.meta` files with their assets. A missing `.meta` makes Unity
   regenerate the GUID, which breaks every reference to that asset for everyone
   else who pulls.

## Pull requests

The template asks how you verified the change. Please answer it properly.

- Tuning changes need a before/after parameter table and a lap time on the
  reference track.
- Rendering or physics changes need frame time numbers from a real device.
- CI runs hygiene checks on every pull request. Unity tests are skipped on
  pull requests from forks, because repository secrets are not available there —
  that is deliberate, not a misconfiguration.

## Working with an AI assistant

This repository is set up for it, and that is a supported workflow, not a
guilty secret. See [`CLAUDE.md`](CLAUDE.md) and `.claude/`.

Two expectations:

- **You are the author.** Review every line you submit and be able to explain
  why it is correct. "The model wrote it" is not a review.
- **Say so in the pull request** if a change was largely generated, so reviewers
  know where to look harder.

## Licence

Code contributions are MIT, per [`LICENSE`](LICENSE). Assets keep their own
licence, recorded in `ASSET-LICENSES.md`. By opening a pull request you confirm
you have the right to contribute what it contains.
