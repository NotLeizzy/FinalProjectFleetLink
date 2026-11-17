using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record TripStatusOperationResult(bool Success, string? ErrorMessage = null);

public sealed record TripShipmentDetail(
    int ShipmentId,
    string ReferenceNo,
    string? Description,
    decimal WeightKg,
    CargoType CargoType,
    ShipmentStatus Status);

public sealed record TripDetails(
    int TripId,
    TripStatus Status,
    string VehiclePlate,
    string DriverName,
    DateTime PlannedDeparture,
    DateTime PlannedArrival,
    DateTime? ActualDeparture,
    DateTime? ActualArrival,
    decimal? DistanceKm,
    string? DispatchUser,
    DateTime? DispatchedAt,
    string? CheckSummary,
    IReadOnlyList<TripShipmentDetail> Shipments);

public sealed class TripStatusService
{
    public async Task<TripStatusOperationResult> StartAsync(int tripId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var trip = await GetTripQuery(db).SingleOrDefaultAsync(item => item.TripId == tripId);
            if (trip is null)
            {
                return await RollbackAsync(transaction, "The trip was not found or is outside your branch scope.");
            }
            if (trip.Status != (int)TripStatus.Dispatched)
            {
                return await RollbackAsync(transaction, "Only Dispatched trips can be started.");
            }

            trip.Status = (int)TripStatus.InTransit;
            trip.ActualDeparture = DateTime.Now;
            foreach (var shipment in trip.Shipments)
            {
                shipment.Status = (int)ShipmentStatus.InTransit;
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new TripStatusOperationResult(true);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TripStatusOperationResult> CompleteAsync(int tripId, decimal? distanceKm)
    {
        EnsureDispatcherAccess();
        if (distanceKm.HasValue && distanceKm.Value <= 0)
        {
            return new TripStatusOperationResult(false, "Distance must be greater than 0 km when entered.");
        }

        await using var db = DbContextFactory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var trip = await GetTripQuery(db).SingleOrDefaultAsync(item => item.TripId == tripId);
            if (trip is null)
            {
                return await RollbackAsync(transaction, "The trip was not found or is outside your branch scope.");
            }
            if (trip.Status != (int)TripStatus.InTransit)
            {
                return await RollbackAsync(transaction, "Only InTransit trips can be completed.");
            }

            var actualArrival = DateTime.Now;
            if (trip.ActualDeparture.HasValue && actualArrival <= trip.ActualDeparture.Value)
            {
                return await RollbackAsync(transaction, "Actual arrival must be after actual departure.");
            }
            trip.Status = (int)TripStatus.Completed;
            trip.ActualArrival = actualArrival;
            trip.DistanceKm = distanceKm;
            foreach (var shipment in trip.Shipments)
            {
                shipment.Status = (int)ShipmentStatus.Delivered;
            }
            trip.Vehicle.Status = (int)VehicleStatus.Available;
            trip.Driver.Status = (int)DriverStatus.Active;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new TripStatusOperationResult(true);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TripDetails?> GetDetailsAsync(int tripId)
    {
        EnsureDispatcherAccess();
        await using var db = DbContextFactory.Create();
        var trip = await GetTripQuery(db)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TripId == tripId);
        if (trip is null)
        {
            return null;
        }
        return new TripDetails(
            trip.TripId,
            (TripStatus)trip.Status,
            trip.Vehicle.PlateNo,
            trip.Driver.FullName,
            trip.PlannedDeparture,
            trip.PlannedArrival,
            trip.ActualDeparture,
            trip.ActualArrival,
            trip.DistanceKm,
            trip.Dispatch?.DispatchedByUser.FullName,
            trip.Dispatch?.DispatchedAt,
            trip.Dispatch?.CheckSummary,
            trip.Shipments
                .OrderBy(item => item.ReferenceNo)
                .Select(item => new TripShipmentDetail(
                    item.ShipmentId,
                    item.ReferenceNo,
                    item.Description,
                    item.WeightKg,
                    (CargoType)item.CargoType,
                    (ShipmentStatus)item.Status))
                .ToList());
    }

    private static IQueryable<Trip> GetTripQuery(FleetLinkDbContext db) => db.Trips
        .ForCurrentBranch()
        .Include(item => item.Vehicle)
        .Include(item => item.Driver)
        .Include(item => item.Shipments)
        .Include(item => item.Dispatch!)
            .ThenInclude(item => item.DispatchedByUser);

    private static async Task<TripStatusOperationResult> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        string message)
    {
        await transaction.RollbackAsync();
        return new TripStatusOperationResult(false, message);
    }

    private static void EnsureDispatcherAccess()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (Permissions.GetAccessLevel(user.Role, Module.Trips) != AccessLevel.Full)
        {
            throw new UnauthorizedAccessException("Only Dispatchers can update trip status.");
        }
    }
}
