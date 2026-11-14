FROM node:22-bookworm-slim AS node
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet
FROM mcr.microsoft.com/playwright:v1.64.1-noble

COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && ln -sf ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -sf ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx \
    && apt-get update \
    && apt-get install -y --no-install-recommends imagemagick poppler-utils fonts-liberation libicu74 libfontconfig1 libfreetype6 libharfbuzz0b \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_PROCESSOR_COUNT=2 \
    NUGET_XMLDOC_MODE=skip \
    HOME=/tmp/pvad-linux-home
COPY run.sh /usr/local/bin/pvad-linux-parity
USER pwuser
WORKDIR /tmp
ENTRYPOINT ["bash", "/usr/local/bin/pvad-linux-parity"]
