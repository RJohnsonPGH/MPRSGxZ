using MPRSGxZ.Library.IntegrationTest.Configuration;
using System.Runtime.CompilerServices;
using Xunit;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// Marks a test that requires a live amplifier. The test is skipped, with an explanatory reason, unless hardware
/// testing has been enabled in configuration.
/// </summary>
/// <remarks>
/// The skip decision is made at discovery time from the same configuration the fixture uses, so a developer with no
/// rig attached sees a run full of clearly explained skips rather than a wall of connection timeouts.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HardwareFactAttribute : FactAttribute
{
	public HardwareFactAttribute(
		[CallerFilePath] string? sourceFilePath = null,
		[CallerLineNumber] int sourceLineNumber = -1)
		: base(sourceFilePath, sourceLineNumber)
	{
		Skip = HardwareTestConfiguration.Options.GetSkipReason();
	}
}

/// <summary>
/// Marks a test that requires a live amplifier and changes its state. Skipped unless both hardware testing and
/// mutation have been enabled.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HardwareMutationFactAttribute : FactAttribute
{
	public HardwareMutationFactAttribute(
		[CallerFilePath] string? sourceFilePath = null,
		[CallerLineNumber] int sourceLineNumber = -1)
		: base(sourceFilePath, sourceLineNumber)
	{
		Skip = HardwareTestConfiguration.Options.GetMutationSkipReason();
	}
}

/// <summary>
/// Marks a data driven test that requires a live amplifier.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HardwareTheoryAttribute : TheoryAttribute
{
	public HardwareTheoryAttribute(
		[CallerFilePath] string? sourceFilePath = null,
		[CallerLineNumber] int sourceLineNumber = -1)
		: base(sourceFilePath, sourceLineNumber)
	{
		Skip = HardwareTestConfiguration.Options.GetSkipReason();
	}
}

/// <summary>
/// Marks a data driven test that requires a live amplifier and changes its state.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class HardwareMutationTheoryAttribute : TheoryAttribute
{
	public HardwareMutationTheoryAttribute(
		[CallerFilePath] string? sourceFilePath = null,
		[CallerLineNumber] int sourceLineNumber = -1)
		: base(sourceFilePath, sourceLineNumber)
	{
		Skip = HardwareTestConfiguration.Options.GetMutationSkipReason();
	}
}
