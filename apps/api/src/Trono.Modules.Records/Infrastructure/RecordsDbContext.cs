using Microsoft.EntityFrameworkCore;
using Trono.Modules.Identity.Contracts;
using Trono.Modules.Records.Domain;

namespace Trono.Modules.Records.Infrastructure;

public sealed class AccountReference
{
    public Guid Id { get; set; }
}
public sealed class CreationReceipt
{
    public Guid AccountId { get; set; }
    public Guid Key { get; set; }
    public Guid RecordId { get; set; }
    public string ContentHash { get; set; } = null!;
}
public sealed class RecordsDbContext(DbContextOptions<RecordsDbContext> options) : DbContext(options)
{
    public DbSet<PrivateRecord> Records => Set<PrivateRecord>();
    public DbSet<CreationReceipt> CreationReceipts => Set<CreationReceipt>();
    public DbSet<AccountReference> AccountReferences => Set<AccountReference>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("records");
        builder.Entity<AccountReference>().ToTable(AccountPersistenceContract.Table, AccountPersistenceContract.Schema, table => table.ExcludeFromMigrations());
        builder.Entity<AccountReference>().HasKey(account => account.Id);
        builder.Entity<PrivateRecord>(record =>
        {
            record.ToTable("Records");
            record.HasKey(value => value.Id);
            record.Property(value => value.Notes).HasMaxLength(280);
            record.Property(value => value.Type).HasMaxLength(20);
            record.HasIndex(value => new { value.AccountId, value.OccurredAt, value.Id });
            record.HasOne<AccountReference>().WithMany().HasForeignKey(value => value.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<CreationReceipt>(receipt =>
        {
            receipt.ToTable("CreationReceipts");
            receipt.HasKey(value => new { value.AccountId, value.Key });
            receipt.Property(value => value.ContentHash).HasMaxLength(64);
            receipt.HasOne<AccountReference>().WithMany().HasForeignKey(value => value.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
