# syntax=docker/dockerfile:1

# ── Build stage ───────────────────────────────────────────────
# The full SDK: it can restore, compile and publish. Only used to produce the output;
# it never ships in the final image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy just the project file first and restore. This layer is cached, so as long as the
# dependencies do not change, "docker build" skips restoring on every code change.
COPY CampaignSystem/CampaignSystem.csproj CampaignSystem/
RUN dotnet restore CampaignSystem/CampaignSystem.csproj

# Now the rest of the source, and publish a Release build to /app.
COPY CampaignSystem/ CampaignSystem/
RUN dotnet publish CampaignSystem/CampaignSystem.csproj -c Release -o /app /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────
# Just the ASP.NET runtime — no SDK, no source. Much smaller and less to attack.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Inside the container the app listens on 8080 (the container default). The host port is
# mapped in docker-compose, so this never clashes with anything on your machine.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CampaignSystem.dll"]
