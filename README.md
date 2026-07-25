# Sea Battle Paper

Sea Battle Paper is a standalone, real-time two-player battleship web game. The project is being rebuilt from a copied KartuOn application while keeping the
same React, ASP.NET Core, PostgreSQL, SignalR, and clean-architecture foundation.

Gate 1 intentionally contains only a minimal baseline:

- ASP.NET Core static SPA host with a `/health` endpoint
- external OpenTelemetry export under service name `SeaBattlePaper.Api`
- React/Vite placeholder shell
- empty Application, Domain, Infrastructure, and Contracts project shells
- Docker Compose services for the app, PostgreSQL, and Caddy

The original mobile application remains unchanged under `OLD/` as a visual and behavior reference.

## Build

```powershell
dotnet build SeaBattlePaper.sln -c Release
npm ci --prefix src/client
npm run build --prefix src/client
```

## Container deployment

Copy `.env.example` to `.env`, replace the development password, then pull or build `ghcr.io/valentk777/sea-battle-paper-web`. Caddy exposes the application at
`http://localhost:8080`; an external proxy can route `kartuon.click/sea_battle_paper` to that root endpoint.

```powershell
docker compose config
docker compose up -d
```

The app publishes telemetry to the external OTLP endpoint configured by `OPEN_TELEMETRY_OTLP_ENDPOINT`; no Grafana, Loki, Tempo, or collector is bundled.

## Database

```powershell
dotnet ef migrations add CreateInitial --startup-project src/SeaBattlePaper.Api --project src/SeaBattlePaper.Infrastructure --context  SeaBattleDbContext
dotnet ef database update --startup-project src/SeaBattlePaper.Api --project src/SeaBattlePaper.Infrastructure --context SeaBattleDbContext
```
