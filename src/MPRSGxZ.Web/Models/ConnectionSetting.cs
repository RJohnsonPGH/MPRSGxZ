namespace MPRSGxZ.Web.Models;

public sealed class ConnectionSetting
{
	public int Id { get; set; }
	public required string Address { get; set; }
	public required int Port { get; set; } = 23;
}
