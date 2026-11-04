# HS&E Incident and Near-Miss Reporting

Employees report incidents and near-misses; reviewers move them through a controlled workflow with an append-only audit trail.

**Stack:** ASP.NET Core 8 minimal API (C#), Angular 22, reactive forms, Swagger.

The API uses in-memory storage, so records are cleared when it stops. Automated API tests and corrective-action controls in the web UI are not implemented yet.

## Run Locally

Prerequisites: .NET 8 SDK, Node.js 22.22.3+ or 24.15+.

Start the API from this directory:
```bash
cd api
dotnet run --urls http://localhost:5000
```

In a second terminal, start the web app:
```bash
cd web
npm ci
npm start
```

Open `http://localhost:4200`. Swagger is at `http://localhost:5000/swagger`.

## Workflow

- Incidents can move through Reported, UnderReview, ActionsAssigned, and Closed states; invalid transitions return `409 Conflict`.
- Each create and transition appends an audit entry.
- Closing an incident with unfinished corrective actions is rejected by the API.
- The dashboard displays summary counts and lets reviewers create incidents and move their status.

## API Example

```bash
curl -X POST http://localhost:5000/api/incidents/ \
	-H 'Content-Type: application/json' \
	-d '{"title":"Oil leak","description":"Small leak near pump","category":"NearMiss","severity":"Medium","location":"North bay","reportedBy":"Alex"}'
```

