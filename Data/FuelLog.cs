using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class FuelLog
{
    public int FuelLogId { get; set; }

    public int VehicleId { get; set; }

    public int? TripId { get; set; }

    public DateTime FuelDate { get; set; }

    public decimal Liters { get; set; }

    public decimal TotalCost { get; set; }

    public int Odometer { get; set; }

    public decimal? KmPerLiter { get; set; }

    public bool IsAbnormal { get; set; }

    public int RecordedByUserId { get; set; }

    public virtual UserAccount RecordedByUser { get; set; } = null!;

    public virtual Trip? Trip { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;
}
