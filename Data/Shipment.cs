using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class Shipment
{
    public int ShipmentId { get; set; }

    public int BranchId { get; set; }

    public string ReferenceNo { get; set; } = null!;

    public string? Description { get; set; }

    public decimal WeightKg { get; set; }

    public int CargoType { get; set; }

    public string Origin { get; set; } = null!;

    public string Destination { get; set; } = null!;

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
