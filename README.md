# LinePilot MES — Manufacturing Execution System

A server-rendered **ASP.NET Core MVC (.NET 10)** application built by **Aoun Raza** for running a factory floor: production dashboard, work orders, machine status with downtime tracking, and raw-material inventory.

🔗 **Live demo:** https://linepilot-aoun.vercel.app
> Runs in a container that sleeps when idle, so the first request can take a few seconds. The demo database is in memory and resets to its sample data whenever the server restarts.

## Features

- **Plant dashboard:** today's output vs target, scrap rate, machines running, overdue orders, a 14-day output chart with a target line, per-line progress, live stoppages and the orders that need attention
- **Work orders:** list with status tabs, search, line filter and pagination; create and edit with server-side and client-side validation; a status workflow (Planned → In progress ⇄ On hold → Completed / Cancelled) enforced in the service layer; record shift output with yield tracking
- **Machines:** shop-floor view grouped by line, utilization, and a status change form. Stopping a machine (Down / Maintenance) requires a reason and opens a downtime entry; restarting it closes the entry automatically
- **Inventory:** stock levels against reorder points, low-stock filter and "receive stock" postings
- Post/Redirect/Get with flash messages, anti-forgery tokens on every form, friendly 404 and error pages
- Responsive Bootstrap 5 UI with a custom light industrial theme; status is always shown with an icon and a label, not colour alone

## Tech Stack

| Layer | Tools |
|:--|:--|
| Web | ASP.NET Core MVC (.NET 10), Razor views, tag helpers, jQuery unobtrusive validation |
| Data | Entity Framework Core 10, SQLite (in-memory, seeded on startup) |
| UI | Bootstrap 5, custom CSS, HTML/CSS chart |
| Testing | xUnit, `WebApplicationFactory` integration tests (19 tests) |
| Deployment | Docker image (`Dockerfile.vercel`) running on Vercel |

## Project Structure

```
linepilot/
├── Dockerfile.vercel          # multi-stage build: .NET SDK → ASP.NET runtime image
├── LinePilot.slnx             # solution (app + tests)
├── LinePilot/                 # the MVC app
│   ├── Program.cs             # DI, EF Core, data protection, forwarded headers, routes
│   ├── Controllers/           # Home (dashboard), WorkOrders, Machines, Inventory
│   ├── Services/              # business rules: status workflow, downtime, stock
│   ├── Models/Entities.cs     # EF Core entities
│   ├── ViewModels/            # view models and validated form models
│   ├── Data/                  # DbContext and seed data
│   ├── Views/                 # Razor views and partials
│   └── wwwroot/css/site.css   # theme
└── LinePilot.Tests/           # unit tests (services) + integration tests (HTTP)
```

## Run Locally

Requires the **.NET 10 SDK**.

```bash
dotnet run --project LinePilot     # opens on the URL printed in the console
dotnet test                        # runs all tests
```

Or open `LinePilot.slnx` in Visual Studio 2022+ / Rider and press **F5**.

## Deploy

```bash
vercel deploy --prod
```

Vercel builds the image from `Dockerfile.vercel` on its own servers. Forms use anti-forgery tokens, so every instance must share the same data protection key: it is provided as the `DATA_PROTECTION_KEY_XML` environment variable in Vercel (never committed to the repo).

## Author

**Aoun Raza** — [GitHub](https://github.com/aoun02) · [LinkedIn](https://www.linkedin.com/in/aoun-raza-5b2638285/) · [Portfolio](https://aounraza.vercel.app) · aounraza008@gmail.com
