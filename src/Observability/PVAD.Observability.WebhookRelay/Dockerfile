FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PXA_COMMIT
ARG PVAD_COMMIT=${PXA_COMMIT:-unknown}
RUN test -z "$PXA_COMMIT" || test "$PXA_COMMIT" = "$PVAD_COMMIT" || \
    { echo "Conflicting PVAD_COMMIT and PXA_COMMIT build arguments" >&2; exit 1; }
WORKDIR /src
COPY VERSION Directory.Build.props ./
COPY src/Observability/PVAD.Observability.WebhookRelay/PVAD.Observability.WebhookRelay.csproj ./
RUN dotnet restore PVAD.Observability.WebhookRelay.csproj
COPY src/Observability/PVAD.Observability.WebhookRelay/ ./
RUN dotnet publish PVAD.Observability.WebhookRelay.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:PvadBuildMetadata=$PVAD_COMMIT

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG PXA_VERSION
ARG PVAD_VERSION=${PXA_VERSION:-1.1.0}
ARG PXA_COMMIT
ARG PVAD_COMMIT=${PXA_COMMIT:-unknown}
ARG PXA_BUILD_TIME
ARG PVAD_BUILD_TIME=${PXA_BUILD_TIME:-unknown}
RUN test -z "$PXA_VERSION" || test "$PXA_VERSION" = "$PVAD_VERSION" || \
    { echo "Conflicting PVAD_VERSION and PXA_VERSION build arguments" >&2; exit 1; }
RUN test -z "$PXA_COMMIT" || test "$PXA_COMMIT" = "$PVAD_COMMIT" || \
    { echo "Conflicting PVAD_COMMIT and PXA_COMMIT build arguments" >&2; exit 1; }
RUN test -z "$PXA_BUILD_TIME" || test "$PXA_BUILD_TIME" = "$PVAD_BUILD_TIME" || \
    { echo "Conflicting PVAD_BUILD_TIME and PXA_BUILD_TIME build arguments" >&2; exit 1; }

LABEL org.opencontainers.image.title="PVAD Observability Webhook Relay" \
      org.opencontainers.image.version="$PVAD_VERSION" \
      org.opencontainers.image.revision="$PVAD_COMMIT" \
      org.opencontainers.image.created="$PVAD_BUILD_TIME"

WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080 \
    PVAD_BUILD_COMMIT=$PVAD_COMMIT \
    PVAD_BUILD_TIME=$PVAD_BUILD_TIME
EXPOSE 8080
ENTRYPOINT ["dotnet", "PVAD.Observability.WebhookRelay.dll"]
