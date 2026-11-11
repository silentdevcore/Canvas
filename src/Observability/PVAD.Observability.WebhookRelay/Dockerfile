FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PVAD_COMMIT=unknown
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
ARG PVAD_VERSION=1.1.0
ARG PVAD_COMMIT=unknown
ARG PVAD_BUILD_TIME=unknown

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
