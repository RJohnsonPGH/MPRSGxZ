using Microsoft.EntityFrameworkCore;
using MPRSGxZ.Web.Models;

namespace MPRSGxZ.Web.Data;

public class AmplifierDbContext(DbContextOptions options) : DbContext(options)
{
	public DbSet<ConnectionSetting> ConnectionSettings { get; set; }
	public DbSet<SoftwareSource> Sources { get; set; }
	public DbSet<SoftwareZone> Zones { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ConnectionSetting>()
			.HasKey(x => x.Id);

		modelBuilder.Entity<ConnectionSetting>()
			.HasData(new { Id = 1, Address = "localhost", Port = 1234 });

		modelBuilder.Entity<SoftwareSource>()
			.HasKey(x => x.Id);

		modelBuilder.Entity<SoftwareZone>()
			.HasKey(x => x.Id);

		modelBuilder.Entity<SoftwareZone>()
			.Property(x => x.MirrorsZoneId)
			.IsRequired(false);

		modelBuilder.Entity<SoftwareSource>()
			.HasData(new { Id = 1, Name = "Source 1", Enabled = true },
				new { Id = 2, Name = "Source 2", Enabled = true },
				new { Id = 3, Name = "Source 3", Enabled = true },
				new { Id = 4, Name = "Source 4", Enabled = true },
				new { Id = 5, Name = "Source 5", Enabled = true },
				new { Id = 6, Name = "Source 6", Enabled = true });

		modelBuilder.Entity<SoftwareZone>()
			.HasData(new { Id = 1, Name = "Amp 1 Zone 1", Enabled = true },
				new { Id = 2, Name = "Amp 1 Zone 2", Enabled = true },
				new { Id = 3, Name = "Amp 1 Zone 3", Enabled = true },
				new { Id = 4, Name = "Amp 1 Zone 4", Enabled = true },
				new { Id = 5, Name = "Amp 1 Zone 5", Enabled = true },
				new { Id = 6, Name = "Amp 1 Zone 6", Enabled = true },
				new { Id = 7, Name = "Amp 2 Zone 1", Enabled = false },
				new { Id = 8, Name = "Amp 2 Zone 2", Enabled = false },
				new { Id = 9, Name = "Amp 2 Zone 3", Enabled = false },
				new { Id = 10, Name = "Amp 2 Zone 4", Enabled = false },
				new { Id = 11, Name = "Amp 2 Zone 5", Enabled = false },
				new { Id = 12, Name = "Amp 2 Zone 6", Enabled = false },
				new { Id = 13, Name = "Amp 3 Zone 1", Enabled = true },
				new { Id = 14, Name = "Amp 3 Zone 2", Enabled = true },
				new { Id = 15, Name = "Amp 3 Zone 3", Enabled = true },
				new { Id = 16, Name = "Amp 3 Zone 4", Enabled = true },
				new { Id = 17, Name = "Amp 3 Zone 5", Enabled = true },
				new { Id = 18, Name = "Amp 3 Zone 6", Enabled = true });
	}
}
