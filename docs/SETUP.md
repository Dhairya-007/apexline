# Setup

## What is already installed on this machine

Verified present: Unity Hub, Blender, Audacity, git, git-lfs, adb
(android-platform-tools), Node, Java, VS Code.

**Not yet installed: the Unity Editor itself.** Hub cannot install an editor
without a signed-in Unity account, so this step needs you.

## 1. Install the Unity Editor

1. Open **Unity Hub** → sign in (or create a free Unity account).
2. **Preferences → Licenses → Add → Get a free personal license.** Unity
   Personal is free below $200k annual revenue.
3. **Installs → Install Editor →** pick the newest **Unity 6 LTS** release.
   On an Apple Silicon Mac take the **Apple silicon** build, not Intel.
4. Tick these modules during install:
   - **Android Build Support** → including **OpenJDK** and **Android SDK & NDK Tools**
   - **iOS Build Support** (only if you intend to ship to iPhone; it is large)
   - **Mac Build Support (Mono)** — handy for fast desktop test builds

Download is roughly 8–12 GB with Android support. You have ~836 GB free.

## 2. The project is already set up

`ProjectSettings/`, `Packages/`, the URP assets and every `.meta` file are
committed. Open the repo folder from Unity Hub (**Add → Add project from
disk**) and it will compile.

It was created headlessly, which is reproducible if you ever need to redo it:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.6.11f1/Unity.app/Contents/MacOS/Unity

# add/repair packages
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod Apexline.EditorTools.ApexlineSetup.AddPackages

# recreate URP assets and reapply mobile settings
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod Apexline.EditorTools.ApexlineConfigure.Run

# run the EditMode tests without opening the editor
"$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode \
  -testResults /tmp/results.xml
```

## 3. Project settings — already applied

`ApexlineConfigure.Run` set these and they are committed. Listed so you know
what is in force, and because Linear colour space is painful to change later:

- **Player → Other Settings**
  - Color Space: **Linear** (this one is painful to change later)
  - Scripting Backend: **IL2CPP**
  - Target Architectures: **ARM64** only
  - Graphics APIs (Android): **Vulkan**, then OpenGLES3
- **Player → Resolution**: Default Orientation **Landscape Left**
- **Quality**: delete all levels except one mobile level; shadow distance 60 m,
  **one** cascade
- **Time**: Fixed Timestep **0.02**
- **Graphics**: assign the **Mobile_RPAsset** URP asset
- **Build Profiles**: Android, Development Build on for profiling

## 4. Packages to add

**Window → Package Manager → Unity Registry:**

- **Input System** (required — `PlayerDriver` keys off `ENABLE_INPUT_SYSTEM`)
- **Cinemachine** (the chase camera)
- **Test Framework** (usually present; required for the EditMode tests)
- **Post Processing** is part of URP already — do not add the legacy package

Unity will offer to enable the new Input System and restart. Accept.

## 5. Verify

```bash
./scripts/check-repo-hygiene.sh      # should pass with .meta checks active
```

In Unity: **Window → General → Test Runner → EditMode → Run All.** The
`WaypointPath` and lap-time tests should pass. If they do not compile, the
`.asmdef` references need fixing before anything else.

## 6. Android device setup

```bash
# On the phone: Settings > About > tap Build Number 7x to enable Developer mode,
# then Developer options > USB debugging.
adb devices        # should list your device as "device", not "unauthorized"
```

Then **File → Build and Run** in Unity. Keep a device build in the loop from day
one; a thing that only ever ran in the editor is a thing you do not know works.

## 7. GitHub (optional, for CI)

```bash
gh repo create apexline --public --source=. --remote=origin
git push -u origin main
```

For Unity CI you must add three repository secrets — **Settings → Secrets and
variables → Actions**:

| Secret | Where it comes from |
|---|---|
| `UNITY_LICENSE` | Contents of `/Library/Application Support/Unity/Unity_lic.ulf` |
| `UNITY_EMAIL` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |

Two things to know before you do this:

- **This puts your Unity account password in GitHub.** Consider a dedicated
  Unity account for CI rather than your main one.
- **Secrets are not available to pull requests from forks**, so Unity jobs are
  skipped there by design. The hygiene job still runs on every contribution.
- Sign out of Unity Hub after copying the `.ulf`, or the Personal seat stays
  held and CI activation can fail.

CI is genuinely optional. The hygiene job works without any of this.

## Enable the secret guard (required, one command)

Git does not install committed hooks automatically:

```bash
git config core.hooksPath .githooks
```

Without this the pre-commit and pre-push secret scans do nothing. Verify:

```bash
git config core.hooksPath          # must print .githooks
./scripts/scan-secrets.sh --history
```

See [`ALLOWED.md`](ALLOWED.md) for what is and is not allowed in the repo, and
[`WORKFLOW.md`](WORKFLOW.md) for the day-to-day loop.
