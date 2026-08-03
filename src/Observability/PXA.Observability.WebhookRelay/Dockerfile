FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PXA_COMMIT=unknown
WORKDIR /src
COPY VERSION Directory.Build.props ./
COPY src/Observability/PXA.Observability.WebhookRelay/PXA.Observability.WebhookRelay.csproj ./
RUN dotnet restore PXA.Observability.WebhookRelay.csproj
COPY src/Observability/PXA.Observability.WebhookRelay/ ./
RUN dotnet publish PXA.Observability.WebhookRelay.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:PxaBuildMetadata=$PXA_COMMIT

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG PXA_VERSION=1.1.0
ARG PXA_COMMIT=unknown
ARG PXA_BUILD_TIME=unknown

LABEL org.opencontainers.image.title="PXA Observability Webhook Relay" \
      org.opencontainers.image.version="$PXA_VERSION" \
      org.opencontainers.image.revision="$PXA_COMMIT" \
      org.opencontainers.image.created="$PXA_BUILD_TIME"

WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080 \
    PXA_BUILD_COMMIT=$PXA_COMMIT \
    PXA_BUILD_TIME=$PXA_BUILD_TIME
EXPOSE 8080
ENTRYPOINT ["dotnet", "PXA.Observability.WebhookRelay.dll"]
