#!/usr/bin/env bash
# Secret scanner. Checks the working tree, the staged index, and optionally
# EVERY commit in history - because a secret removed in a later commit is still
# public forever once the repo is pushed.
#
#   ./scripts/scan-secrets.sh            # working tree + index
#   ./scripts/scan-secrets.sh --history  # also every commit ever made
set -uo pipefail

fail=0
# fail is a COUNT, not a flag: each section compares the count before and
# after itself to decide whether to print its own ok line. A flag would
# make every later section report ok once anything had failed.
problem () { printf '\033[31mFAIL\033[0m %s\n' "$1"; fail=$((fail + 1)); }
ok ()      { printf '\033[32m ok \033[0m %s\n' "$1"; }
note ()    { printf '       %s\n' "$1"; }

# Filenames that must never be committed, whatever they contain.
# Unity logs are included because Unity writes its launch command line -
# -accessToken and -hubSessionId included - into them, one argument per
# line. A multi-line secret cannot be regex-matched without false
# positives on every GUID and base64 blob, so the log files themselves are
# forbidden by name instead.
FORBIDDEN_PATHS='(^|/)(\.env(\..*)?|.*\.keystore|.*\.jks|.*\.ulf|.*\.p12|.*\.pfx|.*\.pem|.*\.key|.*\.mobileprovision|id_rsa|id_ed25519|credentials\.json|serviceAccount.*\.json|\.npmrc|\.pypirc|settings\.local\.json|Editor\.log|Editor-prev\.log|upm\.log|AssetImportWorker.*\.log)$'

