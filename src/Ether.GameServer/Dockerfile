# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Build.targets NuGet.config ./
COPY src/Ether.Domain/ src/Ether.Domain/
COPY src/Ether.Contracts/ src/Ether.Contracts/
COPY src/Ether.Application/ src/Ether.Application/
COPY src/Ether.Infrastructure/ src/Ether.Infrastructure/
COPY src/Ether.GameServer/ src/Ether.GameServer/

RUN dotnet publish src/Ether.GameServer/Ether.GameServer.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Ether.GameServer.dll"]
