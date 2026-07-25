# Project Overview

This repository is an ASP.NET Core + React multiplayer quiz platform. It serves a public catalog and purchase flow,
host account/auth flows, host-controlled live play over SignalR, player join/play screens, and post-game review/scoring.
The API also includes admin game authoring endpoints and serves the built SPA from `src/SeaBattlePaper.Api/wwwroot/`.

## Tech Stack

- Languages: C#, JavaScript
- Frameworks/Libraries: ASP.NET Core 10, EF Core, SignalR, React 18, React Router 6, Vite
- Data: PostgreSQL via Npgsql
- Auth/Payments: cookie auth, optional Google OAuth, Stripe
- Observability: OpenTelemetry for logs/traces
- Localization: request localization plus frontend i18n (`lt`, `en`)
- Tests: NUnit backend unit tests and integration tests with Testcontainers PostgreSQL
- Main entry points: `src/SeaBattlePaper.Api/Program.cs`, `src/client/src/main.jsx`,
  `test/SeaBattlePaper.IntegrationTests/SeaBattlePaper.IntegrationTests.csproj`

## Directory Map

- `src/SeaBattlePaper.Api/` - API bootstrap, controllers, SignalR hubs, auth/cors/localization/observability configuration
- `src/SeaBattlePaper.Application/` - application services, `Result<T>`, mappings, validation, business rules
- `src/SeaBattlePaper.Domain/` - entities and enums for games, plays, answers, purchases, reviews, users
- `src/SeaBattlePaper.Infrastructure/` - EF Core context, repositories, migrations, Stripe/vault integration, seed data
- `src/SeaBattlePaper.Contracts/` - shared immutable DTOs for HTTP and SignalR payloads
- `src/client/` - React SPA for catalog, checkout, host setup/history/review, player join/lobby/live/podium flows
- `test/SeaBattlePaper.IntegrationTests/` - API + SignalR integration tests with PostgreSQL Testcontainers
- `test/SeaBattlePaper.UnitTests/` - backend unit tests

Focus most implementation work on `src/SeaBattlePaper.Api/`, `src/SeaBattlePaper.Application/`, `src/SeaBattlePaper.Domain/`,
`src/SeaBattlePaper.Infrastructure/`, and `src/client/`.
Treat `src/SeaBattlePaper.Contracts/` as a shared surface: confirm before changing contracts that affect both backend and frontend.

## How to Run Checks

- Run all backend tests: `dotnet test SeaBattlePaper.sln`
- Run integration tests only: `dotnet test test/SeaBattlePaper.IntegrationTests/SeaBattlePaper.IntegrationTests.csproj`
- Run unit tests only: `dotnet test test/SeaBattlePaper.UnitTests/SeaBattlePaper.UnitTests.csproj`
- Run frontend unit tests: `npm run test:unit --prefix src/client`
- Build frontend: `npm run build --prefix src/client`
- Lint/format: no dedicated repo-wide lint or formatter command is configured

## Coding Standards & Patterns

- `Directory.Build.props` enforces `net10.0`, nullable enabled, implicit usings, analyzers, and warnings-as-errors.
- Use file-scoped namespaces and PascalCase for C# types.
- Application services use constructor injection and return `Result<T>` for success/failure.
- Domain entities are mutable POCOs; DTOs are immutable records in `src/SeaBattlePaper.Contracts/`.
- Localized strings use `LocalizedText` in domain/storage and are projected using the active UI culture.
- Error handling goes through `Result<T>.Failure(code, message)` and `src/SeaBattlePaper.Api/Common/ApiErrorHandler.cs`.
- SignalR hub surface is split across partial `PlayHub` files in `src/SeaBattlePaper.Api/Hubs/`.

## API Surface

### HTTP (REST)

- Auth: `POST /api/auth/login`, `POST /api/auth/register`, `POST /api/auth/logout`, `GET /api/auth/google`, `GET /api/auth/me`, `POST /api/auth/update-password`
- Public games: `GET /api/games`, `GET /api/games/{id}`
- Admin games: `POST /api/admin/games`, `PUT /api/admin/games/{gameId}`, `POST /api/admin/games/{gameId}/questions`,
  `PUT /api/admin/games/{gameId}/questions/{questionId}`, `DELETE /api/admin/games/{gameId}/questions/{questionId}`
