#!/usr/bin/env bash

# Keeps the Spanish pages honest.
#
# Every translated page is recorded in es/translations.json against the hash of the English page
# it was written from. When the English page changes, the hash stops matching and this fails, so
# the translation cannot quietly drift into describing an older library.
#
# Dates would have been easier and would not have worked: a merge brings in commits older than
# the translation, and the translation would still be wrong.
#
#   ./website/check-translations.sh            check, and fail if anything is stale
#   ./website/check-translations.sh --update   record the English pages as they are now
#
# Run the update only once you have read the change and the Spanish page says the same thing.
# A page with no translation is a warning, not a failure: a reader who follows the switch that
# is not there simply stays in English.

set -euo pipefail

cd "$(dirname "$0")/.."

manifest="website/es/translations.json"
update=false
if [ "${1:-}" = "--update" ]; then
  update=true
fi

# What the English page hashes to, normalised the way git stores it, so a checkout on Windows
# and a checkout on Linux agree.
english_hash() {
  git hash-object --path="$1" "$1"
}

pages=$(git ls-files 'website/*.md' ':!website/es/*' | sed 's|^website/||')

if [ "$update" = true ]; then
  {
    echo "{"
    first=true
    for page in $pages; do
      [ -f "website/es/$page" ] || continue
      [ "$first" = true ] || echo ","
      first=false
      printf '  "%s": "%s"' "$page" "$(english_hash "website/$page")"
    done
    echo
    echo "}"
  } > "$manifest"
  echo "Recorded $(grep -c '": "' "$manifest") translated page(s) in $manifest."
  exit 0
fi

if [ ! -f "$manifest" ]; then
  echo "$manifest is missing. Run ./website/check-translations.sh --update." >&2
  exit 1
fi

status=0

for page in $pages; do
  spanish="website/es/$page"
  recorded=$(sed -n "s|^  \"$page\": \"\([0-9a-f]*\)\".*|\1|p" "$manifest")

  if [ ! -f "$spanish" ]; then
    echo "::warning file=website/$page::No Spanish translation at $spanish. Readers get the English page."
    if [ -n "$recorded" ]; then
      echo "::error file=$manifest::$manifest still lists $page, but $spanish is gone." >&2
      status=1
    fi
    continue
  fi

  if [ -z "$recorded" ]; then
    echo "::error file=$manifest::$spanish exists but $page is not in $manifest. Run ./website/check-translations.sh --update." >&2
    status=1
    continue
  fi

  current=$(english_hash "website/$page")
  if [ "$recorded" != "$current" ]; then
    echo "::error file=website/$page::website/$page changed after $spanish was translated. Update the Spanish page, then run ./website/check-translations.sh --update." >&2
    status=1
  fi
done

if [ "$status" -eq 0 ]; then
  echo "Every translated page matches the English it was written from."
fi

exit $status
