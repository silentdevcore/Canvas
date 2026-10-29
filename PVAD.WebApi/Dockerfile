FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PXA_COMMIT
ARG PVAD_COMMIT=${PXA_COMMIT:-unknown}
RUN test -z "$PXA_COMMIT" || test "$PXA_COMMIT" = "$PVAD_COMMIT" || \
    { echo "Conflicting PVAD_COMMIT and PXA_COMMIT build arguments" >&2; exit 1; }
WORKDIR /src

COPY . .
RUN dotnet restore PVAD.WebApi/PVAD.WebApi.csproj
RUN dotnet publish PVAD.WebApi/PVAD.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:PvadBuildMetadata=$PVAD_COMMIT \
    /p:SkipOcrWorkerPackaging=true
RUN dotnet publish src/Importing/PVAD.FileImporter.ImageOcr.Worker/PVAD.FileImporter.ImageOcr.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish/ocr-worker \
    /p:PvadBuildMetadata=$PVAD_COMMIT \
    /p:UseAppHost=false
RUN dotnet publish src/Execution/PVAD.CodeWorker/PVAD.CodeWorker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish/code-worker \
    /p:PvadBuildMetadata=$PVAD_COMMIT \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
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

LABEL org.opencontainers.image.title="PVAD WebApi" \
      org.opencontainers.image.version="$PVAD_VERSION" \
      org.opencontainers.image.revision="$PVAD_COMMIT" \
      org.opencontainers.image.created="$PVAD_BUILD_TIME"

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        fonts-dejavu-core \
        liblcms2-2 \
        tesseract-ocr \
        tesseract-ocr-deu \
        tesseract-ocr-eng \
        zlib1g \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

# Tesseract 5.2 uses "x64" for its 64-bit search path, including arm64.
RUN case "$(dpkg --print-architecture)" in \
        amd64) native_dir=/usr/lib/x86_64-linux-gnu ;; \
        arm64) native_dir=/usr/lib/aarch64-linux-gnu ;; \
        *) echo "Unsupported OCR container architecture" >&2; exit 1 ;; \
    esac \
    && mkdir -p /app/ocr-worker/native/x64 \
    && ln -s "$native_dir/liblept.so.5" /app/ocr-worker/native/x64/libleptonica-1.82.0.so \
    && ln -s "$native_dir/libtesseract.so.5" /app/ocr-worker/native/x64/libtesseract50.so \
    && ln -s "$native_dir/libdl.so.2" /app/ocr-worker/libdl.so \
    && test -r /app/ocr-worker/native/x64/libleptonica-1.82.0.so \
    && test -r /app/ocr-worker/native/x64/libtesseract50.so \
    && test -r /app/ocr-worker/libdl.so

RUN mkdir -p \
        /app/data/data-protection-keys \
        /app/data/objects \
        /app/data/pdf-viewer \
        /app/fonts \
        /app/tmp \
    && chown -R "$APP_UID:$APP_UID" /app/data /app/fonts /app/tmp

ENV ASPNETCORE_HTTP_PORTS=8080 \
    PVAD_BUILD_COMMIT=$PVAD_COMMIT \
    PVAD_BUILD_TIME=$PVAD_BUILD_TIME \
    DOTNET_EnableDiagnostics=0 \
    TMPDIR=/app/tmp \
    DataProtection__KeysDirectory=/app/data/data-protection-keys \
    Storage__RootPath=/app/data/objects \
    PdfViewer__AnnotationStoragePath=/app/data/pdf-viewer \
    Pdf__FontsDirectory=/app/fonts \
    PdfTrust__RevocationEvidence__CacheDirectory=/app/data/pdf-trust/revocation-cache \
    Ocr__UseIsolatedWorker=true \
    Ocr__WorkerPath=/app/ocr-worker/PVAD.FileImporter.ImageOcr.Worker.dll \
    Ocr__TessDataPath=/usr/share/tesseract-ocr/5/tessdata \
    CodeWorker__Enabled=false \
    CodeWorker__Hardened=false \
    CodeWorker__WorkerPath=/app/code-worker

EXPOSE 8080
USER $APP_UID

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD ["dotnet", "PVAD.WebApi.dll", "--health-check"]

ENTRYPOINT ["dotnet", "PVAD.WebApi.dll"]
