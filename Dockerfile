# Multi-Stage-Build für HarmoniQ (Blazor Server, .NET 10)
# Build-Kontext = Repo-Wurzel.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Zuerst nur die csproj kopieren → Restore-Layer wird gecacht.
COPY src/HarmoniQ.Web/HarmoniQ.Web.csproj src/HarmoniQ.Web/
RUN dotnet restore src/HarmoniQ.Web/HarmoniQ.Web.csproj -r linux-x64

# Rest kopieren und SELF-CONTAINED veröffentlichen: die .NET-10-Runtime wird mitgebündelt, damit
# der Final-Stage (Playwright-Image) NICHT die passende .NET-Version mitbringen muss → robust.
COPY . .
RUN dotnet publish src/HarmoniQ.Web/HarmoniQ.Web.csproj -c Release -o /app/publish \
    -r linux-x64 --self-contained true /p:UseAppHost=true

# ── Final-Stage = offizielles Playwright-Image ────────────────────────────────
# Enthält Chromium + alle System-Libs passend zu Microsoft.Playwright 1.60.0 (Browser unter
# /ms-playwright, via PLAYWRIGHT_BROWSERS_PATH). Damit funktioniert das JS-Rendering (Crawler C2)
# – z. B. für die SPA-Seite https://www.emf26.ch/vereine, die ohne JS nur die Hülle liefert.
FROM mcr.microsoft.com/playwright/dotnet:v1.60.0-noble AS final
# Npgsql versucht beim Verbinden eine GSSAPI/Kerberos-Aushandlung; die Bibliothek nachinstallieren,
# sonst bricht der DB-Zugriff ab.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# JS-Rendering AN – der Browser ist in diesem Image vorhanden. (Überschreibbar via Railway-ENV.)
ENV Crawler__RenderingAktiv=true
# Railway gibt den Port via Umgebungsvariable PORT vor (Program.cs liest sie aus).
ENTRYPOINT ["./HarmoniQ.Web"]
