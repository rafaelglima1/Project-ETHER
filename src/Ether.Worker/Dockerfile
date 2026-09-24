# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Build.targets NuGet.config ./
COPY content/ content/
COPY src/Ether.Domain/ src/Ether.Domain/
COPY src/Ether.Contracts/ src/Ether.Contracts/
COPY src/Ether.Application/ src/Ether.Application/
COPY src/Ether.Infrastructure/ src/Ether.Infrastructure/
COPY src/Ether.Worker/ src/Ether.Worker/

RUN dotnet publish src/Ether.Worker/Ether.Worker.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# procps provides pgrep for the health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends procps \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Ether.Worker.dll"]
