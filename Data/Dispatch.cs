using System;
using System.Collections.Generic;

namespace FinalProjectFleetLink.Data;

public partial class Dispatch
{
    public int DispatchId { get; set; }

    public int TripId { get; set; }

    public int DispatchedByUserId { get; set; }

    public DateTime DispatchedAt { get; set; }

    public string? CheckSummary { get; set; }

    public virtual UserAccount DispatchedByUser { get; set; } = null!;

    public virtual Trip Trip { get; set; } = null!;
}
