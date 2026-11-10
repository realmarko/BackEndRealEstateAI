# Backend — RealEstateApp API (.NET 10)

ASP.NET Core 10 Web API + EF Core + PostgreSQL (Npgsql, snake_case naming via `EFCore.NamingConventions`).
Solution `RealEstate.sln` at the repo root contains a single project, `src/RealEstate.Api`.

## Structure
- `Controllers/` — one per resource (Listings, Agents, Inquiries, Favorites, Auth, Admin, Errors, ...). Kept thin.
- `Services/` — business logic and external integrations (S3 upload, image processing, email, DENUE/population-density, JWT issuing, error logging). Controller logic belongs here, not inline in the controller.
- `Models/Entities` — EF Core entities. `Migrations/` — EF Core migrations.
- `Middleware/` — global exception handling, feeding both Sentry and the in-app `ErrorLogService` (admin errors view).

## Auth & roles
- JWT bearer auth. Roles: `Buyer`, `Owner`, `Agent` (an Agent account also gets `Owner`).
- `AuthController.Register`'s `role` field is case-sensitive PascalCase (`"Owner"`, `"Agent"`) — anything else falls through to `Buyer`.
- Some write endpoints require `[Authorize]` even where anonymous access might seem natural (e.g. `POST /api/inquiries`) — check the controller attribute before assuming a 401 is a bug.

## JSON / enums
- No `JsonStringEnumConverter` is configured. Enums (`FundingMethod`, `PurchaseTimeline`, `ListingType`, etc.) must be sent as **raw integers** in request bodies, not PascalCase or camelCase strings.

## Config & secrets
- Real connection string, JWT signing key, and third-party tokens live in `dotnet user-secrets` (`UserSecretsId: realestate-api-secrets`), not in `appsettings.json` (which only has placeholder/`CHANGE_ME` values checked into git). Run `dotnet user-secrets list` from `src/RealEstate.Api` to see the real values.
- AWS S3 (property/agent photos), SMTP email, Sentry DSN, and the INEGI DENUE token are all optional-by-default (no-op or best-effort when unset) — a missing value there is usually intentional, not a bug.

## Images
- Uploaded photos (listings + agent profiles) are resized and recompressed server-side via `ImageProcessingService` (SkiaSharp) before upload to S3. Don't add client-side resizing — the server is the single source of truth for output size/format.

## Commands
- `dotnet build RealEstate.sln` — build.
- `dotnet test RealEstate.sln` — run the `tests/RealEstate.Api.Tests` suite (xUnit + Moq).
- From `backend/`: `dotnet ef migrations add <Name> --project src/RealEstate.Api --startup-project src/RealEstate.Api` and `dotnet ef database update --project src/RealEstate.Api --startup-project src/RealEstate.Api` for migrations. The repo has two `DbContext`s (`ApplicationDbContext` for Identity/most entities, `RealEstateDbContext` for owners) — pass `--context <Name>` when the EF CLI reports more than one found.
- CI (`.github/workflows`) runs `dotnet build` and `dotnet test` on every push and pull request.

## Deploy
- `deploy-dev.yml` triggers on push to **`develop`** (not `main`) and deploys to AWS Elastic Beanstalk (`EB_ENV_NAME: realestateapp-dev`); `deploy-prod.yml` is the prod counterpart. Both run `dotnet build`/`dotnet test` as a `test` job the `deploy` job depends on, so a failing test blocks that deploy.
- Migrations against the real EB database are applied by the app itself on boot when the `RunMigrationsOnStartup` config value is `true` (see `Program.cs`) — EB sets this so the deploy pipeline doesn't need direct network access to RDS. Separately, `Program.cs` also auto-migrates both DbContexts whenever `ASPNETCORE_ENVIRONMENT=Development`, so a local/dev DB self-heals from "column ... does not exist" drift without anyone running `dotnet ef database update` by hand.

## Known loose end
- `.github/workflows/tests.yml` still pins `dotnet-version: 8.0.x` while `ci.yml`/`deploy-dev.yml` use `10.0.x`, and it duplicates `ci.yml`'s build+test on the same push/PR events — worth consolidating into one workflow and bumping it to match the repo's actual TFM (`net10.0`).

## Working in this repo as Claude Code (recurring facts across sessions)
- This sandbox's base container does not always come with `dotnet` preinstalled. If `dotnet` is missing and apt's `noble-updates`/`security` mirrors 404 on `dotnet-sdk-8.0`/`dotnet-sdk-10.0` packages, pin the install to the `noble/main` channel's older point release instead (e.g. `apt-cache madison dotnet-sdk-10.0` to find one, then `apt-get install -y dotnet-sdk-10.0=<version> dotnet-runtime-10.0=<version> aspnetcore-runtime-10.0=<version> dotnet-host-10.0=<version> dotnet-hostfxr-10.0=<version> ...` — pin every related package, not just the SDK, or apt fails pulling in the newer unavailable ones as dependencies). No Docker daemon is available in this sandbox either, so a `mcr.microsoft.com/dotnet/sdk` container isn't a fallback here.
- This project's designated Claude Code branch is `claude/pensive-lamport-q0d6bs`. Once its PR merges, the next session's work restarts that same branch name from the latest `origin/main` (`git checkout -B claude/pensive-lamport-q0d6bs origin/main`) rather than branching again — don't assume the branch's old commits are still what's live.
