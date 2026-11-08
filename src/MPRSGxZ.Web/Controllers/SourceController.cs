using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MPRSGxZ.Web.Data;
using MPRSGxZ.Web.Models;

namespace MPRSGxZ.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SourceController(IDbContextFactory<AmplifierDbContext> contextFactory) : ControllerBase
{
	[HttpGet]
	public async Task<IEnumerable<SoftwareSource>> Get(CancellationToken token)
	{
		using var context = await contextFactory.CreateDbContextAsync(token);
		return await context.Sources.ToListAsync(token);
	}

	[HttpGet("{id:int:range(0,17)}")]
	public async Task<SoftwareSource> Get(int id, CancellationToken token)
	{
		using var context = await contextFactory.CreateDbContextAsync(token);
		return await context.Sources.SingleAsync(x => x.Id == id, token);
	}

	[HttpPut("{id:int:range(0,17)}")]
	public async Task<IActionResult> Put(int id, [FromBody] SoftwareSource value, CancellationToken token)
	{
		if (id != value.Id)
		{
			return Conflict("Supplied id does not match");
		}

		using var context = await contextFactory.CreateDbContextAsync(token);
		var source = await context.Sources.SingleAsync(x => x.Id == id, token);
		source.Name = value.Name;
		source.Enabled = value.Enabled;

		return Ok();
	}
}
