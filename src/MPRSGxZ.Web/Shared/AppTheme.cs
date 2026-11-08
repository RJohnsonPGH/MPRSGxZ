using MudBlazor;

namespace MPRSGxZ.Web.Shared;

/// <summary>
/// The application-wide MudBlazor theme. A single definition serves both light and dark modes:
/// <see cref="MudTheme.PaletteLight"/> and <see cref="MudTheme.PaletteDark"/> are applied by
/// <see cref="MudThemeProvider"/> based on the user's theme preference.
/// </summary>
public static class AppTheme
{
	private const string PrimaryLight = "#0277BD";
	private const string PrimaryDark = "#4FC3F7";
	private const string ChromeLight = "#123A5E";
	private const string ChromeDark = "#101C2C";

	public static MudTheme Current { get; } = new()
	{
		PaletteLight = new PaletteLight
		{
			Primary = PrimaryLight,
			Info = "#0288D1",
			Success = "#2E7D32",
			Warning = "#ED6C02",
			Error = "#C62828",
			Background = "#F4F6F8",
			Surface = "#FFFFFF",
			AppbarBackground = ChromeLight,
			AppbarText = "#FFFFFF",
			DrawerBackground = ChromeLight,
			DrawerText = "#D6E4F0",
			DrawerIcon = "#9DBDDB",
			LinesDefault = "#E2E8F0",
			LinesInputs = "#CBD5E1"
		},
		PaletteDark = new PaletteDark
		{
			Primary = PrimaryDark,
			Info = "#4FC3F7",
			Success = "#66BB6A",
			Warning = "#FFA726",
			Error = "#E57373",
			Background = "#0B1220",
			Surface = "#151F32",
			AppbarBackground = ChromeDark,
			AppbarText = "#E5EAF2",
			DrawerBackground = ChromeDark,
			DrawerText = "#B0BEC5",
			DrawerIcon = "#78909C",
			TextPrimary = "#E5EAF2",
			TextSecondary = "#AEBACA",
			LinesDefault = "#243347",
			LinesInputs = "#33455C"
		},
		LayoutProperties =
		{
			DefaultBorderRadius = "10px",
			DrawerWidthLeft = "232px"
		}
	};
}
