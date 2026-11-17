using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record AssignmentListItem(
    int AssignmentId,
    int VehicleId,
    string PlateNo,
    VehicleType VehicleType,
    int DriverId,
    string DriverName,
    DateOnly LicenseExpiry,
    DateOnly StartDate,
    DateOnly? EndDate,
    string BranchName);

public sealed record VehicleAssignmentOption(
    int VehicleId,
    string PlateNo,
    VehicleType VehicleType,
    int BranchId,
    string BranchName,
    DateOnly RegistrationExpiry,
    bool IsAvailable,
    string? UnavailableReason);

public sealed record DriverAssignmentOption(
    int DriverId,
    string FullName,
    string LicenseNo,
    int BranchId,
    string BranchName,
    DateOnly LicenseExpiry,
    bool IsAvailable,
    string? UnavailableReason);

public sealed record AssignmentRequest(int VehicleId, int DriverId, DateOnly StartDate);

public sealed record AssignmentOperationResult(
    bool Success,
    bool RequiresConfirmation = false,
    IReadOnlyList<string>? Warnings = null,
    string? ErrorMessage = null,
    int? AssignmentId = null);

public sealed class AssignmentService
{
    public async Task<IReadOnlyList<AssignmentListItem>> GetCurrentAssignmentsAsync(int? branchId)
    {
        EnsureAccess();
        await using var db = DbContextFactory.Create();
        var query = db.VehicleAssignments.AsNoTracking().ForCurrentBranch();
        query = query.Where(assignment => assignment.EndDate == null);
        query = ApplyBranchFilter(query, branchId);
        return await ProjectAssignments(query)
            .OrderBy(item => item.PlateNo)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AssignmentListItem>> GetHistoryAsync(
        int? branchId,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        EnsureAccess();
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            throw new ArgumentException("The start date cannot be after the end date.");
        }

        await using var db = DbContextFactory.Create();
        var query = db.VehicleAssignments.AsNoTracking().ForCurrentBranch();
        query = ApplyBranchFilter(query, branchId);
        if (startDate.HasValue)
        {
            query = query.Where(assignment => assignment.StartDate >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            query = query.Where(assignment => assignment.StartDate <= endDate.Value);
        }
        return await ProjectAssignments(query)
            .OrderByDescending(item => item.StartDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VehicleAssignmentOption>> GetVehicleOptionsAsync(int? branchId)
    {
        EnsureAccess();
        ValidateBranchSelection(branchId);
        await using var db = DbContextFactory.Create();
        var vehicles = db.Vehicles
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(vehicle => vehicle.Status != (int)VehicleStatus.Inactive);
        if (branchId.HasValue)
        {
            vehicles = vehicles.Where(vehicle => vehicle.BranchId == branchId.Value);
        }

        var currentVehicleIds = await db.VehicleAssignments
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(assignment => assignment.EndDate == null)
            .Select(assignment => assignment.VehicleId)
            .ToListAsync();
        var rows = await vehicles
            .OrderBy(vehicle => vehicle.PlateNo)
            .Select(vehicle => new
            {
                vehicle.VehicleId,
                vehicle.PlateNo,
                vehicle.VehicleType,
                vehicle.BranchId,
                BranchName = vehicle.Branch.BranchName,
                vehicle.RegistrationExpiry
            })
            .ToListAsync();
        return rows.Select(vehicle => new VehicleAssignmentOption(
            vehicle.VehicleId,
            vehicle.PlateNo,
            (VehicleType)vehicle.VehicleType,
            vehicle.BranchId,
            vehicle.BranchName,
            vehicle.RegistrationExpiry,
            !currentVehicleIds.Contains(vehicle.VehicleId),
            currentVehicleIds.Contains(vehicle.VehicleId) ? "Already has a current assignment" : null)).ToList();
    }

    public async Task<IReadOnlyList<DriverAssignmentOption>> GetDriverOptionsAsync(int? branchId)
    {
        EnsureAccess();
        ValidateBranchSelection(branchId);
        await using var db = DbContextFactory.Create();
        var drivers = db.Drivers
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(driver => driver.Status != (int)DriverStatus.Inactive);
        if (branchId.HasValue)
        {
            drivers = drivers.Where(driver => driver.BranchId == branchId.Value);
        }

        var currentDriverIds = await db.VehicleAssignments
            .AsNoTracking()
            .ForCurrentBranch()
            .Where(assignment => assignment.EndDate == null)
            .Select(assignment => assignment.DriverId)
            .ToListAsync();
        var rows = await drivers
            .OrderBy(driver => driver.FullName)
            .Select(driver => new
            {
                driver.DriverId,
                driver.FullName,
                driver.LicenseNo,
                driver.BranchId,
                BranchName = driver.Branch.BranchName,
                driver.LicenseExpiry
            })
            .ToListAsync();
        return rows.Select(driver => new DriverAssignmentOption(
            driver.DriverId,
            driver.FullName,
            driver.LicenseNo,
            driver.BranchId,
            driver.BranchName,
            driver.LicenseExpiry,
            !currentDriverIds.Contains(driver.DriverId),
            currentDriverIds.Contains(driver.DriverId) ? "Already has a current assignment" : null)).ToList();
    }

    public async Task<AssignmentOperationResult> AssignAsync(AssignmentRequest request, bool confirmWarnings)
    {
        EnsureAccess();
        ValidateStartDate(request.StartDate);
        await using var db = DbContextFactory.Create();
        var vehicle = await GetScopedVehicleAsync(db, request.VehicleId);
        var driver = await GetScopedDriverAsync(db, request.DriverId);
        if (vehicle is null || driver is null)
        {
            return Failure("The selected vehicle or driver was not found or is outside your branch scope.");
        }
        if (vehicle.Status == (int)VehicleStatus.Inactive || driver.Status == (int)DriverStatus.Inactive)
        {
            return Failure("Inactive vehicles and drivers cannot be assigned.");
        }
        if (vehicle.BranchId != driver.BranchId)
        {
            return Failure("The vehicle and driver must belong to the same branch.");
        }
        if (await HasCurrentVehicleAssignmentAsync(db, vehicle.VehicleId)
            || await HasCurrentDriverAssignmentAsync(db, driver.DriverId))
        {
            return Failure("The selected vehicle or driver already has a current assignment.");
        }

        var warnings = BuildExpiryWarnings(vehicle.RegistrationExpiry, driver.LicenseExpiry, request.StartDate);
        if (warnings.Count > 0 && !confirmWarnings)
        {
            return new AssignmentOperationResult(false, true, warnings);
        }

        db.VehicleAssignments.Add(new VehicleAssignment
        {
            VehicleId = vehicle.VehicleId,
            DriverId = driver.DriverId,
            StartDate = request.StartDate,
            EndDate = null
        });
        await db.SaveChangesAsync();
        return new AssignmentOperationResult(true);
    }

    public async Task<AssignmentOperationResult> ReassignAsync(
        AssignmentRequest request,
        bool confirmWarnings)
    {
        EnsureAccess();
        ValidateStartDate(request.StartDate);
        await using var db = DbContextFactory.Create();
        var vehicle = await GetScopedVehicleAsync(db, request.VehicleId);
        var newDriver = await GetScopedDriverAsync(db, request.DriverId);
        var current = await db.VehicleAssignments
            .ForCurrentBranch()
            .SingleOrDefaultAsync(assignment =>
                assignment.VehicleId == request.VehicleId && assignment.EndDate == null);
        if (vehicle is null || newDriver is null || current is null)
        {
            return Failure("The selected vehicle, driver, or current assignment was not found.");
        }
        if (vehicle.BranchId != newDriver.BranchId)
        {
            return Failure("The vehicle and driver must belong to the same branch.");
        }
        if (newDriver.Status == (int)DriverStatus.Inactive)
        {
            return Failure("An inactive driver cannot be assigned.");
        }
        if (current.DriverId == newDriver.DriverId)
        {
            return Failure("Select a different driver when reassigning.");
        }
        if (await HasCurrentDriverAssignmentAsync(db, newDriver.DriverId))
        {
            return Failure("The selected driver already has a current assignment.");
        }
        await EnsureNoUnfinishedTripAsync(db, vehicle.VehicleId, current.DriverId);

        var warnings = BuildExpiryWarnings(vehicle.RegistrationExpiry, newDriver.LicenseExpiry, request.StartDate);
        if (warnings.Count > 0 && !confirmWarnings)
        {
            return new AssignmentOperationResult(false, true, warnings);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var oldEndDate = request.StartDate.AddDays(-1);
        if (oldEndDate < current.StartDate)
        {
            oldEndDate = DateOnly.FromDateTime(DateTime.Now);
            if (oldEndDate < current.StartDate)
            {
                oldEndDate = current.StartDate;
            }
        }
        current.EndDate = oldEndDate;
        var replacement = new VehicleAssignment
        {
            VehicleId = vehicle.VehicleId,
            DriverId = newDriver.DriverId,
            StartDate = request.StartDate
        };
        db.VehicleAssignments.Add(replacement);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new AssignmentOperationResult(true, AssignmentId: replacement.AssignmentId);
    }

    public async Task<AssignmentOperationResult> EndAssignmentAsync(
        int assignmentId,
        DateOnly endDate)
    {
        EnsureAccess();
        if (endDate > DateOnly.FromDateTime(DateTime.Now))
        {
            return Failure("End Date cannot be in the future.");
        }

        await using var db = DbContextFactory.Create();
        var assignment = await db.VehicleAssignments
            .ForCurrentBranch()
            .SingleOrDefaultAsync(item => item.AssignmentId == assignmentId && item.EndDate == null);
        if (assignment is null)
        {
            return Failure("The current assignment was not found.");
        }
        if (endDate < assignment.StartDate)
        {
            return Failure("End Date cannot be before Start Date.");
        }
        await EnsureNoUnfinishedTripAsync(db, assignment.VehicleId, assignment.DriverId);
        assignment.EndDate = endDate;
        await db.SaveChangesAsync();
        return new AssignmentOperationResult(true);
    }

    private static IQueryable<AssignmentListItem> ProjectAssignments(IQueryable<VehicleAssignment> query)
    {
        return query.Select(assignment => new AssignmentListItem(
            assignment.AssignmentId,
            assignment.VehicleId,
            assignment.Vehicle.PlateNo,
            (VehicleType)assignment.Vehicle.VehicleType,
            assignment.DriverId,
            assignment.Driver.FullName,
            assignment.Driver.LicenseExpiry,
            assignment.StartDate,
            assignment.EndDate,
            assignment.Vehicle.Branch.BranchName));
    }

    private static IQueryable<VehicleAssignment> ApplyBranchFilter(
        IQueryable<VehicleAssignment> query,
        int? branchId)
    {
        ValidateBranchSelection(branchId);
        return branchId.HasValue
            ? query.Where(assignment => assignment.Vehicle.BranchId == branchId.Value)
            : query;
    }

    private static async Task<Vehicle?> GetScopedVehicleAsync(FleetLinkDbContext db, int vehicleId)
    {
        return await db.Vehicles.ForCurrentBranch().SingleOrDefaultAsync(vehicle => vehicle.VehicleId == vehicleId);
    }

    private static async Task<Driver?> GetScopedDriverAsync(FleetLinkDbContext db, int driverId)
    {
        return await db.Drivers.ForCurrentBranch().SingleOrDefaultAsync(driver => driver.DriverId == driverId);
    }

    private static async Task<bool> HasCurrentVehicleAssignmentAsync(FleetLinkDbContext db, int vehicleId)
    {
        return await db.VehicleAssignments.ForCurrentBranch()
            .AnyAsync(assignment => assignment.VehicleId == vehicleId && assignment.EndDate == null);
    }

    private static async Task<bool> HasCurrentDriverAssignmentAsync(FleetLinkDbContext db, int driverId)
    {
        return await db.VehicleAssignments.ForCurrentBranch()
            .AnyAsync(assignment => assignment.DriverId == driverId && assignment.EndDate == null);
    }

    private static async Task EnsureNoUnfinishedTripAsync(FleetLinkDbContext db, int vehicleId, int driverId)
    {
        var unfinishedStatuses = new[]
        {
            (int)TripStatus.Scheduled,
            (int)TripStatus.Dispatched,
            (int)TripStatus.InTransit
        };
        var hasTrip = await db.Trips.AnyAsync(trip =>
            unfinishedStatuses.Contains(trip.Status)
            && (trip.VehicleId == vehicleId || trip.DriverId == driverId));
        if (hasTrip)
        {
            throw new InvalidOperationException("The assignment cannot be ended while the vehicle or driver has an unfinished trip.");
        }
    }

    private static List<string> BuildExpiryWarnings(
        DateOnly registrationExpiry,
        DateOnly licenseExpiry,
        DateOnly startDate)
    {
        var warnings = new List<string>();
        if (registrationExpiry < startDate)
        {
            warnings.Add("The vehicle registration is expired or expires before the Start Date.");
        }
        if (licenseExpiry < startDate)
        {
            warnings.Add("The driver's license is expired or expires before the Start Date.");
        }
        return warnings;
    }

    private static void ValidateStartDate(DateOnly startDate)
    {
        var latestAllowed = DateOnly.FromDateTime(DateTime.Now).AddDays(30);
        if (startDate > latestAllowed)
        {
            throw new ArgumentException("Start Date cannot be more than 30 days in the future.");
        }
    }

    private static void ValidateBranchSelection(int? branchId)
    {
        if (!Session.IsHeadOffice && branchId != Session.CurrentUser!.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }
    }

    private static AssignmentOperationResult Failure(string message) => new(false, ErrorMessage: message);

    private static void EnsureAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Assignments) != AccessLevel.Full)
        {
            throw new UnauthorizedAccessException("Only Fleet Managers can manage assignments.");
        }
    }
}
