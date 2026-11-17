# FleetLink Manual Test Checklist

## Preconditions

- Use a test database with at least two active branches and Head Office.
- Prepare users for Administrator, Fleet Manager assigned to Branch A, Fleet Manager assigned to Branch B, Head Office Fleet Manager, Dispatcher, Maintenance Officer, and Fuel/Finance Clerk.
- Prepare active and inactive vehicles/drivers, current assignments, unfinished trips, fuel logs, and reportable records.
- Do not use production data for destructive tests.

## Authorization and Navigation

- [ ] Administrator sees Vehicles, Drivers, and Reports as permitted screens; Vehicles and Drivers are read-only.
  - Expected: no add, edit, deactivate, or other mutation buttons are available.
- [ ] Fleet Manager sees Dashboard, Vehicles, Drivers, Assignments, Fuel Monitor, and Reports.
  - Expected: Fleet Manager has full access to the permitted management screens.
- [ ] Dispatcher opens the application.
  - Expected: Assignments, Fuel Monitor, Vehicles, Drivers, and Reports are unavailable.
- [ ] Maintenance Officer opens the application.
  - Expected: Assignments, Fuel Monitor, Vehicles, Drivers, and Reports are unavailable.
- [ ] Fuel/Finance Clerk opens the application.
  - Expected: Assignments, Vehicles, Drivers, Fuel Monitor, and Reports are unavailable.
- [ ] Call reviewed service methods directly while logged in as an unauthorized role.
  - Expected: service authorization rejects the call; UI visibility is not the only protection.

## Vehicle Management

- [ ] Add a vehicle with a plate number already in use.
  - Expected: duplicate plate validation blocks the save.
- [ ] Add a vehicle with zero capacity.
  - Expected: validation blocks the save.
- [ ] Edit a vehicle and lower its odometer below the existing value.
  - Expected: validation blocks the save.
- [ ] Add or edit a vehicle whose registration is expired.
  - Expected: an expiry warning is shown; verify the workflow follows the specified warning/confirmation policy.
- [ ] Deactivate a vehicle with a current assignment.
  - Expected: deactivation is blocked and the current assignment is identified.
- [ ] Deactivate a vehicle involved in an unfinished trip.
  - Expected: deactivation is blocked and the unfinished trip condition is identified.
- [ ] Confirm Vehicle.Status is not changed to `OnTrip` by vehicle, driver, assignment, or fuel-monitor screens.
  - Expected: status remains controlled by the appropriate trip/dispatch workflow.

## Driver Management

- [ ] Add a driver with a license number already in use.
  - Expected: duplicate license validation blocks the save.
- [ ] Add or edit a driver whose license is expired.
  - Expected: an expiry warning is shown; verify the workflow follows the specified warning/confirmation policy.
- [ ] Confirm Driver.Status is not changed to `OnTrip` by vehicle, driver, assignment, or fuel-monitor screens.
  - Expected: status remains controlled by the appropriate trip/dispatch workflow.

## Assignments

- [ ] Assign a driver and vehicle from different branches.
  - Expected: assignment is blocked.
- [ ] Assign a driver who already has a current assignment.
  - Expected: assignment is blocked and the driver is shown as unavailable.
- [ ] Assign a vehicle that already has a current assignment.
  - Expected: assignment is blocked and the vehicle is shown as unavailable.
- [ ] Assign an inactive vehicle or inactive driver.
  - Expected: inactive records do not appear as available choices and direct service calls are blocked.
- [ ] Reassign a vehicle to a different driver.
  - Expected: one transaction creates the new assignment and ends the old assignment; exactly one old row has an EndDate and one new row has a null EndDate.
- [ ] End a current assignment using today as the End Date.
  - Expected: the current row receives EndDate and no vehicle/driver status is changed.
- [ ] End or reassign a vehicle/driver involved in an unfinished trip.
  - Expected: operation is blocked.
- [ ] Use a Start Date more than 30 days in the future.
  - Expected: operation is blocked.
- [ ] Use an expired vehicle registration or driver license.
  - Expected: warning is displayed before confirmation; assignment is not silently accepted.

## Branch Scope

- [ ] Branch Fleet Manager opens the dashboard.
  - Expected: only the assigned branch is shown; no other branch data appears.
- [ ] Branch Fleet Manager opens Vehicles, Drivers, Assignments, Fuel Monitor, and Reports.
  - Expected: all queries and filters return only the assigned branch.
- [ ] Branch Fleet Manager attempts to pass another branch ID to a service.
  - Expected: service rejects the request.
- [ ] Head-office Fleet Manager opens the dashboard and reviewed screens.
  - Expected: all active branches are available and the Branch filter can switch the displayed data.
- [ ] Administrator read-only access is tested for branch-assigned and head-office accounts.
  - Expected: data remains branch-scoped for assigned administrators and mutation controls remain unavailable.

## Fuel Monitor

- [ ] Open Fuel Monitor as Fleet Manager.
  - Expected: the grid is read-only; no add, edit, or delete action is available.
