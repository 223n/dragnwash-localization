# The checks and the translators' tools, in one image.
#
# Why: the checks that guard the translations run on Python 3.12, the plugin
# needs the .NET SDK 8, and the step every translator takes before committing -
# tools/hash-strings.ps1, which rewrites a working copy into the published,
# hashed form - is PowerShell. Asking each person for all three, on Windows,
# macOS and Linux alike, is the thing this image removes:
#
#   docker compose run --rm checks    what CI checks
#   docker compose run --rm hash ja   the hashing step, on any system
#
# GitHub Actions runs its jobs in the same image, so a check that passes here
# passes there.
#
# Ubuntu 24.04 ("noble"), because its python3 is 3.12, the version the
# workflows pin. The .NET SDK is 8, as setup-dotnet installs.
#
# What is NOT in this image, and never will be: the game's files. The plugin
# compiles against the game's assemblies, which live in the ignored libs/ and
# come with the mounted working tree; the translations themselves are text in
# this repository and carry no English from the game.
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble

# python3 is 3.12 here; python-is-python3 makes `python tools/...` work, which
# is how the documentation writes every command. git is for the checks that
# read the history, rsync for docker/tree.sh.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        python3 \
        python-is-python3 \
        git \
        curl \
        rsync \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# PowerShell, for tools/hash-strings.ps1 and tools/pack.ps1. It is installed
# from the .NET SDK that is already here (a global tool), which needs no
# Microsoft package feed and no architecture-specific download.
RUN dotnet tool install --global PowerShell --version 7.4.6 \
    && ln -s /root/.dotnet/tools/pwsh /usr/local/bin/pwsh
ENV PATH="/root/.dotnet/tools:${PATH}"

# The working tree is mounted from the host, so its files belong to another
# user than the one in the container. Without this, every git command inside
# refuses to run ("detected dubious ownership") and the checks that read the
# history fail for a reason that has nothing to do with the repository.
RUN git config --global --add safe.directory '*'

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# A tree that was already built on the host is copied here first and built
# there, so the host's bin/ and obj/ are left as they are (docker/tree.sh).
ENV COPY=/build

WORKDIR /work

CMD ["docker/checks.sh"]
