FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Observability/PXA.Observability.WebhookRelay/PXA.Observability.WebhookRelay.csproj ./
RUN dotnet restore PXA.Observability.WebhookRelay.csproj
COPY src/Observability/PXA.Observability.WebhookRelay/ ./
RUN dotnet publish PXA.Observability.WebhookRelay.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PXA.Observability.WebhookRelay.dll"]
