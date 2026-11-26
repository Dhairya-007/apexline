# Asset licenses

The MIT license in [`LICENSE`](LICENSE) covers **code only**. Every art, audio
and font asset keeps its own license, and each one must be recorded here before
it is merged.

## Hard rules

1. **Only CC0, CC-BY, or an explicitly redistributable license.** This repository
   is public, so every file in it is redistributed to everyone who clones it.
2. **Never commit Unity Asset Store assets — including the free ones.** The Asset
   Store EULA licenses them to *you*, the account holder. It does not let you
   redistribute them in a public repository, and "it was free" makes no
   difference. This is the single most common way open-source Unity projects end
   up legally unshippable.
3. **No ripped assets.** No car models, liveries, logos, track layouts, or engine
   recordings taken from Real Racing, Forza, Gran Turismo, F1, Assetto Corsa, or
   any other commercial game. Real manufacturer car shapes and team liveries are
   trademarked; original or generic designs only.
4. **CC-BY requires attribution to ship in the build**, not just in this file.
   Add it to the in-game credits screen at the same time.

## Approved sources

| Source | License | Good for |
|---|---|---|
| [Kenney](https://kenney.nl) | CC0 | UI, racing kit, audio, prototyping |
| [Quaternius](https://quaternius.com) | CC0 | Low-poly vehicles, props, nature |
| [Poly Haven](https://polyhaven.com) | CC0 | HDRIs, PBR textures, models |
| [ambientCG](https://ambientcg.com) | CC0 | Asphalt, concrete, kerbs, metal |
| [Poly Pizza](https://poly.pizza) | CC0 / CC-BY | Props (check per model) |
| [freesound.org](https://freesound.org) | varies | Engine, tyre, ambience (check per file) |
| [OpenGameArt](https://opengameart.org) | varies | Misc (check per file) |
| [Google Fonts](https://fonts.google.com) | OFL / Apache | UI typefaces |

CC0 is strongly preferred: no attribution burden, no license propagation, no
risk of a contributor dropping the credit line later.

## Registry

Every asset directory must have a matching row. Keep paths repo-relative.

| Path | Asset | Author | Source URL | License | Modified? |
|---|---|---|---|---|---|
| _(none yet)_ | | | | | |

### How to add a row

1. Download to `Assets/_Game/Art/<category>/` (or `Assets/_Game/Audio/`).
2. Add a row above with the real source URL — not a search page.
3. If the license is CC-BY, also add the credit to the in-game credits screen.
4. Commit the asset and this file together, so the license never lags the asset.

## Audit

```bash
# List art/audio files that exist on disk, to reconcile against the table above
find Assets/_Game/Art Assets/_Game/Audio -type f \
  ! -name '*.meta' ! -name '.gitkeep' 2>/dev/null | sort
```

If a file is in the tree and not in the table, either document it or delete it.
