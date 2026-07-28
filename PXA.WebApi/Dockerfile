FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore PXA.WebApi/PXA.WebApi.csproj
RUN dotnet publish PXA.WebApi/PXA.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:SkipOcrWorkerPackaging=true
RUN dotnet publish src/Importing/PXA.FileImporter.ImageOcr.Worker/PXA.FileImporter.ImageOcr.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish/ocr-worker \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

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
