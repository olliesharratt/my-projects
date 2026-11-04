using System.Text;
using AssetMonitoring.Api.Hubs;
using AssetMonitoring.Api.Middleware;
using AssetMonitoring.Api.Services;
using AssetMonitoring.Application.Assets;
using AssetMonitoring.Infrastructure.Persistence;
using AssetMonitoring.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, config) => config.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());
builder.Services.AddDbContext<MonitoringDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<AssetRepository>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateAssetValidator>();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)) };
    options.Events = new JwtBearerEvents { OnMessageReceived = context => { var token = context.Request.Query["access_token"]; if (!string.IsNullOrWhiteSpace(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs")) context.Token = token; return Task.CompletedTask; } };
});
builder.Services.AddAuthorization(options => options.AddPolicy("EngineerOrAdmin", p => p.RequireRole("Engineer", "Admin")));
builder.Services.AddHostedService<TelemetryWorker>();

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");
app.Run();
