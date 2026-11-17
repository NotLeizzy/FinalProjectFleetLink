using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record MaintenanceListItem(
    int MaintenanceId,
    int VehicleId,
    string PlateNo,
    MaintenanceType MaintenanceType,
    DateOnly ScheduledDate,
    int? DueOdometer,
    int CurrentOdometer,
    MaintenanceStatus Status,
    MaintenanceDueState DueState,
    DateOnly? CompletedDate,
    decimal? Cost,
    string BranchName,
    string? Notes);

public sealed record MaintenanceVehicleOption(
    int VehicleId,
    string PlateNo,
    int CurrentOdometer,
    DateOnly? LastCompletedServiceDate,
    int BranchId,
    string BranchName);

public sealed record MaintenanceRequest(
    int VehicleId,
    MaintenanceType MaintenanceType,
    DateOnly ScheduledDate,
    int? DueOdometer,
    string? Notes);

public sealed record ServiceRecordRequest(
    DateOnly CompletedDate,
    int OdometerAtService,
    decimal? Cost,
    string? Notes);

public sealed record MaintenanceActionResult(bool Success, string? ErrorMessage = null);

public sealed record VehicleMaintenanceHistoryItem(
    MaintenanceType MaintenanceType,
    DateOnly ScheduledDate,
    DateOnly? CompletedDate,
    int? OdometerAtService,
    decimal? Cost,
    string? Notes,
    MaintenanceStatus Status);

public sealed record VehicleMaintenanceHistory(
    int VehicleId,
    string PlateNo,
    int CurrentOdometer,
    IReadOnlyList<VehicleMaintenanceHistoryItem> Records,
    int ServiceCount,
    decimal TotalCost,
    DateOnly? LastCompletedServiceDate,
    DateOnly? NextDueDate,
    int? NextDueOdometer);

public sealed class MaintenanceService
{
    public async Task<VehicleMaintenanceHistory?> GetVehicleHistoryAsync(int vehicleId)
    {
        EnsureViewAccess();
        await using var db = DbContextFactory.Create();
        var vehicle = await db.Vehicles
            .AsNoTracking()
            .ForCurrentBranch()
            .Include(item => item.Maintenances)
            .SingleOrDefaultAsync(item => item.VehicleId == vehicleId);
        if (vehicle is null)
        {
            return null;
        }

        var records = vehicle.Maintenances
            .OrderByDescending(item => item.ScheduledDate)
            .ThenByDescending(item => item.MaintenanceId)
            .Select(item => new VehicleMaintenanceHistoryItem(
                (MaintenanceType)item.MaintenanceType,
                item.ScheduledDate,
                item.CompletedDate,
                item.OdometerAtService,
                item.Cost,
                item.Notes,
                (MaintenanceStatus)item.Status))
            .ToList();
        var active = vehicle.Maintenances
            .Where(item => item.Status == (int)MaintenanceStatus.Scheduled
                || item.Status == (int)MaintenanceStatus.InProgress)
            .OrderBy(item => item.ScheduledDate)
            .ThenBy(item => item.DueOdometer ?? int.MaxValue)
            .FirstOrDefault();

        return new VehicleMaintenanceHistory(
            vehicle.VehicleId,
            vehicle.PlateNo,
            vehicle.CurrentOdometer,
            records,
            vehicle.Maintenances.Count(item => item.Status == (int)MaintenanceStatus.Completed),
            vehicle.Maintenances
                .Where(item => item.Status == (int)MaintenanceStatus.Completed)
                .Sum(item => item.Cost ?? 0),
            vehicle.Maintenances
                .Where(item => item.Status == (int)MaintenanceStatus.Completed)
                .Max(item => item.CompletedDate),
            active?.ScheduledDate,
            active?.DueOdometer);
    }

