# The checks and the hashing step in a container

[日本語](DOCKER.ja.md)

Translating needs no programming, but until now it did need PowerShell: the
step before committing, `tools/hash-strings.ps1`, rewrites your file into the
published, hashed form, and the checks that guard the translations run on
Python 3.12. The image in [`docker/Dockerfile`](../docker/Dockerfile) carries
both, so one command does it on Windows, macOS or Linux alike:

```bash
docker compose run --rm hash ja      # hash a language before committing
docker compose run --rm checks       # every check CI runs
docker compose run --rm shell        # a shell in the same image
```

The first run builds the image (a minute or two); after that it is cached. Your
working tree is mounted at `/work`, so the file the hashing step rewrites is
your file — read the result with `git diff` as usual.

GitHub Actions runs its jobs in the same image, so a check that passes here
passes there, for the same reason.

## What it does and does not cover

| | |
|---|---|
| **Covered** | `hash-strings.ps1` (through `hash`), `check-translations.py`, `check-game-files.py`, `linekeys.py --check`, `check-commits.py` |
| **Not covered** | the game, the in-game exports (F6/F7 and the working copy come from playing), the release zip (the Build workflow makes it), building the plugin — that needs the game's assemblies in the ignored `libs/` |

Nothing from the game is in the image, and nothing about your machine changes:
removing the image (`docker image rm dragnwash-localization-ci:local`) removes
every trace of it.

## Hashing a language

```bash
docker compose run --rm hash          # every locale, working copies included
docker compose run --rm hash ja       # one locale's published file
docker compose run --rm hash ja ko    # several
```

With no language it does what `pwsh tools/hash-strings.ps1` does: every locale
under `Translations/`, taking a locale's working copy
(`Translations/_discovered/<locale>.working.csv`) as the input when there is
one. Naming a language converts that published file itself.

It is the same conversion as the in-game **Hash for commit** button, so use
whichever is at hand.

## In GitHub Actions

The image is built and pushed to `ghcr.io/tomxv/dragnwash-localization-ci` by
[ci-image.yml](../.github/workflows/ci-image.yml) when anything in `docker/`
changes. The jobs in
[check-translations.yml](../.github/workflows/check-translations.yml) and
[commit-checker.yml](../.github/workflows/commit-checker.yml) run their checks
in it; [docker/ci-image.sh](../docker/ci-image.sh) builds the image from the
Dockerfile when it cannot be pulled, which is what happens for a pull request
from a fork — so a translator's pull request is checked exactly as a
maintainer's is, with no permission of any kind.

**Build** stays outside it: it packs the release on Windows with the private
reference assemblies.

The job names (`check`, `commits`) are unchanged, because the branch ruleset
requires those names.

## When something looks wrong

- **`docker compose` cannot reach the daemon.** Docker Desktop is not running,
  or its Linux engine is not started.
- **A check passes here and fails in Actions.** `docker compose build` after
  pulling rebuilds your image from the Dockerfile as it now is.
- **The hashing step changed more than you expected.** That is the published
  form: rows in play order, `#` section headers, and a row keyed by the hash of
  the English. [CONTRIBUTING.md](../CONTRIBUTING.md) describes it, and
  `git diff` shows exactly what moved.
