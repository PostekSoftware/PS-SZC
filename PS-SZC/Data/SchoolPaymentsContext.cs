using Microsoft.EntityFrameworkCore;

namespace PS_SZC.Data;

public sealed class Family
{
    public int Id { get; set; }

    public decimal StartingBalance { get; set; }

    public ICollection<Parent> Parents { get; set; } = [];

    public ICollection<Child> Children { get; set; } = [];

    public ICollection<FamilyPrice> Prices { get; set; } = [];

    public ICollection<FamilyDiscount> Discounts { get; set; } = [];

    public ICollection<Transfer> Transfers { get; set; } = [];
}

public sealed class Parent
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public Family Family { get; set; } = null!;

    public int ParentIndex { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Pesel { get; set; }
}

public sealed class Child
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public Family Family { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly BirthDate { get; set; }

    public string? Pesel { get; set; }
}

public sealed class FamilyPrice
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public Family Family { get; set; } = null!;

    public int EffectiveYear { get; set; }

    public int EffectiveMonth { get; set; }

    public decimal Amount { get; set; }
}

public sealed class FamilyDiscount
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public Family Family { get; set; } = null!;

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Amount { get; set; }
}

public sealed class Transfer
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public Family Family { get; set; } = null!;

    public DateOnly TransferDate { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }
}

public sealed class SchoolPaymentsContext : DbContext
{
    public const string DatabaseName = "school-payments";

    public SchoolPaymentsContext(DbContextOptions<SchoolPaymentsContext> options)
        : base(options)
    {
    }

    public DbSet<Family> Families => Set<Family>();

    public DbSet<Parent> Parents => Set<Parent>();

    public DbSet<Child> Children => Set<Child>();

    public DbSet<FamilyPrice> FamilyPrices => Set<FamilyPrice>();

    public DbSet<FamilyDiscount> FamilyDiscounts => Set<FamilyDiscount>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Family>(entity =>
        {
            entity.ToTable("families");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StartingBalance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Parent>(entity =>
        {
            entity.ToTable("parents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).IsRequired();
            entity.Property(x => x.LastName).IsRequired();
            entity.Property(x => x.Pesel).HasMaxLength(11);
            entity.HasIndex(x => new { x.FamilyId, x.ParentIndex }).IsUnique();
            entity.HasOne(x => x.Family).WithMany(x => x.Parents).HasForeignKey(x => x.FamilyId);
        });

        modelBuilder.Entity<Child>(entity =>
        {
            entity.ToTable("children");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).IsRequired();
            entity.Property(x => x.LastName).IsRequired();
            entity.Property(x => x.Pesel).HasMaxLength(11);
            entity.HasOne(x => x.Family).WithMany(x => x.Children).HasForeignKey(x => x.FamilyId);
        });

        modelBuilder.Entity<FamilyPrice>(entity =>
        {
            entity.ToTable("family_prices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.FamilyId, x.EffectiveYear, x.EffectiveMonth }).IsUnique();
            entity.HasOne(x => x.Family).WithMany(x => x.Prices).HasForeignKey(x => x.FamilyId);
        });

        modelBuilder.Entity<FamilyDiscount>(entity =>
        {
            entity.ToTable("family_discounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.FamilyId, x.Year, x.Month }).IsUnique();
            entity.HasOne(x => x.Family).WithMany(x => x.Discounts).HasForeignKey(x => x.FamilyId);
        });

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.ToTable("transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.Family).WithMany(x => x.Transfers).HasForeignKey(x => x.FamilyId);
        });
    }
}
