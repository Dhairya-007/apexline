---
description: Run the repo hygiene checks and explain any failures
---

Run the licensing, LFS and `.meta` checks, then shellcheck:

```bash
./scripts/check-repo-hygiene.sh
shellcheck -S warning scripts/*.sh scripts/hooks/*.sh
```

For each failure, explain what it means and the fix:

- **Credential or `.ulf` file tracked** — remove from the index immediately and
  treat the credential as compromised; it is in git history.
- **Asset Store content** — must be deleted, not relicensed. See
  `ASSET-LICENSES.md`.
- **Binary not in LFS** — `.gitattributes` needs the extension, and the file
  needs re-adding so the filter applies.
- **Missing `.meta`** — commit it with the asset, or Unity regenerates the GUID
  and breaks every reference to that asset for everyone else.
- **Unregistered art/audio** — add the `ASSET-LICENSES.md` row with the real
  source URL.

If the `.meta` checks report as skipped, the Unity project has not been created
yet — see `docs/SETUP.md`.
