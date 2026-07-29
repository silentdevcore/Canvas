FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PXA_COMMIT=unknown
WORKDIR /src

COPY . .
RUN dotnet restore PXA.WebApi/PXA.WebApi.csproj
RUN dotnet publish PXA.WebApi/PXA.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:PxaBuildMetadata=$PXA_COMMIT \
    /p:SkipOcrWorkerPackaging=true
RUN dotnet publish src/Importing/PXA.FileImporter.ImageOcr.Worker/PXA.FileImporter.ImageOcr.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish/ocr-worker \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG PXA_VERSION=1.0.0
ARG PXA_COMMIT=unknown
ARG PXA_BUILD_TIME=unknown

LABEL org.opencontainers.image.title="PXA WebApi" \
      org.opencontainers.image.version="$PXA_VERSION" \
      org.opencontainers.image.revision="$PXA_COMMIT" \
      org.opencontainers.image.created="$PXA_BUILD_TIME"

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        fonts-dejavu-core \
        tesseract-ocr \
        tesseract-ocr-deu \
        tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

RUN mkdir -p \
        /app/data/data-protection-keys \
        /app/data/objects \
        /app/data/pdf-viewer \
        /app/fonts \
        /app/tmp \
    && chown -R "$APP_UID:$APP_UID" /app/data /app/fonts /app/tmp

ENV ASPNETCORE_HTTP_PORTS=8080 \
    PXA_BUILD_COMMIT=$PXA_COMMIT \
    PXA_BUILD_TIME=$PXA_BUILD_TIME \
    DOTNET_EnableDiagnostics=0 \
    TMPDIR=/app/tmp \
    DataProtection__KeysDirectory=/app/data/data-protection-keys \
    Storage__RootPath=/app/data/objects \
    PdfViewer__AnnotationStoragePath=/app/data/pdf-viewer \
    Pdf__FontsDirectory=/app/fonts \
    Ocr__UseIsolatedWorker=true \
    Ocr__WorkerPath=/app/ocr-worker/PXA.FileImporter.ImageOcr.Worker.dll \
    Ocr__TessDataPath=/usr/share/tesseract-ocr/5/tessdata

EXPOSE 8080
USER $APP_UID

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD ["dotnet", "PXA.WebApi.dll", "--health-check"]

ENTRYPOINT ["dotnet", "PXA.WebApi.dll"]
