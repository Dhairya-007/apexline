# Allowed and forbidden

The single reference for "can I put this in the repo?" Everything here is
enforced by [`scripts/check-repo-hygiene.sh`](../scripts/check-repo-hygiene.sh),
[`scripts/scan-secrets.sh`](../scripts/scan-secrets.sh) and the
[Security workflow](../.github/workflows/security.yml) — not by trust.

---

## Licences

### Code

| Licence | Allowed | Note |
|---|---|---|
| MIT, BSD-2/3, Apache-2.0, Unlicense, CC0 | ✅ | Compatible with our MIT |
| MPL-2.0 | ⚠️ | File-level copyleft; ask first |
| **GPL / AGPL / LGPL** | ❌ | Would force this project to relicense |
| No licence stated | ❌ | Unlicensed code is "all rights reserved" |

### Assets

| Licence | Allowed | Obligation |
|---|---|---|
| CC0 / public domain | ✅ preferred | None |
| CC-BY | ✅ | Credit in the **in-game credits screen**, not just a file |
| OFL (fonts) | ✅ | Keep the licence file; do not rename the font |
| CC-BY-SA | ❌ | Share-alike propagates to the whole work |
| CC-BY-NC | ❌ | Non-commercial is incompatible with a free game people may fork |
| CC-BY-ND | ❌ | No-derivatives forbids the modification we need |
| "Free for personal use" | ❌ | Not a licence |
| **Unity Asset Store** | ❌ | Per-account EULA, no redistribution right |

CC0 is strongly preferred: no attribution to drift, no propagation, nothing for
a future contributor to accidentally drop.

---

## Asset sources

### ✅ Approved

| Source | Licence | For |
|---|---|---|
| [kenney.nl](https://kenney.nl) | CC0 | UI, racing kit, audio |
| [quaternius.com](https://quaternius.com) | CC0 | Low-poly vehicles, props |
| [polyhaven.com](https://polyhaven.com) | CC0 | HDRIs, PBR textures |
| [ambientcg.com](https://ambientcg.com) | CC0 | Asphalt, concrete, metal |
| [poly.pizza](https://poly.pizza) | CC0 / CC-BY | Props — check per model |
| [freesound.org](https://freesound.org) | varies | Audio — check per file |
| [opengameart.org](https://opengameart.org) | varies | Misc — check per file |
| [fonts.google.com](https://fonts.google.com) | OFL / Apache | UI type |

### ❌ Forbidden

- **Unity Asset Store** — including the free section. The most common reason an
  open-source Unity game turns out to be unshippable.
- **Any commercial game's content** — Real Racing, Forza, Gran Turismo, F1,
  Assetto Corsa, Need for Speed. No models, liveries, logos, track layouts, or
  recorded engine audio.
- **Real manufacturer car shapes and team liveries** — trademarked. Original or
  generic designs only.
- **Real circuit names** paired with their exact layout, where the name is
  licensed. An original track inspired by one is fine.
- **AI-generated assets whose training provenance is unclear** — treat as
  unlicensed until the tool's terms say otherwise in writing.
- **Sketchfab "free" downloads** without checking the per-model licence. Many
  are CC-BY-NC or ripped.

### Verifying

Check the **specific asset page**, never the site's general terms, and record
the URL you checked in `ASSET-LICENSES.md`. If the licence is unclear, the asset
is unusable — an uncertain licence is a latent obligation to redo the work.

---

## Files that must never be committed

Blocked by `.gitignore`, the hygiene script, the secret scanner, and both git
hooks.

| Pattern | Why |
|---|---|
| `*.keystore`, `*.jks` | Android signing keys — loss means you cannot update your own app |
| `*.ulf` | Unity licence file |
| `.env`, `.env.*` | Credentials |
| `*.p12`, `*.pfx`, `*.pem`, `*.key` | Certificates and private keys |
| `*.mobileprovision` | iOS provisioning |
| `id_rsa`, `id_ed25519` | SSH private keys |
| `credentials.json`, `serviceAccount*.json` | Cloud service accounts |
| `local.properties` | Local SDK paths, sometimes credentials |
| `.npmrc`, `.pypirc` | Registry tokens |
| `.claude/settings.local.json` | Personal overrides |
| `Library/`, `Temp/`, `obj/`, `Build/` | Unity generated; huge and machine-specific |

**If one is ever committed:** rewrite history (`git filter-repo`) **and rotate
the credential.** Deleting it in a new commit does nothing — the old commit is
still in history, and on a public repo it is already mirrored.

---

## Secret values the scanner rejects

AWS access keys (`AKIA`/`ASIA`), GitHub tokens (`ghp_`, `gho_`, `github_pat_`),
Google API keys (`AIza`), Slack tokens (`xox*`), OpenAI and Anthropic keys
(`sk-`, `sk-ant-`), GitLab PATs (`glpat-`), JWTs, Azure connection strings,
PEM private-key blocks, and Unity `.ulf` markers.

References are fine — `${{ secrets.UNITY_PASSWORD }}` is not a secret. Values
are not.

---

## GitHub Actions

| Source | Pinning |
|---|---|
| `actions/*`, `github/*` | Major tag (`@v4`) — first-party |
| `game-ci/*` | Major tag (`@v4`) — core to the Unity toolchain |
| Anything else | **Full commit SHA, with the version in a comment** |

Never `@main` or `@master` on a third-party action. Never a SHA you have not
verified against the upstream repository — an unverified pin looks auditable and
is not.

`pull_request_target` is forbidden outright: it runs with repository secrets
available to fork-authored code.

---

## Unity packages

| Package | Status |
|---|---|
| Input System, Cinemachine, Test Framework, URP | ✅ Required |
| Burst, Mathematics, Addressables | ⚠️ Only with a stated reason |
| Legacy Post Processing Stack v2 | ❌ URP has its own; the two conflict |
| Anything from a non-Unity registry | ❌ Ask first |

---

## Commands that leak credentials

Unity and Unity Hub pass secrets as **command-line arguments**, which every
process on the machine can read:

```
Unity ... -accessToken <live token> -hubSessionId <id> -cbmEditorLicense <hash>
```

Unity also writes that whole command line into `Editor.log`, one argument per
line.

| ❌ Forbidden | ✅ Use instead |
|---|---|
| `ps aux`, `ps -ef`, `ps -eo args` | `./scripts/unity-status.sh` |
| `pgrep -l`, `pgrep -af` | `pgrep -f <pat> \| wc -l` |
| `cat ~/Library/Logs/Unity/Editor.log` | `./scripts/unity-log.sh` |
| `printenv`, bare `env`, `export -p` | `printenv VARNAME` |

`scripts/hooks/block-token-exposure.sh` denies these as a PreToolUse hook, and
names the alternative in the refusal.

**If a token does get printed:** it is a short-lived Hub session token, not in
the repo. Don't paste that output anywhere. To invalidate it, sign out of Unity
Hub and back in.

## Claude Code permissions

Defined in [`.claude/settings.json`](../.claude/settings.json).

- **Auto-allowed:** read-only git, search, `jq`, `shellcheck`, `adb` device and
  profiling commands, `gh` read commands, the repo's own scripts.
- **Always asked:** `git push`, `git reset --hard`, `git clean`, `gh pr create`,
  `gh pr merge`, `gh release`.
- **Denied outright:** reading `*.keystore`, `*.ulf`, `.env`,
  `local.properties`; `git push --force`.

Personal additions go in `.claude/settings.local.json`, which is gitignored.
