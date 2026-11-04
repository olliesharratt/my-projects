using ChatServer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomStore>();
builder.Services.AddSingleton<PresenceTracker>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<ChatHub>("/chat");
app.Run();

public partial class Program;   // lets integration tests use WebApplicationFactory<Program>
