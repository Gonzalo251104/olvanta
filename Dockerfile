FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /source
COPY . .
RUN dotnet restore Olvanta.slnx --locked-mode
RUN dotnet publish src/Olvanta.Api -c Release --no-restore -o /out/api && \
    dotnet publish src/Olvanta.Worker -c Release --no-restore -o /out/worker

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS worker
WORKDIR /app
COPY --from=build /out/worker .
USER $APP_UID
ENTRYPOINT ["dotnet", "Olvanta.Worker.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS api
WORKDIR /app
COPY --from=build /out/api .
USER $APP_UID
ENTRYPOINT ["dotnet", "Olvanta.Api.dll"]
