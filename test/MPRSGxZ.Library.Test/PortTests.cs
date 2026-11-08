// These integration tests require a live amplifier reachable over the network and are not run
// as part of normal automated builds. They are kept for manual/local verification only.
/*
﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPRSGxZ.Commands;
using MPRSGxZ.Enumerators;
using MPRSGxZ.Events;
using MPRSGxZ.Ports;
using System.IO;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MPRSGxZ;

namespace AmpTest;

[TestClass]
public class PortTests
{
	[TestMethod]
	public async Task TestAmplifierCountDetection()
	{
		var serviceProvider = new ServiceCollection()
			.AddLogging(x => { x.AddConsole(); })
			.BuildServiceProvider();

		var factory = serviceProvider.GetService<ILoggerFactory>();

		var serialServer = new IPEndPoint(IPAddress.Parse("172.16.20.5"), 23);
		var amplifier = new Amplifier(factory, serialServer);
		await amplifier.OpenAsync(CancellationToken.None);
		
		Assert.AreEqual(1, amplifier.AmplifierCount);
	}

	//[TestMethod]
	//public async Task TestAmplifierQuery()
	//{
	//	var serialServer = new IPEndPoint(IPAddress.Parse("172.16.20.5"), 23);
	//	var port = new IPAmplifierPort(serialServer);

	//	await port.OpenAsync(CancellationToken.None);
	//	var result = await port.ExecuteCommandAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All));

	//	Assert.AreEqual(6, result.Count());
	//}

	//[TestMethod]
	//public async Task TestAmplifierSet()
	//{
	//	var serialServer = new IPEndPoint(IPAddress.Parse("172.16.20.5"), 23);
	//	var port = new IPAmplifierPort(serialServer);

	//	await port.OpenAsync(CancellationToken.None);

	//	var result = await port.ExecuteCommandAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All));
	//	var zoneOne = result.Single(x => x.ZoneId == 1);
	//	Assert.AreEqual(false, zoneOne.Power);

	//	result = await port.ExecuteCommandAsync(new SetCommand(AmplifierIdentifier.One, ZoneIdentifier.One, SetCommandType.Power, 1));

	//	result = await port.ExecuteCommandAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All));
	//	zoneOne = result.Single(x => x.ZoneId == 1);
	//	Assert.AreEqual(true, zoneOne.Power);

	//	Thread.Sleep(3000);

	//	result = await port.ExecuteCommandAsync(new SetCommand(AmplifierIdentifier.One, ZoneIdentifier.One, SetCommandType.Power, 0));

	//	result = await port.ExecuteCommandAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All));
	//	zoneOne = result.Single(x => x.ZoneId == 1);
	//	Assert.AreEqual(false, zoneOne.Power);
	//}

	//private void ZoneChanged(object? sender, ZoneChangedEventArgs e) { }
}
*/
