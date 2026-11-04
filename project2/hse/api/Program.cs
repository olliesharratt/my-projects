using System.Collections.Concurrent;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
// Swap this registration for a SqlIncidentRepository / CosmosIncidentRepository to compare SQL vs NoSQL.
builder.Services.AddSingleton<IIncidentRepository, InMemoryIncidentRepository>();
builder.Services.AddSingleton<IncidentService>();

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api/incidents");
api.MapGet("/", (IncidentService s, Status? status, Severity? severity) => s.List(status, severity));
api.MapGet("/{id:guid}", (IncidentService s, Guid id) => s.Get(id) is { } i ? Results.Ok(i) : Results.NotFound());
api.MapPost("/", (IncidentService s, CreateIncident dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Location))
        return Results.BadRequest("Title and location are required.");
    var i = s.Create(dto);
    return Results.Created($"/api/incidents/{i.Id}", i);
});
api.MapPost("/{id:guid}/transition", (IncidentService s, Guid id, TransitionRequest r) =>
{
    try { return s.Transition(id, r) is { } i ? Results.Ok(i) : Results.NotFound(); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
});
api.MapPost("/{id:guid}/actions", (IncidentService s, Guid id, AddAction a) =>
    s.AddAction(id, a) is { } i ? Results.Ok(i) : Results.NotFound());
app.MapGet("/api/analytics", (IncidentService s) => s.Analytics());
app.MapGet("/health", () => "ok");
app.Run();

// ---------- Domain ----------
public enum Severity { Low, Medium, High, Critical }
public enum Category { NearMiss, Injury, Environmental, EquipmentFailure, Other }
public enum Status { Reported, UnderReview, ActionsAssigned, Closed }

public record AuditEntry(DateTime At, string By, string Event);
public record CorrectiveAction(Guid Id, string Description, string Owner, DateOnly Due, bool Done);

public class Incident
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public Category Category { get; init; }
    public Severity Severity { get; init; }
    public string Location { get; init; } = "";
    public string ReportedBy { get; init; } = "";
    public DateTime ReportedAt { get; init; } = DateTime.UtcNow;
    public Status Status { get; set; } = Status.Reported;
    public List<CorrectiveAction> Actions { get; } = new();
    // Append-only: only IncidentService adds entries, nothing edits or deletes them.
    public List<AuditEntry> Audit { get; } = new();
}

public record CreateIncident(string Title, string Description, Category Category, Severity Severity, string Location, string ReportedBy);
public record TransitionRequest(Status To, string By);
public record AddAction(string Description, string Owner, DateOnly Due, string By);

// ---------- Persistence abstraction ----------
public interface IIncidentRepository
{
    IEnumerable<Incident> All();
    Incident? Find(Guid id);
    void Save(Incident i);
}

public class InMemoryIncidentRepository : IIncidentRepository
{
    private readonly ConcurrentDictionary<Guid, Incident> _store = new();
    public IEnumerable<Incident> All() => _store.Values;
    public Incident? Find(Guid id) => _store.GetValueOrDefault(id);
    public void Save(Incident i) => _store[i.Id] = i;
}

// ---------- Application logic ----------
public class IncidentService(IIncidentRepository repo)
{
    // The workflow state machine: which moves are legal from each state.
    private static readonly Dictionary<Status, Status[]> Allowed = new()
    {
        [Status.Reported] = [Status.UnderReview],
        [Status.UnderReview] = [Status.ActionsAssigned, Status.Closed],
        [Status.ActionsAssigned] = [Status.Closed],
        [Status.Closed] = []
    };

    public IEnumerable<Incident> List(Status? status, Severity? severity) =>
        repo.All().Where(i => (status is null || i.Status == status) && (severity is null || i.Severity == severity))
                  .OrderByDescending(i => i.ReportedAt);

    public Incident? Get(Guid id) => repo.Find(id);

    public Incident Create(CreateIncident d)
    {
        var i = new Incident { Title = d.Title, Description = d.Description, Category = d.Category,
            Severity = d.Severity, Location = d.Location, ReportedBy = d.ReportedBy };
        i.Audit.Add(new(DateTime.UtcNow, d.ReportedBy, "Reported"));
        repo.Save(i);
        return i;
    }

    public Incident? Transition(Guid id, TransitionRequest r)
    {
        if (repo.Find(id) is not { } i) return null;
        if (!Allowed[i.Status].Contains(r.To))
            throw new InvalidOperationException($"Cannot move from {i.Status} to {r.To}.");
        if (r.To == Status.Closed && i.Actions.Any(a => !a.Done))
            throw new InvalidOperationException("Complete all corrective actions before closing.");
        i.Audit.Add(new(DateTime.UtcNow, r.By, $"{i.Status} -> {r.To}"));
        i.Status = r.To;
        repo.Save(i);
        return i;
    }

    public Incident? AddAction(Guid id, AddAction a)
    {
        if (repo.Find(id) is not { } i) return null;
        i.Actions.Add(new(Guid.NewGuid(), a.Description, a.Owner, a.Due, false));
        i.Audit.Add(new(DateTime.UtcNow, a.By, $"Action added: {a.Description}"));
        repo.Save(i);
        return i;
    }

    public object Analytics()
    {
        var all = repo.All().ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new
        {
            Total = all.Count,
            BySeverity = all.GroupBy(i => i.Severity).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ByMonth = all.GroupBy(i => i.ReportedAt.ToString("yyyy-MM")).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            OverdueActions = all.SelectMany(i => i.Actions.Where(a => !a.Done && a.Due < today)).Count()
        };
    }
}