- Reviews: `GET /api/games/{gameId}/reviews`, `GET /api/games/{gameId}/reviews/me`, `POST /api/reviews`, `PUT /api/reviews/{reviewId}`,
  `DELETE /api/reviews/{reviewId}`
- Cart: `GET /api/me/cart`, `PUT /api/me/cart`
- Payments: `POST /api/payments/checkout`, `GET /api/payments/checkout/verify`
- Purchases: `GET /api/purchases`
- Plays: `POST /api/plays`, `GET /api/plays/{playId}/validate`, `GET /api/plays`, `GET /api/plays/{playId}/review`,
  `GET /api/plays/{playId}/scores`, `PATCH /api/plays/{playId}/scores`
- Stripe webhook: `POST /api/stripe/webhook`

### SignalR

- Hub: `/hubs/play`
- Host actions: `HostAttach`, `HostStartPlay`, `HostNextQuestion`, `HostPreviousQuestion`,
  `HostCompletePlayAndMoveToResultsState`, `HostNextAnswer`, `HostPreviousAnswer`, `HostMoveToPodium`, `HostEndPlay`,
  `HostKickPlayer`
- Player actions: `PlayerJoin`, `PlayerLeave`, `SubmitAnswer`, `GetAnswersLive`
- Events: `StateUpdated`, `AnswersUpdated`, `JoinAccepted`, `JoinRejected`, `PlayerKicked`, `Error`

## Domain Rules

- Plays use generated 6-character `playId` values; uniqueness is retried up to 20 times.
- Implemented play state flow is `Lobby -> Started -> PreAnswers -> Answers -> Results -> Podium -> Ended`.
- `Leaderboard` exists in the shared enum/contracts but is not currently assigned by the application service flow.
- Hosts can move question order forward/backward while the play is in `Started`.
- Play creation requires a purchase for the selected game; `UsedPlays` increments when a play is created.
- Checkout uses the current cart; successful verification creates purchases and clears the cart.
- Joining uses `playId`, nickname, and optional rejoin key; authenticated users can rejoin by `UserId`, anonymous users by `RejoinKey`.
- Kicking a player removes them from the play and disconnects their tracked SignalR connection.
- Answers are accepted only while the play is in `Started`, only once per player/question, and only for the active question.
- Two-choice and four-choice answers are auto-scored immediately.
- Free-text questions are not auto-scored; missing free-text answer rows are synthesized during review/scoring so hosts can score every player/question pair.
- Flashcard/open-conversation style content is modeled as `GameKind.Flashcards` with `QuestionType.OpenConversation`.
- Key play timestamps are UTC: `StartedAtUtc`, `EndedAtUtc`, `QuestionStartAtUtc`, `ActivePhaseStartedAtUtc`, `ActivePhaseEndedAtUtc`.
- Per-play duration uses `QuestionDurationSeconds`; creation caps the configured value at 120 seconds.

## Safety and Boundaries

- Do not edit generated frontend build outputs under `src/SeaBattlePaper.Api/wwwroot/`.
- Avoid adding new dependencies without confirmation.
- Avoid changing EF migrations unless the user explicitly asks for schema work.
- Do not change `SeaBattlePaper.sln.DotSettings.user` unless explicitly requested.
- If a change would require updating both contracts and client/server consumers, stop and confirm first.

## Agent Lifecycle

- Reuse an existing agent when follow-up requests stay in the same area and are iterative, for example several UI tweaks on the same screen.
- Do not restart investigation from scratch for small follow-up changes when an existing agent already has the relevant local context.
- Close agents as soon as they are no longer useful for the active area of work.
- If an agent is not reused within the next 2 user requests, close it instead of leaving it idle.
- If the task shifts to a different area of the codebase, close the old agent and start a fresh one when needed.
- Prefer a fresh agent over a stale one when the old context is likely to add noise or reflect outdated code.

## When to Ask Clarifying Questions

- The request implies new or changed API contracts in `src/SeaBattlePaper.Contracts/`.
- The change affects purchase gating, Stripe checkout, authentication, scoring, or play/session state flow.
- The change needs both backend and frontend updates but the user mentioned only one side.
- You need a new dependency, database migration, or external service/configuration change.
- The requested behavior conflicts with current play states, join/rejoin rules, or existing tests.

Always run required agents and subagents and use skills you think could help WITHOUT me specifying it. I am giving you permission
If I ask to implement anything and code, PROBABLY you can start from project-development-agent.toml. This is mainly orchestration related agent.
