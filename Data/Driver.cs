using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class Driver
{
    public int DriverId { get; set; }

    public int BranchId { get; set; }

    public string FullName { get; set; } = null!;

    public string LicenseNo { get; set; } = null!;

    public DateOnly LicenseExpiry { get; set; }

    public string? ContactNo { get; set; }

    public int Status { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public virtual ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
}
