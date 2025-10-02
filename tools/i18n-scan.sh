#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   tools/i18n-scan.sh          -> scan entire repo
#   tools/i18n-scan.sh --staged -> scan staged files only (for pre-commit)

is_staged=0
if [[ "${1-}" == "--staged" ]]; then
  is_staged=1
fi

# Allow localized docs: *.sv.md
# Skip build/.git, and only scan these textual extensions
exclude_re='(^|/)(bin|obj|\.git)/|\.sv\.md$'
ext_re='\.((cs|md|json|yml|yaml|csproj|props|targets))$'

# Build file list (null-delimited for safety)
mapfile -d '' -t candidates < <(
  if [[ $is_staged -eq 1 ]]; then
    git diff --cached --name-only --diff-filter=ACMR -z
  else
    git ls-files -z
  fi
)

violations=0

for f in "${candidates[@]}"; do
  # Skip excluded paths and non-target extensions
  [[ "$f" =~ $exclude_re ]] && continue
  [[ "$f" =~ $ext_re ]] || continue

  # Respect .gitattributes binary marker; skip if binary
  if git check-attr --stdin --all < <(printf "%s\0" "$f") 2>/dev/null | grep -q 'binary: set'; then
    continue
  fi

  # Read staged or working tree content
  if [[ $is_staged -eq 1 ]]; then
    content="$(git show ":$f" 2>/dev/null || true)"
  else
    [[ -f "$f" ]] || continue
    content="$(cat "$f" 2>/dev/null || true)"
  fi

  # Look for Swedish characters
  if printf "%s" "$content" | grep -nE '[ÅÄÖåäö]'; then
    echo "^^ Found Swedish characters in: $f"
    echo
    violations=1
  fi
done

if [[ $violations -ne 0 ]]; then
  echo "I18N check failed: Swedish characters (å/ä/ö) found in non-localized files." >&2
  echo "Allowed localized files must end with .sv.md" >&2
  exit 1
fi

echo "I18N check OK."

