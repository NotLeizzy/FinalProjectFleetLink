namespace FinalProjectFleetLink.Models;

public enum Role
{
    Administrator = 1,
    FleetManager = 2,
    Dispatcher = 3,
    MaintenanceOfficer = 4,
    FuelFinanceClerk = 5
}

public enum VehicleStatus
{
    Available = 1,
    OnTrip = 2,
    InMaintenance = 3,
    Inactive = 4
}

public enum DriverStatus
{
    Active = 1,
    OnTrip = 2,
    OnLeave = 3,
    Inactive = 4
}

public enum ShipmentStatus
{
    Pending = 1,
    Assigned = 2,
    InTransit = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum TripStatus
{
    Scheduled = 1,
    Dispatched = 2,
    InTransit = 3,
    Completed = 4,
    Cancelled = 5
}

public enum MaintenanceStatus
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum VehicleType
{
    Van = 1,
    LightTruck = 2,
    WingVan = 3,
    Refrigerated = 4,
    TrailerTruck = 5
}

public enum CargoType
{
    General = 1,
    ColdChain = 2,
    Fragile = 3,
    Oversized = 4
}

public enum MaintenanceType
{
    Preventive = 1,
    Repair = 2,
    Inspection = 3
}

public enum Module
{
    Dashboard,
    Users,
    Branches,
    Vehicles,
    Drivers,
    Assignments,
    Shipments,
    Trips,
    Dispatch,
    Maintenance,
    FuelMonitor,
    Reports
}

public enum AccessLevel
{
    None,
    ReadOnly,
    ScheduleOnly,
    Full
}
