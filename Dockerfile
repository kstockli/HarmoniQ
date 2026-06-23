# Multi-Stage-Build für HarmoniQ (Blazor Server, .NET 10)
# Build-Kontext = Repo-Wurzel.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Zuerst nur die csproj kopieren → Restore-Layer wird gecacht.
COPY src/HarmoniQ.Web/HarmoniQ.Web.csproj src/HarmoniQ.Web/
RUN dotnet restore src/HarmoniQ.Web/HarmoniQ.Web.csproj

# Rest kopieren und veröffentlichen.
COPY . .
RUN dotnet publish src/HarmoniQ.Web/HarmoniQ.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# Npgsql versucht beim Verbinden eine GSSAPI/Kerberos-Aushandlung; die Bibliothek fehlt im
# schlanken Runtime-Image und muss nachinstalliert werden, sonst bricht der DB-Zugriff ab.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# ── Optional: JS-Rendering (Crawler C2, Playwright/Chromium) ──────────────────
# Standardmäßig AUS (Crawler:RenderingAktiv=false) → kein Browser nötig, schlankes Image.
# Zum Aktivieren in Produktion: RenderingAktiv=true setzen UND Chromium + Systemlibs installieren,
# z. B. (entkommentieren):
#   RUN apt-get update && apt-get install -y --no-install-recommends powershell \
#       && pwsh playwright.ps1 install --with-deps chromium \
#       && rm -rf /var/lib/apt/lists/*
# Alternativ als Basis-Image 'mcr.microsoft.com/playwright/dotnet:v1.60.0-noble' verwenden
# (Browser + Abhängigkeiten bereits enthalten).

# Railway gibt den Port via Umgebungsvariable PORT vor (Program.cs liest sie aus).
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "HarmoniQ.Web.dll"]
