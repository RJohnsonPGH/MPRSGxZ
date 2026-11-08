using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MPRSGxZ.Web.Models;
using MPRSGxZ.Web.Data;

namespace MPRSGxZ.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ConnectionController(IDbContextFactory<AmplifierDbContext> contextFactory) : ControllerBase
{
	[HttpGet]
	public async Task<ConnectionSetting> Get(CancellationToken token)
	{
		using var context = await contextFactory.CreateDbContextAsync(token);
		return await context.ConnectionSettings.SingleAsync(token);
	}

	//[HttpPost]
	//public void Post([FromBody] AmplifierStackSettings value)
	//{
	//}

	//[HttpPut]
	//public void Put([FromBody] AmplifierStackSettings value)
	//{
	//}

	//[HttpDelete]
	//public void Delete()
	//{
	//}
}