# High-confidence secret VALUES. Deliberately strict: a reference such as
# "${{ secrets.UNITY_PASSWORD }}" is not a secret and must not trip this.
read -r -d '' PATTERNS <<'PAT' || true
-----BEGIN [A-Z ]*PRIVATE KEY-----
AKIA[0-9A-Z]{16}
ASIA[0-9A-Z]{16}
gh[pousr]_[A-Za-z0-9]{36,}
github_pat_[A-Za-z0-9_]{70,}
AIza[0-9A-Za-z_\-]{35}
xox[baprs]-[0-9A-Za-z-]{10,}
sk-[A-Za-z0-9]{32,}
sk-ant-[A-Za-z0-9_\-]{20,}
eyJ[A-Za-z0-9_\-]{15,}\.eyJ[A-Za-z0-9_\-]{15,}\.
glpat-[A-Za-z0-9_\-]{20,}
AccountKey=[A-Za-z0-9+/]{60,}
(password|passwd|secret|api_?key|auth_?token|access_?token)[\"'\'' ]*[:=][\"'\'' ]*[A-Za-z0-9+/_\-]{12,}
<License id=
DeveloperData
-accessToken[ =][A-Za-z0-9_.\-]{20,}
-cbmEditorLicense[ =][0-9a-f]{20,}
-hubSessionId[ =][0-9a-f]{8}-[0-9a-f]{4}
PAT

# Paths whose matches are documentation about secrets, not secrets.
is_allowlisted () {
  case "$1" in
    scripts/scan-secrets.sh|docs/SETUP.md|SECURITY.md|CLAUDE.md|\
    .github/workflows/*|.githooks/*|CONTRIBUTING.md|.gitignore|\
    .claude/commands/hygiene.md) return 0 ;;
    *) return 1 ;;
  esac
}

# A binary file cannot contain a readable credential, and regex-scanning
# megabytes of JPEG costs far more than it finds. Adding the first texture sets
# took this scanner from under a second to fifty, which a pre-commit hook
# cannot afford.
is_binary () {
  case "$1" in
    *.jpg|*.jpeg|*.png|*.hdr|*.exr|*.tga|*.psd|*.tif|*.fbx|*.blend|*.obj|\
    *.glb|*.gltf|*.wav|*.ogg|*.mp3|*.mp4|*.ttf|*.otf|*.dll|*.so|*.dylib)
      return 0 ;;
  esac
  # Anything routed through LFS is binary by this repository's own definition.
  if git check-attr filter -- "$1" 2>/dev/null | grep -q 'filter: lfs'; then
    return 0
  fi
  return 1
}

# All patterns in one file, so each blob needs ONE grep instead of one per
# pattern. With ~60 files and 16 patterns that is 60 greps rather than 960,
# which took the scan from 29 seconds to under two.
PATTERN_FILE=$(mktemp -t apexline-pat)
trap 'rm -f "$PATTERN_FILE"' EXIT
printf '%s\n' "$PATTERNS" > "$PATTERN_FILE"

scan_blob () {           # $1 = label, $2 = path, content on stdin
  local label="$1" path="$2" content
  content=$(cat)
  is_allowlisted "$path" && return 0
  is_binary "$path" && return 0

  # Fast path: one grep for every pattern at once. Most files match nothing,
  # so this is the only cost they pay.
  if ! printf '%s' "$content" | grep -qE -f "$PATTERN_FILE" 2>/dev/null; then
    return 0
  fi

  # Something matched, so now it is worth finding out WHICH pattern, to give a
  # useful message. -e is mandatory: a pattern beginning with "-" (such as
  # -accessToken) is otherwise parsed by grep as an option and never matches.
  while IFS= read -r pat; do
    [ -z "$pat" ] && continue
    if printf '%s' "$content" | grep -qE -e "$pat" 2>/dev/null; then
      problem "$label: $path matches secret pattern /$pat/"
    fi
  done <<< "$PATTERNS"
}

echo "--- 1. Forbidden filenames (tracked now) ---"
hits=$(git ls-files | grep -iE "$FORBIDDEN_PATHS" || true)
if [ -n "$hits" ]; then
  problem "Credential-like files are tracked:"
  echo "$hits" | while read -r f; do note "$f"; done
else
  ok "No credential filenames tracked"
fi

echo "--- 2. Secret values in tracked files ---"
before=$fail
while IFS= read -r f; do
  [ -z "$f" ] && continue
  [ -f "$f" ] || continue
  is_binary "$f" && continue
  scan_blob "working tree" "$f" < "$f"
done < <(git ls-files)
[ "$fail" -eq "$before" ] && ok "No secret values in tracked files"

echo "--- 3. Staged changes ---"
before=$fail
while IFS= read -r f; do
  [ -z "$f" ] && continue
  git show ":$f" 2>/dev/null | scan_blob "staged" "$f"
done < <(git diff --cached --name-only --diff-filter=ACM 2>/dev/null)
[ "$fail" -eq "$before" ] && ok "Nothing problematic staged"

if [ "${1:-}" = "--history" ]; then
  echo "--- 4. Forbidden filenames anywhere in history ---"
  hits=$(git log --all --pretty=format: --name-only 2>/dev/null \
         | sort -u | grep -ivE '^$' | grep -iE "$FORBIDDEN_PATHS" || true)
  if [ -n "$hits" ]; then
    problem "Credential filenames exist in history (rewrite required before pushing):"
    echo "$hits" | while read -r f; do note "$f"; done
  else
    ok "No credential filenames in any commit"
  fi

  echo "--- 5. Secret values anywhere in history ---"
  before=$fail

  # Collected into an array rather than left unquoted: word splitting on
  # $(git rev-list --all) happens to work, but breaks silently the moment a ref
  # contains whitespace. Built with a read loop, not mapfile, so this still
  # runs on the bash 3.2 that ships with macOS.
  revs=()
  while IFS= read -r r; do
    [ -n "$r" ] && revs+=("$r")
  done < <(git rev-list --all 2>/dev/null)

  if [ "${#revs[@]}" -eq 0 ]; then
    ok "No commits to scan"
  else
    while IFS= read -r pat; do
      [ -z "$pat" ] && continue
      out=$(git grep -I -n -E -e "$pat" "${revs[@]}" -- 2>/dev/null \
            | grep -vE ':(scripts/scan-secrets\.sh|docs/SETUP\.md|SECURITY\.md|CLAUDE\.md|CONTRIBUTING\.md|docs/ALLOWED\.md|docs/WORKFLOW\.md|README\.md|\.gitignore|\.github/workflows/|\.githooks/|\.claude/)' || true)
      if [ -n "$out" ]; then
        problem "History matches /$pat/:"
        echo "$out" | head -5 | while read -r l; do note "$l"; done
      fi
    done <<< "$PATTERNS"
    [ "$fail" -eq "$before" ] && ok "No secret values in any commit"
  fi
fi

echo
if [ "$fail" -ne 0 ]; then
  echo "SECRET SCAN FAILED - do not push."
  echo "A secret already committed stays in history. Rewrite it out"
  echo "(git filter-repo) and rotate the credential; deleting the file is not enough."
  exit 1
fi
echo "Secret scan passed."
