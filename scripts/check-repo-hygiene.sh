#!/usr/bin/env bash
# Repository hygiene checks. Runs in CI without a Unity license, so it also
# covers pull requests from forks, where repository secrets are unavailable.
#
# Usage: ./scripts/check-repo-hygiene.sh
set -uo pipefail

fail=0
note () { printf '  %s\n' "$1"; }
problem () { printf '\033[31mFAIL\033[0m %s\n' "$1"; fail=1; }
ok () { printf '\033[32m ok \033[0m %s\n' "$1"; }

# --- 1. Secrets and licence files must never be committed -------------------
secret_hits=$(git ls-files | grep -iE '(^|/)(\.env|.*\.keystore|.*\.ulf|.*\.p12|.*\.mobileprovision|credentials\.json|serviceAccount.*\.json)$' || true)
if [ -n "$secret_hits" ]; then
  problem "Credential-like files are tracked by git:"
  echo "$secret_hits" | while read -r f; do note "$f"; done
else
  ok "No credential or licence files tracked"
fi

# --- 2. No Unity Asset Store content ---------------------------------------
# The Asset Store EULA licenses assets per-account with no redistribution
# right, so shipping them in a public repo is a licensing violation.
store_hits=$(git ls-files | grep -iE 'AssetStoreTools|Asset Store|/Standard Assets/' || true)
if [ -n "$store_hits" ]; then
  problem "Possible Unity Asset Store content is tracked (see ASSET-LICENSES.md):"
  echo "$store_hits" | while read -r f; do note "$f"; done
else
  ok "No Asset Store content detected"
fi

# --- 3. Binary assets must go through Git LFS ------------------------------
binary_ext='\.(png|jpg|jpeg|tga|psd|exr|hdr|tif|fbx|blend|obj|glb|gltf|wav|ogg|mp3|mp4|ttf|otf)$'
not_lfs=""
while read -r f; do
  [ -z "$f" ] && continue
  if ! git check-attr filter -- "$f" | grep -q 'filter: lfs'; then
    not_lfs="${not_lfs}${f}"$'\n'
  fi
done < <(git ls-files | grep -iE "$binary_ext" || true)

if [ -n "${not_lfs//[$'\n']/}" ]; then
  problem "Binary assets are not routed through Git LFS (check .gitattributes):"
  echo "$not_lfs" | while read -r f; do [ -n "$f" ] && note "$f"; done
else
  ok "All binary assets route through Git LFS"
fi

# --- 4. Every Unity asset needs its .meta committed ------------------------
# Unity writes .meta files the first time it opens the project. Before that
# there is nothing to check, so skip rather than fail a fresh clone.
if [ ! -f ProjectSettings/ProjectVersion.txt ]; then
  printf '\033[33mskip\033[0m %s\n' "Unity project not initialised yet - skipping .meta checks"
  echo
  if [ "$fail" -ne 0 ]; then
    echo "Repository hygiene checks FAILED."
    exit 1
  fi
  echo "Repository hygiene checks passed."
  exit 0
fi

# --- 4. Every Unity asset needs its .meta committed ------------------------
# A missing .meta makes Unity regenerate the GUID, which silently breaks every
# reference to that asset for everyone else on the team.
# Unity's own asset-ignore rules: it never imports, and never writes a .meta
# for, a path where any component starts with "." or ends with "~", or that is
# named "cvs", or ends in ".tmp". Requiring a .meta for those would demand a
# file Unity refuses to create.
unity_ignores () {
  case "$1" in
    .*|*/.*)   return 0 ;;   # dotfile or anything under a dot-folder
    *~)        return 0 ;;   # *~ already covers */*~
    *.tmp)     return 0 ;;
    cvs|*/cvs) return 0 ;;
  esac
  return 1
}

missing_meta=""
while read -r f; do
  [ -z "$f" ] && continue
  case "$f" in
    *.meta) continue ;;
  esac
  unity_ignores "$f" && continue
  if ! git ls-files --error-unmatch "${f}.meta" >/dev/null 2>&1; then
    missing_meta="${missing_meta}${f}"$'\n'
  fi
done < <(git ls-files 'Assets/*' | grep -vE '\.meta$' || true)

if [ -n "${missing_meta//[$'\n']/}" ]; then
  problem "Assets committed without their .meta file (Unity will regenerate GUIDs and break references):"
  echo "$missing_meta" | while read -r f; do [ -n "$f" ] && note "$f"; done
else
  ok "Every tracked asset has its .meta"
fi

# --- 5. Orphaned .meta files ----------------------------------------------
orphan_meta=""
while read -r m; do
  [ -z "$m" ] && continue
  base="${m%.meta}"

  # A tracked file is obviously fine.
  if git ls-files --error-unmatch "$base" >/dev/null 2>&1; then
    continue
  fi

  # Unity writes a .meta for every FOLDER too, and git cannot track a
  # directory - an empty one has no tracked file at all. A directory .meta is
  # legitimate whenever the directory exists on disk, or anything is tracked
  # beneath it.
  if [ -d "$base" ]; then
    continue
  fi
  if [ -n "$(git ls-files -- "$base/" 2>/dev/null)" ]; then
    continue
  fi

  orphan_meta="${orphan_meta}${m}"$'\n'
done < <(git ls-files 'Assets/*.meta' || true)

if [ -n "${orphan_meta//[$'\n']/}" ]; then
  problem "Orphaned .meta files with no matching asset:"
  echo "$orphan_meta" | while read -r f; do [ -n "$f" ] && note "$f"; done
else
  ok "No orphaned .meta files"
fi

# --- 6. Art and audio must be registered in ASSET-LICENSES.md -------------
unregistered=""
while read -r f; do
  [ -z "$f" ] && continue
  name=$(basename "$f")
  if ! grep -qF "$name" ASSET-LICENSES.md 2>/dev/null; then
    unregistered="${unregistered}${f}"$'\n'
  fi
done < <(git ls-files 'Assets/_Game/Art/*' 'Assets/_Game/Audio/*' \
                     'Assets/_Game/Textures/PBR/*' 'Assets/_Game/Textures/HDRI/*' \
           | grep -vE '\.meta$|\.gitkeep$' || true)

if [ -n "${unregistered//[$'\n']/}" ]; then
  problem "Art/audio files not listed in ASSET-LICENSES.md:"
  echo "$unregistered" | while read -r f; do [ -n "$f" ] && note "$f"; done
else
  ok "All third-party art, audio and textures are license-registered"
fi

echo
if [ "$fail" -ne 0 ]; then
  echo "Repository hygiene checks FAILED."
  exit 1
fi
echo "Repository hygiene checks passed."
