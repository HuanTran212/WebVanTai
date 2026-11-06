# SYSTEM ARCHITECTURE: BUS TICKET & TRANSPORT MANAGEMENT SYSTEM

## 1. Tech Stack & Environment
- **Framework:** ASP.NET Core Web App (Model-View-Controller) on **.NET 10.0**.
- **ORM & Database:** Entity Framework Core 10 (Code-First) with **SQL Server LocalDB** (`Server=(localdb)\\mssqllocaldb;Database=BusTicketDb;Trusted_Connection=True;MultipleActiveResultSets=true`).
- **Authentication & RBAC:** ASP.NET Core Identity (`ApplicationUser : IdentityUser`) with 4 Roles: `Admin`, `RouteManager`, `TicketInspector`, `Customer`.
- **Frontend:** Razor Views (`.cshtml`), Bootstrap 5 (Mobile-First responsive, G-Trans orange theme `#F26522`), Leaflet.js (OpenStreetMap), `html5-qrcode` (Camera scanner).
- **NuGet Packages:** `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.UI`, `QRCoder`.

## 2. Directory & Area Partitioning (Strict Team Boundaries)
- `Models/` & `Data/`: Owned strictly by **Member 1 (Team Lead)**. Other agents MUST NOT modify files here without explicit approval.
- `Controllers/` & `Views/` (Root Area): Customer-facing mobile-first portal.
  - `BookingController` & `SeatLockCleanupService`: Owned by **Member 1**.
  - `HomeController` & `TripsController`: Owned by **Member 2** (Search, advanced filters, Leaflet map).
  - `PaymentController` & `Services/VnPayService.cs`: Owned by **Member 3** (VNPay Sandbox, QRCoder generation, ticket history).
- `Areas/RouteManager/`: Owned by **Member 4** (CRUD for Buses, BusTypes, Stations, Routes, Trips, schedule conflict validation).
- `Areas/TicketInspector/` & `Areas/Admin/`: Owned by **Member 5** (Mobile QR camera scanner, check-in API, Admin revenue dashboard & staff accounts).

## 3. Enums (`Models/Enums/SystemEnums.cs`)
- `StationType`: `Pickup = 0`, `Dropoff = 1`, `Both = 2`
- `TripStatus`: `Scheduled = 0`, `Running = 1`, `Completed = 2`, `Cancelled = 3`
- `BookingStatus`: `Pending = 0`, `Paid = 1`, `Expired = 2`, `Cancelled = 3`
- `TicketStatus`: `Valid = 0`, `CheckedIn = 1`, `Void = 2`

## 4. Database Schema (3NF Compliant - 9 Entities in `Models/`)
1. **`ApplicationUser` (inherits `IdentityUser`)**:
   - `FullName` (nvarchar(100), required), `CreatedAt` (datetime2).
2. **`Station`**:
   - `StationId` (PK), `Name` (nvarchar(150)), `Province` (nvarchar(100)), `Address` (nvarchar(250)), `Latitude` (decimal(9,6)), `Longitude` (decimal(9,6)).
3. **`Route`**:
   - `RouteId` (PK), `StartProvince` (nvarchar(100)), `EndProvince` (nvarchar(100)), `DistanceKm` (int), `DurationHours` (decimal(4,1)), `IsActive` (bool).
4. **`RouteStation`** (Junction table resolving N-N between Route and Station):
   - `Id` (PK), `RouteId` (FK), `StationId` (FK), `StopOrder` (int), `StationType` (enum `StationType`), `OffsetMinutes` (int).
5. **`BusType`**:
   - `BusTypeId` (PK), `Name` (nvarchar(100)), `TotalSeats` (int), `TotalDecks` (int), `SeatLayoutJson` (nvarchar(max) - stores JSON matrix of decks, rows, columns, and seat codes like `A01`, `B01`).
6. **`Bus`**:
   - `BusId` (PK), `BusTypeId` (FK), `LicensePlate` (nvarchar(20), Unique Index), `Amenities` (nvarchar(250)), `IsActive` (bool).
7. **`Trip`**:
   - `TripId` (PK), `RouteId` (FK), `BusId` (FK), `DepartureTime` (datetime2), `ArrivalTime` (datetime2), `BasePrice` (decimal(18,2)), `Status` (enum `TripStatus`).
8. **`Booking`**:
   - `BookingId` (PK), `UserId` (FK to `ApplicationUser`), `TripId` (FK to `Trip`), `PickupStationId` (FK to `Station`), `DropoffStationId` (FK to `Station`), `TotalAmount` (decimal(18,2)), `Status` (enum `BookingStatus`), `CreatedAt` (datetime2), `ExpiredAt` (datetime2), `VnPayTxnRef` (nvarchar(100), nullable).
9. **`Ticket`**:
   - `TicketId` (PK), `BookingId` (FK to `Booking`), `TripId` (FK to `Trip`), `SeatCode` (nvarchar(10)), `PassengerName` (nvarchar(100)), `PassengerPhone` (nvarchar(20)), `QrToken` (nvarchar(256), nullable), `Status` (enum `TicketStatus`), `CheckedInAt` (datetime2, nullable).

## 5. Critical Fluent API Rules (`Data/ApplicationDbContext.cs`)
- **Prevent SQL Server Multiple Cascade Paths:** Set `DeleteBehavior.Restrict` on:
  - `Booking.PickupStation` and `Booking.DropoffStation`
  - `Ticket.Trip` (since `Ticket` already cascades via `Booking`)
  - `RouteStation.Station`
- **Concurrency & Double-Booking Protection:** Create a Unique Filtered Index on `Ticket`:
  `builder.Entity<Ticket>().HasIndex(t => new { t.TripId, t.SeatCode }).IsUnique().HasFilter("[Status] <> 2");` (Blocks duplicate active bookings for the same seat on the same trip while allowing re-booking if a previous ticket was `Void`).
- **Decimal Precision:** Configure `BasePrice` and `TotalAmount` as `decimal(18,2)`, `Latitude` and `Longitude` as `decimal(9,6)`.

## 6. Core Business Rules
- **10-Minute Seat Lock:** Creating a booking sets `Status = BookingStatus.Pending` and `ExpiredAt = DateTime.UtcNow.AddMinutes(10)`. A `BackgroundService` (`SeatLockCleanupService`) runs every 60 seconds to mark expired pending bookings as `Expired` and set their tickets to `Void`.
- **Max Seats:** Maximum 5 seats per booking.
- **Cancellation:** Customers can only cancel paid bookings at least 4 hours before `Trip.DepartureTime`.
- **Bus Conflict Check:** A `BusId` cannot be assigned to two overlapping `Trip` time ranges (`DepartureTime` to `ArrivalTime`).
- **One-Time QR Check-In:** Scanning a valid `QrToken` on the matching `TripId` updates `Ticket.Status = TicketStatus.CheckedIn` and records `CheckedInAt`. Scanning again returns an "Already Checked-In" warning.