    public async Task<IReadOnlyList<MaintenanceListItem>> GetMaintenanceAsync(
        string? search,
        MaintenanceType? maintenanceType,
        MaintenanceStatus? status,
        int? branchId,
        DateOnly? scheduledFrom,
        DateOnly? scheduledTo,
        bool dueOnly,
        bool showCompletedCancelled)
    {
        EnsureViewAccess();
        ValidateBranch(branchId);
        if (scheduledFrom.HasValue && scheduledTo.HasValue && scheduledFrom > scheduledTo)
        {
            throw new ArgumentException("The scheduled date range is invalid.");
        }

        await using var db = DbContextFactory.Create();
        var query = db.Maintenances
            .AsNoTracking()
            .ForCurrentBranch()
            .Include(item => item.Vehicle)
            .Include(item => item.Vehicle.Branch)
            .AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(item => item.Vehicle.BranchId == branchId.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Vehicle.PlateNo.Contains(term));
        }
        if (maintenanceType.HasValue)
        {
            query = query.Where(item => item.MaintenanceType == (int)maintenanceType.Value);
        }
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == (int)status.Value);
        }
        else if (!showCompletedCancelled)
        {
            query = query.Where(item => item.Status != (int)MaintenanceStatus.Completed
                && item.Status != (int)MaintenanceStatus.Cancelled);
        }
        if (scheduledFrom.HasValue)
        {
            query = query.Where(item => item.ScheduledDate >= scheduledFrom.Value);
        }
        if (scheduledTo.HasValue)
        {
            query = query.Where(item => item.ScheduledDate <= scheduledTo.Value);
        }

        var rows = await query
            .OrderBy(item => item.ScheduledDate)
            .ThenBy(item => item.Vehicle.PlateNo)
            .ToListAsync();
        var result = rows.Select(ToListItem).ToList();
        return dueOnly
            ? result.Where(item => item.DueState is MaintenanceDueState.Overdue or MaintenanceDueState.DueSoon).ToList()
            : result;
    }

    public async Task<MaintenanceListItem?> GetByIdAsync(int maintenanceId)
    {
        EnsureViewAccess();
        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .AsNoTracking()
            .ForCurrentBranch()
            .Include(record => record.Vehicle)
            .Include(record => record.Vehicle.Branch)
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        return item is null ? null : ToListItem(item);
    }

    public async Task<IReadOnlyList<MaintenanceVehicleOption>> GetVehicleOptionsAsync()
    {
        EnsureScheduleAccess();
        await using var db = DbContextFactory.Create();
        var vehicles = await db.Vehicles
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(vehicle => vehicle.Status != (int)VehicleStatus.Inactive)
            .Include(vehicle => vehicle.Branch)
            .OrderBy(vehicle => vehicle.PlateNo)
            .ToListAsync();
        return vehicles.Select(vehicle => new MaintenanceVehicleOption(
            vehicle.VehicleId,
            vehicle.PlateNo,
            vehicle.CurrentOdometer,
            db.Maintenances
                .Where(item => item.VehicleId == vehicle.VehicleId
                    && item.Status == (int)MaintenanceStatus.Completed
                    && item.CompletedDate.HasValue)
                .OrderByDescending(item => item.CompletedDate)
                .Select(item => item.CompletedDate)
                .FirstOrDefault(),
            vehicle.BranchId,
            vehicle.Branch.BranchName)).ToList();
    }

    public async Task<MaintenanceActionResult> ScheduleAsync(MaintenanceRequest request)
    {
        EnsureScheduleAccess();
        ValidateRequest(request, creating: true);
        await using var db = DbContextFactory.Create();
        var vehicle = await GetScopedVehicleAsync(db, request.VehicleId);
        if (vehicle is null)
        {
            return Failure("The vehicle was not found or is outside your branch scope.");
        }
        var validation = ValidateVehicleRequest(vehicle, request);
        if (!validation.Success)
        {
            return validation;
        }
        if (await HasDuplicateAsync(db, request, null))
        {
            return Failure("An active maintenance record already exists for this vehicle, type, and scheduled date.");
        }

        db.Maintenances.Add(new Maintenance
        {
            VehicleId = vehicle.VehicleId,
            MaintenanceType = (int)request.MaintenanceType,
            ScheduledDate = request.ScheduledDate,
            DueOdometer = request.DueOdometer,
            Status = (int)MaintenanceStatus.Scheduled,
            Notes = NormalizeNotes(request.Notes),
            ScheduledByUserId = Session.CurrentUser!.UserId
        });
        await db.SaveChangesAsync();
        return new MaintenanceActionResult(true);
    }

    public async Task<MaintenanceActionResult> EditAsync(int maintenanceId, MaintenanceRequest request)
    {
        EnsureScheduleAccess();
        ValidateRequest(request, creating: false);
        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .ForCurrentBranch()
            .Include(record => record.Vehicle)
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        if (item is null)
        {
            return Failure("The maintenance record was not found.");
        }
        if (item.Status != (int)MaintenanceStatus.Scheduled)
        {
            return Failure("Only Scheduled maintenance records can have their vehicle, type, date, or odometer edited.");
        }
        var vehicle = await GetScopedVehicleAsync(db, request.VehicleId);
        if (vehicle is null)
        {
            return Failure("The vehicle was not found or is outside your branch scope.");
        }
        var validation = ValidateVehicleRequest(vehicle, request);
        if (!validation.Success)
        {
            return validation;
        }
        if (await HasDuplicateAsync(db, request, maintenanceId))
        {
            return Failure("An active maintenance record already exists for this vehicle, type, and scheduled date.");
        }
        item.VehicleId = request.VehicleId;
        item.MaintenanceType = (int)request.MaintenanceType;
        item.ScheduledDate = request.ScheduledDate;
        item.DueOdometer = request.DueOdometer;
        item.Notes = NormalizeNotes(request.Notes);
        await db.SaveChangesAsync();
        return new MaintenanceActionResult(true);
    }

    public async Task<MaintenanceActionResult> UpdateInProgressNotesAsync(int maintenanceId, string? notes)
    {
        EnsureScheduleAccess();
        var normalized = NormalizeNotes(notes);
        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .ForCurrentBranch()
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        if (item is null)
        {
            return Failure("The maintenance record was not found.");
        }
        if (item.Status != (int)MaintenanceStatus.InProgress)
        {
            return Failure("Only InProgress maintenance records can have their notes edited.");
        }
        item.Notes = normalized;
        await db.SaveChangesAsync();
        return new MaintenanceActionResult(true);
    }

    public async Task<MaintenanceActionResult> CancelAsync(int maintenanceId, string reason)
    {
        EnsureScheduleAccess();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Failure("A cancellation reason is required.");
        }
        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .ForCurrentBranch()
            .Include(record => record.Vehicle)
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        if (item is null)
        {
            return Failure("The maintenance record was not found.");
        }
        if (item.Status is not ((int)MaintenanceStatus.Scheduled or (int)MaintenanceStatus.InProgress))
        {
            return Failure("Only Scheduled or InProgress maintenance records can be cancelled.");
        }
        var newNotes = AppendCancellationReason(item.Notes, reason);
        if (newNotes is null)
        {
            return Failure("The cancellation reason would exceed the 500-character notes limit.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var wasInProgress = item.Status == (int)MaintenanceStatus.InProgress;
        item.Status = (int)MaintenanceStatus.Cancelled;
        item.Notes = newNotes;
        if (wasInProgress && item.Vehicle.Status == (int)VehicleStatus.InMaintenance
            && !await HasOtherInProgressAsync(db, item.VehicleId, item.MaintenanceId))
        {
            item.Vehicle.Status = (int)VehicleStatus.Available;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new MaintenanceActionResult(true);
    }

    public async Task<MaintenanceActionResult> StartWorkAsync(int maintenanceId)
    {
        EnsureOfficerAccess();
        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .ForCurrentBranch()
            .Include(record => record.Vehicle)
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        if (item is null)
        {
            return Failure("The maintenance record was not found.");
        }
        if (item.Status != (int)MaintenanceStatus.Scheduled)
        {
            return Failure("Only Scheduled maintenance records can be started.");
        }
        if (item.Vehicle.Status == (int)VehicleStatus.Inactive)
        {
            return Failure("Maintenance cannot be started for an inactive vehicle.");
        }
        if (item.Vehicle.Status == (int)VehicleStatus.OnTrip)
        {
            return Failure("Maintenance cannot be started while the vehicle is On Trip.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        item.Status = (int)MaintenanceStatus.InProgress;
        item.Vehicle.Status = (int)VehicleStatus.InMaintenance;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new MaintenanceActionResult(true);
    }

    public async Task<MaintenanceActionResult> RecordServiceAsync(int maintenanceId, ServiceRecordRequest request)
    {
        EnsureOfficerAccess();
        if (request.CompletedDate == default)
        {
            return Failure("Completed date is required.");
        }
        if (request.OdometerAtService < 0)
        {
            return Failure("Odometer at service cannot be negative.");
        }
        if (request.Cost.HasValue && request.Cost.Value < 0)
        {
            return Failure("Cost cannot be negative.");
        }
        var notes = NormalizeNotes(request.Notes);
        await using var db = DbContextFactory.Create();
        var item = await db.Maintenances
            .ForCurrentBranch()
            .Include(record => record.Vehicle)
            .SingleOrDefaultAsync(record => record.MaintenanceId == maintenanceId);
        if (item is null)
        {
            return Failure("The maintenance record was not found.");
        }
        if (item.Status != (int)MaintenanceStatus.InProgress)
        {
            return Failure("Only InProgress maintenance records can be recorded as completed.");
        }
        if (request.OdometerAtService < item.Vehicle.CurrentOdometer)
        {
            return Failure($"Odometer at service must be at least the vehicle's current odometer ({item.Vehicle.CurrentOdometer}).");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        item.Status = (int)MaintenanceStatus.Completed;
        item.CompletedDate = request.CompletedDate;
        item.OdometerAtService = request.OdometerAtService;
        item.Cost = request.Cost;
        item.Notes = notes;
        item.Vehicle.CurrentOdometer = request.OdometerAtService;
        if (!await HasOtherInProgressAsync(db, item.VehicleId, item.MaintenanceId))
        {
            item.Vehicle.Status = (int)VehicleStatus.Available;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new MaintenanceActionResult(true);
    }

    private static MaintenanceListItem ToListItem(Maintenance item) => new(
        item.MaintenanceId,
        item.VehicleId,
        item.Vehicle.PlateNo,
        (MaintenanceType)item.MaintenanceType,
        item.ScheduledDate,
        item.DueOdometer,
        item.Vehicle.CurrentOdometer,
        (MaintenanceStatus)item.Status,
        MaintenanceDueHelper.GetDueState(item, item.Vehicle),
        item.CompletedDate,
        item.Cost,
        item.Vehicle.Branch.BranchName,
        item.Notes);

    private static async Task<Vehicle?> GetScopedVehicleAsync(FleetLinkDbContext db, int vehicleId) => await db.Vehicles
        .ForCurrentBranch()
        .SingleOrDefaultAsync(vehicle => vehicle.VehicleId == vehicleId);

    private static Task<bool> HasDuplicateAsync(FleetLinkDbContext db, MaintenanceRequest request, int? excludedId) => db.Maintenances
        .ForCurrentBranch()
        .AnyAsync(item => item.VehicleId == request.VehicleId
            && item.MaintenanceType == (int)request.MaintenanceType
            && item.ScheduledDate == request.ScheduledDate
            && (item.Status == (int)MaintenanceStatus.Scheduled || item.Status == (int)MaintenanceStatus.InProgress)
            && (!excludedId.HasValue || item.MaintenanceId != excludedId.Value));

    private static Task<bool> HasOtherInProgressAsync(FleetLinkDbContext db, int vehicleId, int excludedId) => db.Maintenances
        .ForCurrentBranch()
        .AnyAsync(item => item.VehicleId == vehicleId
            && item.MaintenanceId != excludedId
            && item.Status == (int)MaintenanceStatus.InProgress);

    private static MaintenanceActionResult ValidateVehicleRequest(Vehicle vehicle, MaintenanceRequest request)
    {
        if (vehicle.Status == (int)VehicleStatus.Inactive)
        {
            return Failure("Inactive vehicles cannot be scheduled for maintenance.");
        }
        if (request.DueOdometer.HasValue && request.DueOdometer.Value <= vehicle.CurrentOdometer)
        {
            return Failure($"Due odometer must be greater than the vehicle's current odometer ({vehicle.CurrentOdometer}).");
        }
        return new MaintenanceActionResult(true);
    }

    private static void ValidateRequest(MaintenanceRequest request, bool creating)
    {
        if (request.VehicleId <= 0)
        {
            throw new ArgumentException("Vehicle is required.");
        }
        if (!Enum.IsDefined(request.MaintenanceType))
        {
            throw new ArgumentException("Maintenance type is invalid.");
        }
        if (creating && request.ScheduledDate < DateOnly.FromDateTime(DateTime.Now))
        {
            throw new ArgumentException("Scheduled date cannot be before today.");
        }
        if (request.ScheduledDate == default)
        {
            throw new ArgumentException("Scheduled date is required.");
        }
        if (request.DueOdometer.HasValue && request.DueOdometer.Value < 0)
        {
            throw new ArgumentException("Due odometer must be a whole number greater than zero.");
        }
        if (request.Notes?.Length > 500)
        {
            throw new ArgumentException("Notes cannot exceed 500 characters.");
        }
    }

    private static string? NormalizeNotes(string? notes) => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private static string? AppendCancellationReason(string? notes, string reason)
    {
        var addition = $"Cancellation reason: {reason.Trim()}";
        var combined = string.IsNullOrWhiteSpace(notes) ? addition : $"{notes.Trim()} | {addition}";
        return combined.Length <= 500 ? combined : null;
    }

    private static void ValidateBranch(int? branchId)
    {
        if (!Session.IsHeadOffice && branchId != Session.CurrentUser!.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }
    }

    private static void EnsureViewAccess()
    {
        var user = Session.CurrentUser ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (!Permissions.CanAccess(user.Role, Module.Maintenance))
        {
            throw new UnauthorizedAccessException("You do not have permission to access Maintenance.");
        }
    }

    private static void EnsureScheduleAccess()
    {
        var user = Session.CurrentUser ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        var access = Permissions.GetAccessLevel(user.Role, Module.Maintenance);
        if (access is not (AccessLevel.Full or AccessLevel.ScheduleOnly))
        {
            throw new UnauthorizedAccessException("You do not have permission to schedule Maintenance.");
        }
    }

    private static void EnsureOfficerAccess()
    {
        var user = Session.CurrentUser ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Maintenance) != AccessLevel.Full
            || user.Role != Role.MaintenanceOfficer)
        {
            throw new UnauthorizedAccessException("Only Maintenance Officers can perform this maintenance action.");
        }
    }

    private static MaintenanceActionResult Failure(string message) => new(false, message);
}
