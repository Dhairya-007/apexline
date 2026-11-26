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

## 2. Create the Unity project

The repository already contains `Assets/_Game/`, but Unity's own
`ProjectSettings/` and `Packages/` folders do not exist yet, so nothing has
compiled in-engine. Unity Hub will not create a project into a non-empty folder,
so create it elsewhere and merge:

```bash
# In Unity Hub: New project > Universal 3D (URP) > name it "apexline-tmp",
# location ~/Desktop. Let it finish opening once, then quit Unity.

cd "/Users/dhairyaoza/Desktop/Open Source/apexline"
cp -R ~/Desktop/apexline-tmp/ProjectSettings ./
cp -R ~/Desktop/apexline-tmp/Packages ./
cp -R ~/Desktop/apexline-tmp/Assets/Settings ./Assets/   # URP render assets

# Confirm the Unity version got recorded - CI reads this file
cat ProjectSettings/ProjectVersion.txt

rm -rf ~/Desktop/apexline-tmp
```

Now open `/Users/dhairyaoza/Desktop/Open Source/apexline` from Unity Hub
(**Add → Add project from disk**). Unity will import `Assets/_Game/`, generate
every `.meta` file, and compile the scripts.

**Expect first-compile errors.** None of this code has ever been through a Unity
compiler. Fix them before anything else.

Then commit the generated files — they belong in the repo:

```bash
./scripts/check-repo-hygiene.sh      # the .meta checks now actually run
git add ProjectSettings Packages Assets
git commit -m "build: initialise Unity project and import generated meta files"
```

## 3. Project settings to change immediately

**Edit → Project Settings:**

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

## Updating the repo settings before going public

- `CODE_OF_CONDUCT.md` — replace `<REPLACE_WITH_CONTACT_EMAIL>`
- `.github/CODEOWNERS` — uncomment and insert your GitHub handle
- `.github/ISSUE_TEMPLATE/config.yml` — replace `REPLACE_ME` in both URLs
- `README.md` — replace the clone URL
