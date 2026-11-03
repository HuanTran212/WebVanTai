IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [BusTypes] (
        [BusTypeId] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [TotalSeats] int NOT NULL,
        [TotalDecks] int NOT NULL,
        [SeatLayoutJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_BusTypes] PRIMARY KEY ([BusTypeId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [Routes] (
        [RouteId] int NOT NULL IDENTITY,
        [StartProvince] nvarchar(100) NOT NULL,
        [EndProvince] nvarchar(100) NOT NULL,
        [DistanceKm] int NOT NULL,
        [DurationHours] decimal(4,1) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Routes] PRIMARY KEY ([RouteId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [Stations] (
        [StationId] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Province] nvarchar(100) NOT NULL,
        [Address] nvarchar(250) NOT NULL,
        [Latitude] decimal(9,6) NOT NULL,
        [Longitude] decimal(9,6) NOT NULL,
        CONSTRAINT [PK_Stations] PRIMARY KEY ([StationId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [Buses] (
        [BusId] int NOT NULL IDENTITY,
        [BusTypeId] int NOT NULL,
        [LicensePlate] nvarchar(20) NOT NULL,
        [Amenities] nvarchar(250) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Buses] PRIMARY KEY ([BusId]),
        CONSTRAINT [FK_Buses_BusTypes_BusTypeId] FOREIGN KEY ([BusTypeId]) REFERENCES [BusTypes] ([BusTypeId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [RouteStations] (
        [Id] int NOT NULL IDENTITY,
        [RouteId] int NOT NULL,
        [StationId] int NOT NULL,
        [StopOrder] int NOT NULL,
        [StationType] int NOT NULL,
        [OffsetMinutes] int NOT NULL,
        CONSTRAINT [PK_RouteStations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RouteStations_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([RouteId]) ON DELETE CASCADE,
        CONSTRAINT [FK_RouteStations_Stations_StationId] FOREIGN KEY ([StationId]) REFERENCES [Stations] ([StationId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [Trips] (
        [TripId] int NOT NULL IDENTITY,
        [RouteId] int NOT NULL,
        [BusId] int NOT NULL,
        [DepartureTime] datetime2 NOT NULL,
        [ArrivalTime] datetime2 NOT NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_Trips] PRIMARY KEY ([TripId]),
        CONSTRAINT [FK_Trips_Buses_BusId] FOREIGN KEY ([BusId]) REFERENCES [Buses] ([BusId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Trips_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([RouteId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [Bookings] (
        [BookingId] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [TripId] int NOT NULL,
        [PickupStationId] int NOT NULL,
        [DropoffStationId] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiredAt] datetime2 NOT NULL,
        [VnPayTxnRef] nvarchar(100) NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([BookingId]),
        CONSTRAINT [FK_Bookings_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Stations_DropoffStationId] FOREIGN KEY ([DropoffStationId]) REFERENCES [Stations] ([StationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Stations_PickupStationId] FOREIGN KEY ([PickupStationId]) REFERENCES [Stations] ([StationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Trips_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trips] ([TripId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [TicketId] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [TripId] int NOT NULL,
        [SeatCode] nvarchar(10) NOT NULL,
        [PassengerName] nvarchar(100) NOT NULL,
        [PassengerPhone] nvarchar(20) NOT NULL,
        [QrToken] nvarchar(256) NULL,
        [Status] int NOT NULL,
        [CheckedInAt] datetime2 NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([TicketId]),
        CONSTRAINT [FK_Tickets_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([BookingId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Tickets_Trips_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trips] ([TripId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_DropoffStationId] ON [Bookings] ([DropoffStationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_PickupStationId] ON [Bookings] ([PickupStationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_TripId] ON [Bookings] ([TripId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_UserId] ON [Bookings] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Buses_BusTypeId] ON [Buses] ([BusTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Buses_LicensePlate] ON [Buses] ([LicensePlate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RouteStations_RouteId] ON [RouteStations] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RouteStations_StationId] ON [RouteStations] ([StationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_BookingId] ON [Tickets] ([BookingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Tickets_TripId_SeatCode] ON [Tickets] ([TripId], [SeatCode]) WHERE [Status] <> 2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Trips_BusId] ON [Trips] ([BusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Trips_RouteId] ON [Trips] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080952_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102080952_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

