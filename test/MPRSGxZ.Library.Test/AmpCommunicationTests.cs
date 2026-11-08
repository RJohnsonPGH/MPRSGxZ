// These integration tests require a live amplifier reachable over the network and are not run
// as part of normal automated builds. They are kept for manual/local verification only.
/*
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPRSGxZ.Library;
using MPRSGxZ.Library.Events;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AmplifierTests;

[TestClass]
public class AmpCommunicationTests
{
	[TestMethod]
	public async Task AmpPollTest()
	{
		var serviceProvider = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();

		var factory = serviceProvider.GetService<ILoggerFactory>();

		var serialServer = new IPEndPoint(IPAddress.Parse("172.16.20.5"), 23);
		var amplifier = new Amplifier(factory, serialServer);
		await amplifier.OpenAsync(CancellationToken.None);

		Thread.Sleep(new System.TimeSpan(0, 5, 0));
	}

	[TestMethod]
	public async Task AmpZoneControlTest()
	{
		var serviceProvider = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();

		var factory = serviceProvider.GetService<ILoggerFactory>();

		var serialServer = new IPEndPoint(IPAddress.Parse("172.16.20.5"), 23);
		var amplifier = new Amplifier(factory, serialServer);
		await amplifier.OpenAsync(CancellationToken.None);

		foreach (var zone in amplifier.Zones)
		{
			zone.Power = true;
			Thread.Sleep(500);
		}

		foreach (var zone in amplifier.Zones)
		{
			zone.Power = false;
			Thread.Sleep(500);
		}

		foreach (var zone in amplifier.Zones)
		{
			zone.Power = true;
		}
		Thread.Sleep(500);

		foreach (var zone in amplifier.Zones)
		{
			zone.Power = false;
		}
		Thread.Sleep(500);

		amplifier.Close();
	}

	private void ZoneChanged(object? sender, ZoneChangedEventArgs e) { }
}
*/