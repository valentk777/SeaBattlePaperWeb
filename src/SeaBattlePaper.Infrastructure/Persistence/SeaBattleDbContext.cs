using Microsoft.EntityFrameworkCore;
using SeaBattlePaper.Domain.Matches;

namespace SeaBattlePaper.Infrastructure.Persistence;

public sealed class SeaBattleDbContext(DbContextOptions<SeaBattleDbContext> options) : DbContext(options)
{
    public DbSet<Match> Matches => Set<Match>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<Ship> Ships => Set<Ship>();

    public DbSet<Shot> Shots => Set<Shot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable("Matches");
            entity.HasKey(match => match.Id);
            entity.Property(match => match.Id).HasMaxLength(24);
            entity.Property(match => match.Mode).HasMaxLength(32).IsRequired();
            entity.Property(match => match.RevealSunkShips).HasDefaultValue(true);
            entity.Property(match => match.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasMany(match => match.Players)
                .WithOne(player => player.Match)
                .HasForeignKey(player => player.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(match => match.Shots)
                .WithOne()
                .HasForeignKey(shot => shot.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("Players");
            entity.HasKey(player => player.Id);
            entity.Property(player => player.MatchId).HasMaxLength(24).IsRequired();
            entity.Property(player => player.Nickname).HasMaxLength(24).IsRequired();
            entity.Property(player => player.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(player => new { player.MatchId, player.Seat }).IsUnique();
            entity.HasMany(player => player.Ships)
                .WithOne(ship => ship.Player)
                .HasForeignKey(ship => ship.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ship>(entity =>
        {
            entity.ToTable("Ships");
            entity.HasKey(ship => ship.Id);
            entity.Property(ship => ship.CellOffsets).HasMaxLength(128).IsRequired();
            entity.HasIndex(ship => ship.PlayerId);
        });

        modelBuilder.Entity<Shot>(entity =>
        {
            entity.ToTable("SeaBattleShots");
            entity.HasKey(shot => shot.Id);
            entity.Property(shot => shot.MatchId).HasMaxLength(24).IsRequired();
            entity.Property(shot => shot.Result).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(shot => new { shot.MatchId, shot.AttackerPlayerId, shot.Row, shot.Column }).IsUnique();
            entity.HasIndex(shot => new { shot.MatchId, shot.Sequence }).IsUnique();
        });
    }
}
