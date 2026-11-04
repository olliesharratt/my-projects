# Asset Monitoring Dashboard

**Status: scaffold; not yet runnable.** This project is planned as an industrial asset monitoring and maintenance application.

The current checkout contains a .NET solution, Docker Compose configuration, and Angular package/configuration files. The source folders referenced by those files (`src/AssetMonitoring.*` and `frontend/src`) are not present, so the API and dashboard cannot currently be built or started.

## Planned Architecture

```text
Angular dashboard -> ASP.NET Core API -> Application -> Domain
                                      -> Infrastructure (EF Core/PostgreSQL)
                                      -> SignalR hub
```

## Planned Features

- Asset inventory and paged search
- Simulated telemetry with threshold alerts and live updates
- Maintenance work orders with controlled state transitions
- Authentication and role-based access

## Next Steps

Add the API and frontend source, then verify clean-checkout setup, tests, and Docker Compose before presenting this as a working demo.
