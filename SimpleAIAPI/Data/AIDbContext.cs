using Microsoft.EntityFrameworkCore;
using SimpleAIAPI.Models;

namespace SimpleAIAPI.Data;

public class AIDbContext : DbContext
{
    public AIDbContext(DbContextOptions<AIDbContext> options) : base(options) { }

    public DbSet<AIServer> AIServers => Set<AIServer>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AIServer>(entity =>
        {
            entity.ToTable("ai_server");
            entity.HasKey(e => e.AI_IP);
            entity.Property(e => e.AI_IP).HasColumnName("ai_ip");
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");
            entity.HasKey(e => e.ClientID);
            entity.Property(e => e.ClientID).HasColumnName("clientid");
            entity.Property(e => e.Client_IP).HasColumnName("client_ip");
            entity.Property(e => e.Model).HasColumnName("model");
            entity.Property(e => e.AI_IP).HasColumnName("ai_ip");

            entity.HasOne(c => c.AIServer)
                .WithMany(s => s.Clients)
                .HasForeignKey(c => c.AI_IP);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("message");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Role).HasColumnName("role");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.ClientID).HasColumnName("clientid");

            entity.HasOne(m => m.Client)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ClientID);
        });
    }
}
