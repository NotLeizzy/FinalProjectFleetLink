using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class Branch
{
    public int BranchId { get; set; }

    public string BranchCode { get; set; } = null!;

    public string BranchName { get; set; } = null!;

    public string? Address { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    public virtual ICollection<Trip> TripDestinationBranches { get; set; } = new List<Trip>();

    public virtual ICollection<Trip> TripOriginBranches { get; set; } = new List<Trip>();

    public virtual ICollection<UserAccount> UserAccounts { get; set; } = new List<UserAccount>();

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
