# Security policy

This is a single-player game client, so the realistic risk surface is small:
malicious asset or save files, and dependency supply chain.

## Reporting a vulnerability

Please **do not** open a public issue for anything exploitable.

Use GitHub's private reporting: **Security → Advisories → Report a
vulnerability** on this repository. Expect an acknowledgement within 7 days.

Useful detail to include:
- Affected version, commit SHA, platform and Unity version
- Reproduction steps, and a crash log or stack trace if you have one
- What an attacker gains

## In scope

- Code execution via loaded assets, save files, replay files or mod content
- Path traversal or arbitrary file write from any import or save path
- Build scripts or CI workflows that could leak repository secrets

## Out of scope

- Gameplay cheating and score manipulation in single-player
- Issues that require a modified local build to trigger
- Bugs in Unity itself — report those to Unity, and link the ticket here

## For contributors

- Never commit a keystore, `.ulf` license file, API key or `.env`. `.gitignore`
  blocks the usual names, but it is not a substitute for checking `git diff`.
- CI secrets are deliberately unavailable to pull requests from forks. Do not
  "fix" this by moving a licensed job into a `pull_request_target` workflow —
  that hands repository secrets to untrusted code.
