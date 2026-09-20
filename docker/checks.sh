#!/usr/bin/env bash
# Every check CI runs, in one go, in the container image (docker/Dockerfile).
#
#   docker compose run --rm checks
#
# Each check is the command the workflow runs, so a pass here is a pass there.
# The script stops at the first failure; --keep-going runs the rest anyway and
# reports every reason at once.
set -u

KEEP_GOING=0
[ "${1:-}" = "--keep-going" ] && KEEP_GOING=1

failed=0

run() {
    title=$1
    shift
    printf '\n\033[1m== %s ==\033[0m\n' "$title"
    if "$@"; then
        return 0
    fi
    printf '\033[31mFAILED: %s\033[0m\n' "$title"
    failed=$((failed + 1))
    [ "$KEEP_GOING" = "1" ] || exit 1
}

# The published files: keys, rows, tags, the order, the languages.
run "The published strings.csv files" \
    python tools/check-translations.py

# The repository carries no English from the game: every file is compared with
# the fingerprints of real game installs.
run "No file matches a game install's" \
    python tools/check-game-files.py

run "Line keys match the vectors" \
    python tools/linekeys.py --check

# The range the commit checker walks: what a push or a pull request brings.
# The script's own default (what is not yet on origin/main) is the right one
# for a working tree; BASE and HEAD override it the way the workflow does.
if [ -n "${BASE:-}" ] && [ -n "${HEAD:-}" ]; then
    run "No tool's attribution in the commit messages" \
        python tools/check-commits.py "$BASE" "$HEAD"
else
    run "No tool's attribution in the commit messages" \
        python tools/check-commits.py
fi

printf '\n'
if [ "$failed" -gt 0 ]; then
    printf '\033[31m%d check(s) failed.\033[0m\n' "$failed"
    exit 1
fi
printf '\033[32mEvery check passed.\033[0m\n'
