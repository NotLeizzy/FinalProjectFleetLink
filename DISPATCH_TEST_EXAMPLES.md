# Dispatch check examples

Each case starts with a branch-scoped trip in `Scheduled` status and uses the current database values when `RunChecksAsync(tripId)` is called.

| Check | Setup | Expected result |
|---|---|---|
| C1 Vehicle availability | Set the trip vehicle status to `Available` and create no overlapping active trip. | Pass: vehicle is available with no overlap. Set status to `OnTrip`, or create an overlapping `Scheduled` trip, to verify failure. |
| C2 Driver availability | Set the assigned driver status to `Active` and create no overlapping active trip. | Pass: driver is active with no overlap. Set status to `OnLeave`, or create an overlapping `InTransit` trip, to verify failure. |
| C3 Registration | Set vehicle registration expiry to `2026-12-02` and planned arrival to `2026-12-02`. | Pass because expiry on the planned arrival date is valid. Set expiry to `2026-12-01` to expect a failure explaining both dates. |
| C4 License | Set driver license expiry to `2026-12-02` and planned arrival to `2026-12-02`. | Pass because expiry on the planned arrival date is valid. Set expiry to `2026-12-01` to expect a failure explaining both dates. |
| C5 Capacity | Add shipments totaling `3,400 kg` to a vehicle with capacity `3,000 kg`. | Fail with a message containing `3,400 kg on the trip` and `3,000 kg`. Reduce the total to `3,000 kg` to verify the boundary passes. |
| C6 Cargo type | Assign Cold Chain cargo to a non-Refrigerated vehicle, then assign Oversized cargo to a Van. | Fail with the applicable vehicle-type requirement. Refrigerated vehicles pass Cold Chain; Wing Van or Trailer Truck passes Oversized. |
| C7 Maintenance | Add a `Scheduled` or `InProgress` maintenance record due on/before planned departure, or with due odometer at/below current odometer. | Fail with the blocking-maintenance message. Complete the maintenance or move it after departure/above the current odometer to verify pass. |
| No shipments | Remove every TripShipment row for the trip. | Fail with `The trip must have at least one shipment.` Add one valid shipment to verify pass. |

## Transaction and authorization examples

- Change a vehicle, driver, expiry, maintenance, or shipment after opening the form, then select **Re-check**; the result must reflect the current database value.
- Make a check fail after the form is displayed, then click **Dispatch**; the transaction must not insert a Dispatch row or change any status.
- Dispatch the same trip concurrently or click dispatch twice; one operation succeeds and the other shows `This trip was already dispatched`.
- Try a trip from another branch as a branch dispatcher; it must not be found or dispatched.
- Try dispatching as a non-Dispatcher role; the service must reject the operation.
