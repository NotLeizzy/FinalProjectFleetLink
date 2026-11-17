using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class Trip
{
    public int TripId { get; set; }

    public int VehicleId { get; set; }

    public int DriverId { get; set; }

    public int OriginBranchId { get; set; }

    public int? DestinationBranchId { get; set; }

    public string Origin { get; set; } = null!;

    public string Destination { get; set; } = null!;

    public DateTime PlannedDeparture { get; set; }

    public DateTime PlannedArrival { get; set; }

    public DateTime? ActualDeparture { get; set; }

    public DateTime? ActualArrival { get; set; }

    public decimal? DistanceKm { get; set; }

    public int Status { get; set; }

    public int CreatedByUserId { get; set; }

    public virtual UserAccount CreatedByUser { get; set; } = null!;

    public virtual Branch? DestinationBranch { get; set; }

    public virtual Dispatch? Dispatch { get; set; }

    public virtual Driver Driver { get; set; } = null!;

    public virtual ICollection<FuelLog> FuelLogs { get; set; } = new List<FuelLog>();

    public virtual Branch OriginBranch { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
}
