using Microsoft.Extensions.Logging;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Exceptions;

namespace MPRSGxZ.Library.Hardware;

/// <summary>
/// Tracks primary/secondary zone mirroring relationships.
/// </summary>
/// <remarks>
/// A primary zone may have any number of secondary zones. A secondary zone may mirror exactly
/// one primary. Chaining is not supported: a zone that is already a primary cannot become a
/// secondary of another zone, and a zone that is already a secondary cannot become a primary.
/// This class is hardware-agnostic; it only tracks zone ids and their link mode.
/// </remarks>
/// <param name="logger">Used to report stored links that fail validation during <see cref="ApplyLinks"/>.</param>
public sealed partial class ZoneLinkRegistry(ILogger<ZoneLinkRegistry> logger)
{
	private readonly ILogger _logger = logger;

	private readonly Dictionary<int, int> _secondaryToPrimary = [];
	private readonly Dictionary<int, HashSet<int>> _primaryToSecondaries = [];
	private readonly Lock _lock = new();

	/// <summary>
	/// Links <paramref name="secondaryId"/> as a mirror of <paramref name="primaryId"/>.
	/// </summary>
	/// <exception cref="ArgumentException">A zone was linked to itself.</exception>
	/// <exception cref="ZoneAlreadyLinkedException">
	/// Either zone already participates in a link that would conflict with the requested one
	/// (the secondary already mirrors a zone, or either zone is already a primary/secondary in a
	/// way that would require chaining).
	/// </exception>
	public void LinkZone(int primaryId, int secondaryId)
	{
		if (primaryId == secondaryId)
		{
			throw new ArgumentException("A zone cannot mirror itself.", nameof(secondaryId));
		}

		lock (_lock)
		{
			if (_secondaryToPrimary.ContainsKey(secondaryId))
			{
				throw new ZoneAlreadyLinkedException($"Zone {secondaryId} already mirrors a primary zone.");
			}

			if (_primaryToSecondaries.ContainsKey(secondaryId))
			{
				throw new ZoneAlreadyLinkedException($"Zone {secondaryId} is already a primary zone and cannot also be a secondary.");
			}

			if (_secondaryToPrimary.ContainsKey(primaryId))
			{
				throw new ZoneAlreadyLinkedException($"Zone {primaryId} already mirrors another zone and cannot also be a primary.");
			}

			if (!_primaryToSecondaries.TryGetValue(primaryId, out var secondaries))
			{
				secondaries = [];
				_primaryToSecondaries[primaryId] = secondaries;
			}

			secondaries.Add(secondaryId);
			_secondaryToPrimary[secondaryId] = primaryId;
		}
	}

	/// <summary>
	/// Removes the mirroring link between <paramref name="primaryId"/> and <paramref name="secondaryId"/>.
	/// </summary>
	/// <exception cref="ZoneNotLinkedException">The two zones are not currently linked.</exception>
	public void UnlinkZone(int primaryId, int secondaryId)
	{
		lock (_lock)
		{
			if (!_secondaryToPrimary.TryGetValue(secondaryId, out var existingPrimary) || existingPrimary != primaryId)
			{
				throw new ZoneNotLinkedException($"Zone {secondaryId} does not mirror zone {primaryId}.");
			}

			_secondaryToPrimary.Remove(secondaryId);

			if (_primaryToSecondaries.TryGetValue(primaryId, out var secondaries))
			{
				secondaries.Remove(secondaryId);
				if (secondaries.Count == 0)
				{
					_primaryToSecondaries.Remove(primaryId);
				}
			}
		}
	}

	/// <summary>
	/// Removes all links.
	/// </summary>
	public void ClearLinks()
	{
		lock (_lock)
		{
			_secondaryToPrimary.Clear();
			_primaryToSecondaries.Clear();
		}
	}

	/// <summary>
	/// Replaces all links with the provided set of primary/secondary pairs.
	/// </summary>
	/// <remarks>
	/// Used to bulk-load link configuration (e.g. from the database) without requiring the
	/// caller to unlink previous state first. Pairs that fail validation are skipped rather than
	/// aborting the whole load, since stored configuration could theoretically go stale.
	/// </remarks>
	public void ApplyLinks(IEnumerable<(int PrimaryId, int SecondaryId)> links)
	{
		lock (_lock)
		{
			_secondaryToPrimary.Clear();
			_primaryToSecondaries.Clear();
		}

		var applied = 0;
		var skipped = 0;

		foreach (var (primaryId, secondaryId) in links)
		{
			try
			{
				LinkZone(primaryId, secondaryId);
				applied++;
			}
			catch (Exception ex) when (ex is ArgumentException or ZoneAlreadyLinkedException)
			{
				// Skip invalid stored links rather than failing the whole reload. Warning rather than error
				// because the reload itself still completes; only this one pair is discarded.
				skipped++;
				LogSkippedInvalidStoredLink(ex, primaryId, secondaryId);
			}
		}

		LogAppliedLinks(applied, skipped);
	}

	/// <summary>
	/// Gets whether <paramref name="zoneId"/> is unlinked, a primary, or a secondary.
	/// </summary>
	public ZoneLinkMode GetLinkMode(int zoneId)
	{
		lock (_lock)
		{
			if (_secondaryToPrimary.ContainsKey(zoneId))
			{
				return ZoneLinkMode.Secondary;
			}

			if (_primaryToSecondaries.ContainsKey(zoneId))
			{
				return ZoneLinkMode.Primary;
			}

			return ZoneLinkMode.Unlinked;
		}
	}

	/// <summary>
	/// Gets the secondary zone ids currently mirroring <paramref name="primaryId"/>.
	/// </summary>
	public IReadOnlyList<int> GetSecondaries(int primaryId)
	{
		lock (_lock)
		{
			return _primaryToSecondaries.TryGetValue(primaryId, out var secondaries)
				? [.. secondaries]
				: [];
		}
	}

	/// <summary>
	/// Attempts to get the primary zone id that <paramref name="secondaryId"/> mirrors.
	/// </summary>
	public bool TryGetPrimary(int secondaryId, out int primaryId)
	{
		lock (_lock)
		{
			return _secondaryToPrimary.TryGetValue(secondaryId, out primaryId);
		}
	}

	/// <summary>
	/// Gets all currently configured primary/secondary pairs.
	/// </summary>
	public IReadOnlyList<(int PrimaryId, int SecondaryId)> GetAllLinks()
	{
		lock (_lock)
		{
			return [.. _secondaryToPrimary.Select(kvp => (PrimaryId: kvp.Value, SecondaryId: kvp.Key))];
		}
	}
}
