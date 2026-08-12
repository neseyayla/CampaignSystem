# CampaignSystem

Credit card campaign definition and batch evaluation system, built as an ASP.NET Core Web API.

A campaign is defined once with its criteria — segment, card product, merchant, transaction type, amount range — and a batch job then evaluates transactions against it and writes the earned rewards. No campaign rule is hardcoded; the scope always comes from the criteria tables.

## Stack

- .NET 10, ASP.NET Core Web API (controller based)
- Entity Framework Core 10, code first with migrations
- SQL Server (LocalDB in development)
- OpenAPI + Swagger UI

## Setup

```bash
git clone https://github.com/neseyayla/CampaignSystem.git
cd CampaignSystem
dotnet restore
```

Install the EF Core CLI once per machine:

```bash
dotnet tool install --global dotnet-ef
```

The connection string is **not** in the repository. Set your own with User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=CampaignSystem;Trusted_Connection=True;TrustServerCertificate=True" --project CampaignSystem
```

Then create the database and its tables:

```bash
dotnet ef database update --project CampaignSystem
```

Every developer runs against their own local database — only the migrations are shared, so the schema comes out identical on every machine.

Run the API:

```bash
dotnet run --project CampaignSystem
```

Swagger UI is served at `/swagger`.

## Layout

```
CampaignSystem/
  Entities/       POCO classes, no EF Core dependency
  Enums/          Campaign type, earning type, status, ...
  Data/
    CampaignDbContext.cs
    Configurations/   Fluent API mapping, one class per entity
    Converters/       Enum to database code converters
    Migrations/
  Controllers/
docs/
  schema.dbml           single source of truth for the schema
  database-design.md    design document with ER diagram
```

Mapping rules live in `Data/Configurations` rather than as attributes on the entities, because composite keys, filtered indexes and multi-column unique constraints cannot be expressed with data annotations.

## Adding a schema change

1. Update `docs/schema.dbml`
2. Change the entity and its configuration class
3. `dotnet ef migrations add <Name> --project CampaignSystem --output-dir Data/Migrations`
4. Read the generated migration before applying it
5. `dotnet ef database update --project CampaignSystem`

## Branching

`main` ← `dev` ← `feature/*`. Every step is done on a short lived branch off `dev` and merged back through a pull request. Commit messages follow Conventional Commits.
