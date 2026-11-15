FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PVAD_COMMIT=unknown
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
ARG PVAD_VERSION=1.1.0
ARG PVAD_COMMIT=unknown
ARG PVAD_BUILD_TIME=unknown

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
