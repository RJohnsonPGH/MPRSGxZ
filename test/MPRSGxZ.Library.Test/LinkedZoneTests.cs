// The tests below reference an older AmplifierSolution API that no longer exists. They are kept
// commented out for historical reference; see ZoneLinkRegistryTests.cs for the current
// equivalent coverage of link validation against MPRSGxZ.Library.Hardware.ZoneLinkRegistry.
/*
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPRSGxZ;
using System.IO;

namespace AmplifierTests;

[TestClass]
public class LinkedZoneTests
{
	[TestMethod]
	public void AddLinkZone()
	{
		var TestAmplifierSolution = new AmplifierSolution(true);

		TestAmplifierSolution.Amplifiers[0].Enabled = true;
		TestAmplifierSolution.Zones[0].Enabled = true;
		TestAmplifierSolution.Zones[1].Enabled = true;

		TestAmplifierSolution.LinkZone(1, 2);
	}

	[TestMethod]
	[ExpectedException(typeof(ZoneNotEnabledException))]
	public void AddDisabledLinkZone()
	{
		var TestAmplifierSolution = new AmplifierSolution(true);

		TestAmplifierSolution.Amplifiers[0].Enabled = true;
		TestAmplifierSolution.Zones[0].Enabled = true;
		TestAmplifierSolution.Zones[1].Enabled = false;

		TestAmplifierSolution.LinkZone(1, 2);
	}

	[TestMethod]
	[ExpectedException(typeof(ZoneAlreadyLinkedException))]
	public void AddAlreadyLinkedZone()
	{
		var TestAmplifierSolution = new AmplifierSolution(true);

		TestAmplifierSolution.Amplifiers[0].Enabled = true;
		TestAmplifierSolution.Zones[0].Enabled = true;
		TestAmplifierSolution.Zones[1].Enabled = true;

		TestAmplifierSolution.LinkZone(1, 2);
		TestAmplifierSolution.LinkZone(1, 2);
	}

	[TestMethod]
	public void RemoveLinkZone()
	{
		var TestAmplifierSolution = new AmplifierSolution(true);

		TestAmplifierSolution.Amplifiers[0].Enabled = true;
		TestAmplifierSolution.Zones[0].Enabled = true;
		TestAmplifierSolution.Zones[1].Enabled = true;

		TestAmplifierSolution.LinkZone(1, 2);
		TestAmplifierSolution.UnlinkZone(1, 2);
	}

	[TestMethod]
	[ExpectedException(typeof(ZoneNotLinkedException))]
	public void RemoveUnlinkedPrimaryLinkZone()
	{
		var TestAmplifierSolution = new AmplifierSolution(true);

		TestAmplifierSolution.Amplifiers[0].Enabled = true;
		TestAmplifierSolution.Zones[0].Enabled = true;
		TestAmplifierSolution.Zones[1].Enabled = false;

		TestAmplifierSolution.UnlinkZone(1, 2);
	}

	[TestMethod]
	[ExpectedException(typeof(ZoneNotLinkedException))]
	public void RemoveUnlinkedSecondaryLinkedZone()
	{
		var TestAmplifierSolution = new AmplifierSolution(true);

		TestAmplifierSolution.Amplifiers[0].Enabled = true;
		TestAmplifierSolution.Zones[0].Enabled = true;
		TestAmplifierSolution.Zones[1].Enabled = true;
		TestAmplifierSolution.Zones[2].Enabled = true;

		TestAmplifierSolution.LinkZone(1, 2);
		TestAmplifierSolution.UnlinkZone(1, 3);
	}

	[TestMethod]
	[ExpectedException(typeof(ZoneNotLinkedException))]
	public void RemoveOtherSecondaryLinkedZone()
	{
		var TestAmplifierSolution = new AmplifierSolution(true);

		TestAmplifierSolution.Amplifiers[0].Enabled = true;
		TestAmplifierSolution.Zones[0].Enabled = true;
		TestAmplifierSolution.Zones[1].Enabled = true;
		TestAmplifierSolution.Zones[2].Enabled = true;
		TestAmplifierSolution.Zones[3].Enabled = true;

		TestAmplifierSolution.LinkZone(1, 2);
		TestAmplifierSolution.LinkZone(3, 4);

		TestAmplifierSolution.UnlinkZone(1, 4);
	}
}
*/