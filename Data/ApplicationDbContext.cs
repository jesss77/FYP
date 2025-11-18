using FYP.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FYP.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<TablesJoin> TablesJoins { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationTables> ReservationTables { get; set; }
        public DbSet<ReservationStatus> ReservationStatuses { get; set; }
        public DbSet<ReservationLog> ReservationLogs { get; set; }
        public DbSet<ActionType> ActionTypes { get; set; }
        public DbSet<Settings> Settings { get; set; }
        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------------- Relationships ----------------
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Customer)
                .WithOne(c => c.ApplicationUser)
                .HasForeignKey<ApplicationUser>(u => u.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Employee)
                .WithOne(e => e.ApplicationUser)
                .HasForeignKey<ApplicationUser>(u => u.EmployeeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TablesJoin>()
                .HasKey(tj => tj.TablesJoinID);

            modelBuilder.Entity<TablesJoin>()
                .HasOne(tj => tj.PrimaryTable)
                .WithMany()
                .HasForeignKey(tj => tj.PrimaryTableID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TablesJoin>()
                .HasOne(tj => tj.JoinedTable)
                .WithMany()
                .HasForeignKey(tj => tj.JoinedTableID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReservationTables>()
                .HasKey(rt => rt.ReservationTablesID);

            modelBuilder.Entity<ReservationTables>()
                .HasOne(rt => rt.Reservation)
                .WithMany(r => r.ReservationTables)
                .HasForeignKey(rt => rt.ReservationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReservationTables>()
                .HasOne(rt => rt.Table)
                .WithMany(t => t.ReservationTables)
                .HasForeignKey(rt => rt.TableID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReservationLog>()
                .HasKey(rl => rl.LogID);

            modelBuilder.Entity<ReservationLog>()
                .HasOne(rl => rl.Reservation)
                .WithMany(r => r.ReservationLogs)
                .HasForeignKey(rl => rl.ReservationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReservationLog>()
                .HasOne(rl => rl.ActionType)
                .WithMany()
                .HasForeignKey(rl => rl.ActionTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasKey(r => r.ReservationID);

            // Reservation to Customer relationship (optional)
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Customer)
                .WithMany(c => c.Reservations)
                .HasForeignKey(r => r.CustomerID)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Reservation to Guest relationship (optional)
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Guest)
                .WithMany(g => g.Reservations)
                .HasForeignKey(r => r.GuestID)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.ReservationStatus)
                .WithMany()
                .HasForeignKey(r => r.ReservationStatusID)
                .OnDelete(DeleteBehavior.Restrict);

            // Add check constraint: either CustomerID or GuestID must be set, but not both
            modelBuilder.Entity<Reservation>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Reservation_CustomerOrGuest",
                    "([IsGuest] = 0 AND [CustomerID] IS NOT NULL AND [GuestID] IS NULL) OR ([IsGuest] = 1 AND [GuestID] IS NOT NULL AND [CustomerID] IS NULL)"
                ));

            // ---------------- Keys ----------------
            modelBuilder.Entity<Customer>().HasKey(c => c.CustomerID);
            modelBuilder.Entity<Guest>().HasKey(g => g.GuestID);
            modelBuilder.Entity<Employee>().HasKey(e => e.EmployeeID);
            modelBuilder.Entity<Table>().HasKey(t => t.TableID);
            modelBuilder.Entity<ReservationStatus>().HasKey(rs => rs.ReservationStatusID);
            modelBuilder.Entity<Settings>().HasKey(s => s.SettingsID);
            modelBuilder.Entity<Restaurant>().HasKey(r => r.RestaurantID);
            modelBuilder.Entity<AuditLog>().HasKey(al => al.EntityID);
            modelBuilder.Entity<ActionType>().HasKey(a => a.ActionTypeID);

            // ---------------- Indexes/Constraints ----------------
            // Ensure table numbers are unique per restaurant
            modelBuilder.Entity<Table>()
                .HasIndex(t => new { t.RestaurantID, t.TableNumber })
                .IsUnique();

            // Index on guest email for lookups
            modelBuilder.Entity<Guest>()
                .HasIndex(g => g.Email);

            // ---------------- Seed Data ----------------
            modelBuilder.Entity<Settings>().HasData(
                new Settings
                {
                    SettingsID = 1,
                    Key = "Name",
                    Value = "Fine O Dine",
                    CreatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedBy = "system"
                },
                new Settings
                {
                    SettingsID = 2,
                    Key = "Opening Hours",
                    Value = "10 AM - 10 PM",
                    CreatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedBy = "system"
                }
            );

            modelBuilder.Entity<Restaurant>().HasData(
                new Restaurant
                {
                    RestaurantID = 1,
                    SettingsID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                }
            );
            
            modelBuilder.Entity<ReservationStatus>().HasData(
                new ReservationStatus
                {
                    ReservationStatusID = 1,
                    StatusName = "Pending",
                    Description = "Awaiting confirmation",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new ReservationStatus
                {
                    ReservationStatusID = 2,
                    StatusName = "Confirmed",
                    Description = "Confirmed by staff/system",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new ReservationStatus
                {
                    ReservationStatusID = 3,
                    StatusName = "Seated",
                    Description = "Customer seated",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new ReservationStatus
                {
                    ReservationStatusID = 4,
                    StatusName = "Completed",
                    Description = "Reservation completed",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new ReservationStatus
                {
                    ReservationStatusID = 5,
                    StatusName = "Cancelled",
                    Description = "Cancelled by customer/staff",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                }
            );

            // ---------------- Seed Default Tables ----------------
            modelBuilder.Entity<Table>().HasData(
                // 2-person tables
                new Table
                {
                    TableID = 1,
                    TableNumber = 1,
                    Capacity = 2,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 2,
                    TableNumber = 2,
                    Capacity = 2,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 3,
                    TableNumber = 3,
                    Capacity = 2,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                // 4-person tables
                new Table
                {
                    TableID = 4,
                    TableNumber = 4,
                    Capacity = 4,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 5,
                    TableNumber = 5,
                    Capacity = 4,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 6,
                    TableNumber = 6,
                    Capacity = 4,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 7,
                    TableNumber = 7,
                    Capacity = 4,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                // 6-person tables
                new Table
                {
                    TableID = 8,
                    TableNumber = 8,
                    Capacity = 6,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 9,
                    TableNumber = 9,
                    Capacity = 6,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 10,
                    TableNumber = 10,
                    Capacity = 6,
                    IsJoinable = true,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                // 8-person tables
                new Table
                {
                    TableID = 11,
                    TableNumber = 11,
                    Capacity = 8,
                    IsJoinable = false,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                },
                new Table
                {
                    TableID = 12,
                    TableNumber = 12,
                    Capacity = 8,
                    IsJoinable = false,
                    IsAvailable = true,
                    RestaurantID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = new DateTime(2025, 10, 16, 12, 0, 0),
                    UpdatedAt = new DateTime(2025, 10, 16, 12, 0, 0)
                }
            );
        }
    }
}