using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public int BranchId { get; set; }

    public string PlateNo { get; set; } = null!;

    public int VehicleType { get; set; }

    public string? MakeModel { get; set; }

    public decimal CapacityKg { get; set; }

    public DateOnly RegistrationExpiry { get; set; }

    public decimal ExpectedKmPerLiter { get; set; }

    public int CurrentOdometer { get; set; }

    public int Status { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<FuelLog> FuelLogs { get; set; } = new List<FuelLog>();

    public virtual ICollection<Maintenance> Maintenances { get; set; } = new List<Maintenance>();

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public virtual ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
}
