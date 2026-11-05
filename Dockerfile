# ── Build Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY ["src/TransactionAggregator.Api/TransactionAggregator.Api.csproj", "src/TransactionAggregator.Api/"]
COPY ["src/TransactionAggregator.Application/TransactionAggregator.Application.csproj", "src/TransactionAggregator.Application/"]
COPY ["src/TransactionAggregator.Domain/TransactionAggregator.Domain.csproj", "src/TransactionAggregator.Domain/"]
COPY ["src/TransactionAggregator.Infrastructure/TransactionAggregator.Infrastructure.csproj", "src/TransactionAggregator.Infrastructure/"]

RUN dotnet restore "src/TransactionAggregator.Api/TransactionAggregator.Api.csproj"

# Copy all source
COPY . .

RUN dotnet build "src/TransactionAggregator.Api/TransactionAggregator.Api.csproj" \
    -c Release -o /app/build --no-restore

# ── Publish Stage ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "src/TransactionAggregator.Api/TransactionAggregator.Api.csproj" \
    -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for healthchecks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Security: run as non-root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

# Create log directory with correct permissions
RUN mkdir -p /app/logs && chown appuser:appgroup /app/logs
RUN mkdir -p /app/data/imports && chown appuser:appgroup /app/data/imports

COPY --from=publish --chown=appuser:appgroup /app/publish .

# Ensure import directory exists even if the compose bind-mount is absent
RUN mkdir -p /app/data/imports && chown -R appuser:appgroup /app/data

USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker

ENTRYPOINT ["dotnet", "TransactionAggregator.Api.dll"]
