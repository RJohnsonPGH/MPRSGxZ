using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MPRSGxZ.Web.Data;
using MPRSGxZ.Web.Dto;
using MPRSGxZ.Web.Services;
using System.Runtime.CompilerServices;

namespace MPRSGxZ.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ZoneController(IDbContextFactory<AmplifierDbContext> contextFactory, 
	IAmplifierService service) : ControllerBase
{
	[HttpGet]
	public async IAsyncEnumerable<ZoneDto> Get([EnumeratorCancellation] CancellationToken token)
	{
		using var context = await contextFactory.CreateDbContextAsync(token);
		//var zones = await context.Zones.ToListAsync();

		await foreach(var currentZone in context.Zones)
		{
			var hardwareZone = service.Amplifier.Zones[currentZone.Id - 1];

			yield return new ZoneDto()
			{
				Id = currentZone.Id,
				Name = currentZone.Name,
				Enabled = currentZone.Enabled,
				MirrorsZoneId = currentZone.MirrorsZoneId,
				LinkMode = service.Amplifier.ZoneLinks.GetLinkMode(currentZone.Id),
				Power = hardwareZone.Power,
				Mute = hardwareZone.Mute,
				PublicAddress = hardwareZone.PublicAddress,
				Keypad = hardwareZone.Keypad,
				DoNotDisturb = hardwareZone.DoNotDisturb,
				Volume = hardwareZone.Volume,
				Treble = hardwareZone.Treble,
				Bass = hardwareZone.Bass,
				Balance = hardwareZone.Balance,
				Source = hardwareZone.Source
			};
		}
	}

	[HttpGet("{id:int:range(1,18)}")]
	public async Task<ZoneDto> Get(int id, CancellationToken token)
	{
		using var context = await contextFactory.CreateDbContextAsync(token);
		var zone = await context.Zones
			.SingleAsync(x => x.Id == id, token);

		var hardwareZone = service.Amplifier.Zones[zone.Id - 1];

		return new()
		{
			Id = zone.Id,
			Name = zone.Name,
			Enabled = zone.Enabled,
			MirrorsZoneId = zone.MirrorsZoneId,
			LinkMode = service.Amplifier.ZoneLinks.GetLinkMode(zone.Id),
			Power = hardwareZone.Power,
			Mute = hardwareZone.Mute,
			PublicAddress = hardwareZone.PublicAddress,
			Keypad = hardwareZone.Keypad,
			DoNotDisturb = hardwareZone.DoNotDisturb,
			Volume = hardwareZone.Volume,
			Treble = hardwareZone.Treble,
			Bass = hardwareZone.Bass,
			Balance = hardwareZone.Balance,
			Source = hardwareZone.Source
		};
	}

	//[HttpGet("{id:int:range(0,17)}")]
	//public IActionResult Put(int id, [FromBody] WebAmp.Models.ZoneSettings PutZone)
	//{
	//	if (AmplifierID > AmplifierService.Amplifiers.Length)
	//	{
	//		return NotFound();
	//	}

	//	var Zone = AmplifierService.Amplifiers[AmplifierID - 1].Zones[ZoneID - 1];

	//	Zone.Power = PutZone.Power;
	//	Zone.Mute = PutZone.Mute;
	//	Zone.PublicAddress = PutZone.PublicAddress;
	//	Zone.DoNotDisturb = PutZone.DoNotDisturb;
	//	Zone.Volume = PutZone.Volume;
	//	Zone.Treble = PutZone.Treble;
	//	Zone.Bass = PutZone.Bass;
	//	Zone.Balance = PutZone.Balance;
	//	Zone.Source = PutZone.Source;
	//	Zone.Name = PutZone.Name;
	//	Zone.Enabled = PutZone.Enabled;
	//	Zone.VolumeFactor = PutZone.VolumeFactor;

	//	return Ok();
	//}
}
