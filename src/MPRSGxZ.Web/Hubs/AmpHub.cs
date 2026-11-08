using Microsoft.AspNetCore.SignalR;

namespace MPRSGxZ.Web.Hubs;

public class AmpHub : Hub<IAmpHub>
{
	public async Task SendUpdate() => await Clients.All.SendUpdate();
}