- [ ] Filter by branch as a head-office Fleet Manager.
  - Expected: only the selected branch's fuel logs are shown.
- [ ] Filter by vehicle.
  - Expected: only that vehicle's fuel logs are shown.
- [ ] Filter by date range.
  - Expected: only logs within the inclusive selected date range are shown.
- [ ] Select Abnormal only.
  - Expected: only logs with `IsAbnormal = true` are shown.
- [ ] Load abnormal fuel rows.
  - Expected: abnormal rows are highlighted red.
- [ ] Check the summary strip.
  - Expected: total liters, total cost, and average Km/L match the filtered rows.

## Reports and CSV Export

For each report below, run it for a branch-assigned Fleet Manager and a head-office Fleet Manager where applicable.

- [ ] Vehicle list.
  - Expected: rows are branch-scoped and CSV export contains the displayed columns and rows.
- [ ] Driver list.
  - Expected: rows are branch-scoped and CSV export contains the displayed columns and rows.
- [ ] Current assignments.
  - Expected: only assignments with no EndDate are included; CSV export succeeds.
- [ ] Assignment history.
  - Expected: date-range filtering works and CSV export succeeds.
- [ ] Expiring documents with 30, 60, and 90-day selections.
  - Expected: registration and license records within the selected window are included, sorted by expiry date; CSV export succeeds.
- [ ] Fuel consumption summary per vehicle with a date range.
  - Expected: liters, cost, and average Km/L match the filtered fuel logs; CSV export succeeds.
- [ ] Export every report as CSV.
  - Expected: file is created with headers, escaped values, and all displayed rows.

## Maintenance Officer Review

Use a test database with at least one branch-assigned Maintenance Officer, one branch-assigned Fleet Manager, a Head Office Fleet Manager, an Administrator, a Dispatcher, and a Fuel/Finance Clerk. Do not use production data.

### Authorization and Branch Scope

- [ ] Open Maintenance as a Maintenance Officer.
  - Expected: the Maintenance dashboard and Maintenance screen are available; Start Work and Record Service are available for eligible records.
- [ ] Open Maintenance as a Fleet Manager.
  - Expected: scheduling, editing Scheduled records, and cancelling Scheduled or InProgress records are available; Start Work and Record Service are unavailable.
- [ ] Open Maintenance as an Administrator, Dispatcher, or Fuel/Finance Clerk.
  - Expected: the Maintenance module is unavailable, and direct calls to every maintenance service method throw `UnauthorizedAccessException`.
- [ ] Have a branch-assigned user query maintenance data, reports, dashboard data, vehicle options, and history.
  - Expected: only records whose vehicle belongs to the assigned branch are returned; supplying another branch ID is rejected.
- [ ] Have a Head Office user select each branch and All branches.
  - Expected: the selected branch filter is honored and All branches returns all permitted branch data.

### Scheduling and Editing

- [ ] Schedule maintenance with a past Scheduled Date.
  - Expected: scheduling is blocked with a friendly validation message.
- [ ] Schedule maintenance with Due Odometer less than or equal to the vehicle's current odometer.
  - Expected: scheduling is blocked with a friendly validation message.
- [ ] Schedule the same vehicle, maintenance type, and Scheduled Date twice while the first record is active.
  - Expected: the duplicate is blocked.
- [ ] Start Work, then try to edit the vehicle, type, Scheduled Date, or Due Odometer.
  - Expected: editing is blocked; editing Notes only is allowed.

### Start Work and Record Service

- [ ] Start Work on a vehicle whose status is `OnTrip`.
  - Expected: Start Work is blocked and the vehicle remains OnTrip.
- [ ] Start Work on a vehicle with a Scheduled trip next week.
  - Expected: Start Work succeeds, the vehicle enters maintenance, and a clear warning lists the upcoming trip.
- [ ] Record Service without a completed date.
  - Expected: completion is blocked with a friendly validation message, never a SQL error.
- [ ] Record Service without a service odometer.
  - Expected: completion is blocked with a friendly validation message, never a SQL error.
- [ ] Complete an InProgress record with valid values.
  - Expected: the record becomes `Completed`, the vehicle odometer is updated, and the vehicle returns to `Available` when no other maintenance remains InProgress.
- [ ] Cancel an InProgress record.
  - Expected: the record becomes `Cancelled` and the vehicle is freed to `Available` when no other maintenance remains InProgress.
- [ ] As Fleet Manager, verify Start Work and Record Service cannot be reached through the UI or direct service calls.
  - Expected: controls are unavailable and direct calls throw `UnauthorizedAccessException`.
- [ ] Complete a Preventive record and choose to schedule the next service.
  - Expected: the next preventive form is pre-filled for 3 months after completion and 5,000 km after the service odometer.

### History and Dashboard

- [ ] Open vehicle maintenance history after creating completed and cancelled records.
  - Expected: service count, total cost, last completed date, next due date, and next due odometer match the records.
