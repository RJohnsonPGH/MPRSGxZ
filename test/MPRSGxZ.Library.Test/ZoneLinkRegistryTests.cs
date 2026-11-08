using System;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Exceptions;
using MPRSGxZ.Library.Hardware;

namespace MPRSGxZ.Library.Test;

[TestClass]
public class ZoneLinkRegistryTests
{
	[TestMethod]
	public void LinkZone_SetsSecondaryAndPrimaryModes()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);

		int[] expected = [2];
		Assert.AreEqual(ZoneLinkMode.Primary, registry.GetLinkMode(1));
		Assert.AreEqual(ZoneLinkMode.Secondary, registry.GetLinkMode(2));
		CollectionAssert.AreEquivalent(expected, (System.Collections.ICollection)registry.GetSecondaries(1));
		Assert.IsTrue(registry.TryGetPrimary(2, out var primaryId));
		Assert.AreEqual(1, primaryId);
	}

	[TestMethod]
	public void LinkZone_SupportsMultipleSecondariesForOnePrimary()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);
		registry.LinkZone(1, 3);

		int[] expected = [2, 3];
		CollectionAssert.AreEquivalent(expected, (System.Collections.ICollection)registry.GetSecondaries(1));
		Assert.AreEqual(ZoneLinkMode.Primary, registry.GetLinkMode(1));
		Assert.AreEqual(ZoneLinkMode.Secondary, registry.GetLinkMode(2));
		Assert.AreEqual(ZoneLinkMode.Secondary, registry.GetLinkMode(3));
	}

	[TestMethod]
	public void GetLinkMode_ReturnsUnlinkedForUnknownZone()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		Assert.AreEqual(ZoneLinkMode.Unlinked, registry.GetLinkMode(5));
	}

	[TestMethod]
	public void LinkZone_ThrowsWhenZoneMirrorsItself()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		Assert.ThrowsExactly<ArgumentException>(() => registry.LinkZone(1, 1));
	}

	[TestMethod]
	public void LinkZone_ThrowsWhenSecondaryAlreadyMirrorsAPrimary()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);

		Assert.ThrowsExactly<ZoneAlreadyLinkedException>(() => registry.LinkZone(3, 2));
	}

	[TestMethod]
	public void LinkZone_ThrowsWhenPrimaryIsAlreadyASecondary_NoChaining()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);

		Assert.ThrowsExactly<ZoneAlreadyLinkedException>(() => registry.LinkZone(2, 3));
	}

	[TestMethod]
	public void LinkZone_ThrowsWhenSecondaryIsAlreadyAPrimary_NoChaining()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);

		Assert.ThrowsExactly<ZoneAlreadyLinkedException>(() => registry.LinkZone(3, 1));
	}

	[TestMethod]
	public void UnlinkZone_RemovesLink()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);
		registry.UnlinkZone(1, 2);

		Assert.AreEqual(ZoneLinkMode.Unlinked, registry.GetLinkMode(1));
		Assert.AreEqual(ZoneLinkMode.Unlinked, registry.GetLinkMode(2));
		Assert.IsFalse(registry.TryGetPrimary(2, out _));
	}

	[TestMethod]
	public void UnlinkZone_ThrowsWhenNotLinked()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		Assert.ThrowsExactly<ZoneNotLinkedException>(() => registry.UnlinkZone(1, 2));
	}

	[TestMethod]
	public void UnlinkZone_ThrowsWhenLinkedToDifferentPrimary()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);
		registry.LinkZone(3, 4);

		Assert.ThrowsExactly<ZoneNotLinkedException>(() => registry.UnlinkZone(1, 4));
	}

	[TestMethod]
	public void ClearLinks_RemovesAllLinks()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);
		registry.LinkZone(1, 3);
		registry.ClearLinks();

		Assert.IsEmpty(registry.GetAllLinks());
		Assert.AreEqual(ZoneLinkMode.Unlinked, registry.GetLinkMode(1));
	}

	[TestMethod]
	public void ApplyLinks_ReplacesExistingLinksAndSkipsInvalidPairs()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);
		registry.LinkZone(5, 6);

		registry.ApplyLinks(
		[
			(1, 2),
			(1, 3),
			(2, 2), // invalid: self-link, should be skipped
			(4, 2)  // invalid: 2 already mirrors 1, should be skipped
		]);

		int[] expected = [2, 3];
		CollectionAssert.AreEquivalent(expected, (System.Collections.ICollection)registry.GetSecondaries(1));
		Assert.AreEqual(ZoneLinkMode.Unlinked, registry.GetLinkMode(5));
		Assert.AreEqual(ZoneLinkMode.Unlinked, registry.GetLinkMode(6));
		Assert.AreEqual(ZoneLinkMode.Unlinked, registry.GetLinkMode(4));
	}

	[TestMethod]
	public void GetAllLinks_ReturnsAllPrimarySecondaryPairs()
	{
		var registry = new ZoneLinkRegistry(NullLogger<ZoneLinkRegistry>.Instance);

		registry.LinkZone(1, 2);
		registry.LinkZone(1, 3);

		var links = registry.GetAllLinks();

		CollectionAssert.AreEquivalent(new[] { (1, 2), (1, 3) }, (System.Collections.ICollection)links);
	}
}
