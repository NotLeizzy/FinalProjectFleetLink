using System;
using System.Collections.Generic;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Data;

public partial class FleetLinkDbContext : DbContext
{
    public FleetLinkDbContext(DbContextOptions<FleetLinkDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Dispatch> Dispatches { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<FuelLog> FuelLogs { get; set; }

    public virtual DbSet<Maintenance> Maintenances { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<Trip> Trips { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleAssignment> VehicleAssignments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("Branch");

            entity.HasIndex(e => e.BranchCode, "UQ_Branch_BranchCode").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.BranchCode).HasMaxLength(10);
            entity.Property(e => e.BranchName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Branch_IsActive");
        });

        modelBuilder.Entity<Dispatch>(entity =>
        {
            entity.ToTable("Dispatch");

            entity.HasIndex(e => e.TripId, "UQ_Dispatch_TripId").IsUnique();

            entity.Property(e => e.CheckSummary).HasMaxLength(500);
            entity.Property(e => e.DispatchedAt).ValueGeneratedNever();

            entity.HasOne(d => d.DispatchedByUser).WithMany(p => p.Dispatches)
                .HasForeignKey(d => d.DispatchedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispatch_User");

            entity.HasOne(d => d.Trip).WithOne(p => p.Dispatch)
                .HasForeignKey<Dispatch>(d => d.TripId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispatch_Trip");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.ToTable("Driver");

            entity.HasIndex(e => e.LicenseNo, "UQ_Driver_LicenseNo").IsUnique();

            entity.Property(e => e.ContactNo).HasMaxLength(20);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.LicenseNo).HasMaxLength(30);
            entity.Property(e => e.Status).HasDefaultValue((int)DriverStatus.Active, "DF_Driver_Status");

            entity.HasOne(d => d.Branch).WithMany(p => p.Drivers)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Driver_Branch");
        });

        modelBuilder.Entity<FuelLog>(entity =>
        {
            entity.ToTable("FuelLog");

            entity.Property(e => e.KmPerLiter).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.Liters).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.TotalCost).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.FuelLogs)
                .HasForeignKey(d => d.RecordedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FuelLog_User");

            entity.HasOne(d => d.Trip).WithMany(p => p.FuelLogs)
                .HasForeignKey(d => d.TripId)
                .HasConstraintName("FK_FuelLog_Trip");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.FuelLogs)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FuelLog_Vehicle");
        });

        modelBuilder.Entity<Maintenance>(entity =>
        {
            entity.ToTable("Maintenance");

            entity.Property(e => e.Cost).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Status).HasDefaultValue((int)MaintenanceStatus.Scheduled, "DF_Maintenance_Status");

            entity.HasOne(d => d.ScheduledByUser).WithMany(p => p.Maintenances)
                .HasForeignKey(d => d.ScheduledByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Maintenance_User");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Maintenances)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Maintenance_Vehicle");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("Shipment");

            entity.HasIndex(e => e.ReferenceNo, "UQ_Shipment_ReferenceNo").IsUnique();

            entity.Property(e => e.CreatedAt).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Destination).HasMaxLength(150);
            entity.Property(e => e.Origin).HasMaxLength(150);
            entity.Property(e => e.ReferenceNo).HasMaxLength(30);
            entity.Property(e => e.Status).HasDefaultValue((int)ShipmentStatus.Pending, "DF_Shipment_Status");
            entity.Property(e => e.WeightKg).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Shipment_Branch");
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.ToTable("Trip");

            entity.Property(e => e.Destination).HasMaxLength(150);
            entity.Property(e => e.DistanceKm).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.Origin).HasMaxLength(150);
            entity.Property(e => e.Status).HasDefaultValue((int)TripStatus.Scheduled, "DF_Trip_Status");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Trips)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Trip_CreatedBy");

            entity.HasOne(d => d.DestinationBranch).WithMany(p => p.TripDestinationBranches)
                .HasForeignKey(d => d.DestinationBranchId)
                .HasConstraintName("FK_Trip_DestBr");

            entity.HasOne(d => d.Driver).WithMany(p => p.Trips)
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Trip_Driver");

            entity.HasOne(d => d.OriginBranch).WithMany(p => p.TripOriginBranches)
                .HasForeignKey(d => d.OriginBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Trip_OriginBr");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Trips)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Trip_Vehicle");

            entity.HasMany(d => d.Shipments).WithMany(p => p.Trips)
                .UsingEntity<Dictionary<string, object>>(
                    "TripShipment",
                    r => r.HasOne<Shipment>().WithMany()
                        .HasForeignKey("ShipmentId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TripShipment_Shipment"),
                    l => l.HasOne<Trip>().WithMany()
                        .HasForeignKey("TripId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TripShipment_Trip"),
                    j =>
                    {
                        j.HasKey("TripId", "ShipmentId");
                        j.ToTable("TripShipment");
                    });
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("UserAccount");

            entity.HasIndex(e => e.Email, "UQ_UserAccount_Email").IsUnique();

            entity.Property(e => e.CreatedAt).ValueGeneratedNever();
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_UserAccount_IsActive");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(100).IsRequired();

            entity.HasOne(d => d.Branch).WithMany(p => p.UserAccounts)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_UserAccount_Branch");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("Vehicle");

            entity.HasIndex(e => e.PlateNo, "UQ_Vehicle_PlateNo").IsUnique();

            entity.Property(e => e.CapacityKg).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ExpectedKmPerLiter).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.MakeModel).HasMaxLength(100);
            entity.Property(e => e.PlateNo).HasMaxLength(15);
            entity.Property(e => e.Status).HasDefaultValue((int)VehicleStatus.Available, "DF_Vehicle_Status");

            entity.HasOne(d => d.Branch).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Vehicle_Branch");
        });

        modelBuilder.Entity<VehicleAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);

            entity.ToTable("VehicleAssignment");

            entity.HasOne(d => d.Driver).WithMany(p => p.VehicleAssignments)
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleAssignment_Driver");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleAssignments)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VehicleAssignment_Vehicle");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
