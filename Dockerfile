# ── Build Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/LinnworksMcp/LinnworksMcp.csproj", "src/LinnworksMcp/"]
RUN dotnet restore "src/LinnworksMcp/LinnworksMcp.csproj"

# Copy full source and publish release build
COPY . .
WORKDIR "/src/src/LinnworksMcp"
RUN dotnet publish "LinnworksMcp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime Stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for docker healthchecks
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Run as non-root user for container security
USER app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "LinnworksMcp.dll"]
