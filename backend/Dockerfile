FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src
COPY backend.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish --configuration Release --no-restore --no-self-contained --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

RUN apk add --no-cache krb5-libs
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
        DOTNET_EnableDiagnostics=0

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "backend.dll"]