FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

COPY backend.csproj ./
RUN dotnet restore

COPY . .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_USE_POLLING_FILE_WATCHER=1

EXPOSE 8080

CMD ["dotnet", "watch", "run", "--no-restore", "--no-launch-profile"]