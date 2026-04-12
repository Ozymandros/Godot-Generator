FROM mcr.microsoft.com/playwright/dotnet:v1.58.0-noble

# Ensure .NET 10 SDK is available (tests target net10.0).
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates \
    && wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends dotnet-sdk-10.0 \
    && dotnet --list-sdks \
    && rm -rf /var/lib/apt/lists/*

# Node.js + pnpm are required to run Electron scripts.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
      | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg \
    && echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_22.x nodistro main" \
      > /etc/apt/sources.list.d/nodesource.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends nodejs \
    && corepack enable \
    && corepack prepare pnpm@latest --activate \
    && node --version \
    && npm --version \
    && pnpm --version \
    && rm -rf /var/lib/apt/lists/*

# PowerShell (tools/verify.ps1 and local scripts).
RUN apt-get update \
    && apt-get install -y --no-install-recommends powershell \
    && rm -rf /var/lib/apt/lists/*
