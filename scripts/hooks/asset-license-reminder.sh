#!/usr/bin/env bash
# PostToolUse hook: fires after a Write/Edit under Assets/_Game/Art or
# Assets/_Game/Audio and reminds about the two things that are easy to forget
# and expensive to discover later - the .meta file and the licence row.
#
# Reads the hook payload on stdin. Prints hook JSON only when it has something
# to say, and always exits 0 so it can never block an edit.
set -uo pipefail

payload=$(cat 2>/dev/null) || exit 0
[ -n "$payload" ] || exit 0

file=$(printf '%s' "$payload" | jq -r '
  .tool_input.file_path // .tool_response.filePath // empty
' 2>/dev/null) || exit 0

case "$file" in
  *Assets/_Game/Art/*|*Assets/_Game/Audio/*) ;;
  *) exit 0 ;;
esac

name=$(basename "$file")

# A .meta file itself needs no reminder about its own .meta.
case "$name" in
  *.meta) exit 0 ;;
esac

notes=""

if [ ! -f "${file}.meta" ]; then
  notes="${notes}- No ${name}.meta exists yet. Unity writes it on next focus; commit it WITH the asset, or Unity regenerates the GUID later and silently breaks every reference to this asset for everyone else.
"
fi

if [ -f ASSET-LICENSES.md ] && ! grep -qF "$name" ASSET-LICENSES.md 2>/dev/null; then
  notes="${notes}- ${name} is not listed in ASSET-LICENSES.md. Add a row with author, real source URL and licence. CC0 or CC-BY only; never Unity Asset Store content. CI fails on unregistered art and audio.
"
fi

[ -n "$notes" ] || exit 0

jq -n --arg ctx "Asset checklist for ${file}:
${notes}" '{
  hookSpecificOutput: {
    hookEventName: "PostToolUse",
    additionalContext: $ctx
  }
}' 2>/dev/null || true

exit 0
