using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AssetMonitoring.Api.Hubs;

[Authorize]
public sealed class MonitoringHub : Hub { }
