using System.Globalization;
using System.Text.RegularExpressions;
using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed class ReferenceNumberGenerator
{
    private static readonly Regex ReferencePattern = new(
        "^SH-(?<date>\\d{8})-(?<counter>\\d{4})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<string> GenerateAsync(DateTime? now = null)
    {
        var current = now ?? DateTime.Now;
        await using var db = DbContextFactory.Create();
        return await GenerateAsync(db, current);
    }

    public async Task<string> GenerateAsync(FleetLinkDbContext db, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(db);
        var dateText = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var prefix = $"SH-{dateText}-";
        var existingReferences = await db.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.ReferenceNo.StartsWith(prefix))
            .Select(shipment => shipment.ReferenceNo)
            .ToListAsync();
        var highestCounter = existingReferences
            .Select(reference => ParseCounter(reference, dateText))
            .DefaultIfEmpty()
            .Max();
        return Format(dateText, highestCounter + 1);
    }

    public async Task<bool> IsUniqueAsync(string referenceNo, int? excludedShipmentId = null)
    {
        if (string.IsNullOrWhiteSpace(referenceNo))
        {
            return false;
        }

        await using var db = DbContextFactory.Create();
        return !await db.Shipments.AnyAsync(shipment =>
            shipment.ReferenceNo == referenceNo.Trim()
            && (!excludedShipmentId.HasValue || shipment.ShipmentId != excludedShipmentId.Value));
    }

    public static bool IsValidFormat(string referenceNo)
    {
        return !string.IsNullOrWhiteSpace(referenceNo)
            && ReferencePattern.IsMatch(referenceNo.Trim());
    }

    public static string Normalize(string referenceNo)
    {
        var normalized = referenceNo.Trim().ToUpperInvariant();
        if (!IsValidFormat(normalized))
        {
            throw new ArgumentException("Reference number must use the format SH-yyyyMMdd-0001.");
        }
        return normalized;
    }

    private static int ParseCounter(string reference, string dateText)
    {
        var match = ReferencePattern.Match(reference);
        if (!match.Success || match.Groups["date"].Value != dateText)
        {
            return 0;
        }
        return int.TryParse(match.Groups["counter"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var counter)
            ? counter
            : 0;
    }

    private static string Format(string dateText, int counter)
    {
        if (counter > 9999)
        {
            throw new InvalidOperationException("The daily shipment reference counter is exhausted.");
        }
        return $"SH-{dateText}-{counter:0000}";
    }
}