- [ ] Create overdue and due-soon records, then open the Maintenance dashboard.
  - Expected: the overdue KPI equals the number of blocking records, and the Vehicles to service grid contains the same overdue records with correct urgency values.
- [ ] Open the maintenance cost chart.
  - Expected: it shows the last six months and matches completed maintenance costs for the selected branch.

### Combined Dispatch C7 Scenario

- [ ] Create an overdue `Scheduled` maintenance record for a vehicle, then attempt to dispatch a trip using that vehicle.
  - Expected: dispatch check C7 fails because the maintenance is due by date or odometer; no Dispatch row or status change is committed.
- [ ] Complete the maintenance record, then re-check and dispatch the same otherwise-valid trip.
  - Expected: C7 passes and dispatch succeeds; the vehicle and driver become `OnTrip` and one Dispatch row is created.

## Data Integrity

- [ ] Verify no database tables, columns, indexes, or constraints were changed by the review.
- [ ] Verify timestamps written by application workflows are assigned in C# and are not database-generated.
- [ ] Build the solution after completing the checklist.
  - Expected: build succeeds without errors.

## Dispatcher Shipment and Trip Workflow

- [ ] Create a shipment with a reference number already in use.
  - Expected: creation is blocked with a duplicate-reference validation message.
- [ ] Create a shipment with zero weight.
  - Expected: creation is blocked because weight must be greater than zero.
- [ ] Edit a shipment whose status is not `Pending`.
  - Expected: editing is blocked; only Pending shipments can be edited.
- [ ] Schedule a trip with planned arrival before planned departure.
  - Expected: scheduling is blocked.
- [ ] Schedule a trip using a vehicle and driver from different branches.
  - Expected: scheduling is blocked and both resources must belong to the origin branch.
- [ ] Schedule a trip overlapping another active trip for the same vehicle.
  - Expected: scheduling is blocked and the conflicting vehicle trip is identified.
- [ ] Schedule a trip overlapping another active trip for the same driver.
  - Expected: scheduling is blocked and the conflicting driver trip is identified.
- [ ] Add shipments whose total weight exceeds the trip vehicle capacity.
  - Expected: the shipment assignment is blocked with the capacity rule and totals.
- [ ] Add Cold Chain cargo to a non-Refrigerated vehicle.
  - Expected: assignment is blocked by the cargo compatibility rule.
- [ ] Dispatch a trip with an expired vehicle registration.
  - Expected: dispatch is disabled or rejected and the registration failure explains the planned arrival date.
- [ ] Dispatch a trip with an expired driver license.
  - Expected: dispatch is disabled or rejected and the license failure explains the planned arrival date.
- [ ] Dispatch a trip with no shipments.
  - Expected: dispatch is disabled or rejected because at least one shipment is required.
- [ ] Insert an overdue Scheduled or InProgress Maintenance row for the trip vehicle using SQL, then dispatch.
  - Expected: C7 fails and no Dispatch row or status changes are committed.
- [ ] Dispatch a valid Scheduled trip successfully.
  - Expected: vehicle and driver become `OnTrip`, a Dispatch row is created, and the check summary is saved.
- [ ] Dispatch the same trip a second time.
  - Expected: the operation fails with the already-dispatched message and does not create another Dispatch row.
- [ ] Start a Dispatched trip.
  - Expected: trip becomes `InTransit`, ActualDeparture is populated, and all trip shipments become `InTransit`.
- [ ] Complete an InTransit trip with no distance and with a positive distance.
  - Expected: both are valid; trip becomes `Completed`, ActualArrival is populated after ActualDeparture, shipments become `Delivered`, vehicle becomes `Available`, driver becomes `Active`, and the odometer is unchanged.
- [ ] Complete an InTransit trip with zero or negative distance.
  - Expected: completion is blocked.
- [ ] Cancel a Scheduled trip.
  - Expected: trip becomes `Cancelled`, assigned shipments return to `Pending`, and resources remain available/active.
- [ ] Cancel a Dispatched trip.
  - Expected: trip becomes `Cancelled`, assigned shipments return to `Pending`, vehicle becomes `Available`, and driver becomes `Active`.

## Dispatcher Dashboard and Scope Review

- [ ] Open the Dispatcher dashboard as a branch dispatcher.
  - Expected: Pending Shipments, Scheduled Today, In Transit, Completed Today, Vehicles Available, and Drivers Active show only the assigned branch.
- [ ] Open the Dispatcher dashboard as a head-office dispatcher.
  - Expected: all branches are available through the Branch filter, including an All branches option.
- [ ] Review Trips needing dispatch.
  - Expected: only Scheduled trips departing from now through the next 24 hours are shown; the quick Dispatch button opens the checklist.
- [ ] Review Late trips.
  - Expected: only InTransit trips whose planned arrival has passed are shown.
- [ ] Review Dispatcher screens and services.
  - Expected: no Dispatcher screen or service can add, edit, or deactivate vehicles or drivers.
- [ ] Review status changes in Dispatcher workflows.
  - Expected: forms only request actions; status changes are performed by services, use enum values, and multi-row changes are transactional.
