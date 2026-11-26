---
name: asset-sourcing
description: Find, add or review art, audio, models, textures, HDRIs or fonts for Apexline while keeping it legally redistributable. Use whenever assets are being added, a licence is in question, someone proposes a Unity Asset Store package, or an asset needs a credit. Enforces the CC0/CC-BY-only policy and the licence registry.
---

# Asset sourcing

The project is public, so every committed file is redistributed to everyone who
clones it. That makes licensing a correctness problem, not paperwork.

## Three refusals

1. **Unity Asset Store assets — including free ones.** The EULA grants a
   per-account licence with no redistribution right. "It was free" is irrelevant.
   This is the most common way an open-source Unity game becomes legally
   unshippable. If someone proposes one, say why and offer a CC0 alternative.
2. **Content ripped from commercial games.** No car models, liveries, logos,
   track layouts or engine recordings from Real Racing, Forza, Gran Turismo, F1
   or Assetto Corsa.
3. **Real manufacturer car shapes and team liveries.** Trademarked. Original or
   generic designs only — this is a design constraint to work within, not a risk
   to accept.

## Approved sources

CC0 preferred, because CC-BY attribution drifts and eventually gets dropped:

| Source | Licence | For |
|---|---|---|
| kenney.nl | CC0 | UI, racing kit, audio |
| quaternius.com | CC0 | Low-poly vehicles, props |
| polyhaven.com | CC0 | HDRIs, PBR textures |
| ambientcg.com | CC0 | Asphalt, concrete, metal |
| poly.pizza | CC0 / CC-BY | Props — check per model |
| freesound.org | varies | Engine, tyre, ambience — check per file |
| fonts.google.com | OFL / Apache | UI type |

## Adding an asset

In one commit:

1. File goes in `Assets/_Game/Art/<category>/` or `Assets/_Game/Audio/`
2. Row added to `ASSET-LICENSES.md` — path, asset, author, **real source URL**
   (not a search page), licence, whether modified
3. If CC-BY: credit added to the in-game credits screen, not only the file
4. `.meta` committed with the asset

`./scripts/check-repo-hygiene.sh` fails on unregistered art and audio, on binary
files not routed through LFS, and on missing `.meta` files. CI runs it on every
pull request, including from forks.

## Verifying a licence

Never infer a licence from a site's general terms — check the specific asset
page, and record the URL you checked. If it is unclear, treat it as unusable and
say so. An asset with an uncertain licence is worse than no asset: it is a
latent obligation to redo the work later.
