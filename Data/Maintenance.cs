using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class Maintenance
{
    public int MaintenanceId { get; set; }

    public int VehicleId { get; set; }

    public int MaintenanceType { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public int? DueOdometer { get; set; }

    public int Status { get; set; }

    public DateOnly? CompletedDate { get; set; }

    public int? OdometerAtService { get; set; }

    public decimal? Cost { get; set; }

    public string? Notes { get; set; }

    public int ScheduledByUserId { get; set; }

    public virtual UserAccount ScheduledByUser { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;
}
