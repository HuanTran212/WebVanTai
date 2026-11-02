using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using Route = WebVanTai.Models.Route;

namespace WebVanTai.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Roles Seed
            string[] roles = ["Admin", "RouteManager", "TicketInspector", "Customer"];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Users Seed
            var defaultPassword = "P@ssword123";

            var defaultUsers = new[]
            {
                new { Email = "admin@webvantai.vn", FullName = "Quản Trị Viên Hệ Thống", Role = "Admin" },
                new { Email = "routemanager@webvantai.vn", FullName = "Điều Hành Tuyến Xe", Role = "RouteManager" },
                new { Email = "inspector@webvantai.vn", FullName = "Nhân Viên Soát Vé", Role = "TicketInspector" },
                new { Email = "customer@webvantai.vn", FullName = "Khách Hàng Mẫu", Role = "Customer" }
            };

            foreach (var u in defaultUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(u.Email);
                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = u.Email,
                        Email = u.Email,
                        EmailConfirmed = true,
                        FullName = u.FullName,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(user, defaultPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, u.Role);
                    }
                }
            }

            // 3. Stations Seed (6 Real Stations in TP.HCM, Da Lat, Nha Trang, Vung Tau)
            if (!await context.Stations.AnyAsync())
            {
                var stations = new List<Station>
                {
                    new Station
                    {
                        Name = "Bến xe Miền Đông Mới",
                        Province = "Hồ Chí Minh",
                        Address = "Số 501 Hoàng Hữu Nam, Phường Long Bình, TP. Thủ Đức, TP. Hồ Chí Minh",
                        Latitude = 10.875320m,
                        Longitude = 106.829560m
                    },
                    new Station
                    {
                        Name = "Bến xe Miền Tây",
                        Province = "Hồ Chí Minh",
                        Address = "395 Kinh Dương Vương, Phường An Lạc, Quận Bình Tân, TP. Hồ Chí Minh",
                        Latitude = 10.741350m,
                        Longitude = 106.617930m
                    },
                    new Station
                    {
                        Name = "Bến xe Liên tỉnh Đà Lạt",
                        Province = "Lâm Đồng",
                        Address = "Số 01 Tô Hiến Thành, Phường 3, TP. Đà Lạt, Tỉnh Lâm Đồng",
                        Latitude = 11.928730m,
                        Longitude = 108.445210m
                    },
                    new Station
                    {
                        Name = "Bến xe phía Nam Nha Trang",
                        Province = "Khánh Hòa",
                        Address = "Km 6 đường 23/10, Xã Vĩnh Trung, TP. Nha Trang, Tỉnh Khánh Hòa",
                        Latitude = 12.247850m,
                        Longitude = 109.155820m
                    },
                    new Station
                    {
                        Name = "Bến xe phía Bắc Nha Trang",
                        Province = "Khánh Hòa",
                        Address = "Số 01 đường 2/4, Phường Vĩnh Hòa, TP. Nha Trang, Tỉnh Khánh Hòa",
                        Latitude = 12.290530m,
                        Longitude = 109.197460m
                    },
                    new Station
                    {
                        Name = "Bến xe Vũng Tàu",
                        Province = "Bà Rịa - Vũng Tàu",
                        Address = "Số 192 Nam Kỳ Khởi Nghĩa, Phường Thắng Tam, TP. Vũng Tàu, Tỉnh Bà Rịa - Vũng Tàu",
                        Latitude = 10.354150m,
                        Longitude = 107.086250m
                    }
                };

                await context.Stations.AddRangeAsync(stations);
                await context.SaveChangesAsync();
            }

            // 4. Routes and RouteStations Seed (3 Routes)
            if (!await context.Routes.AnyAsync())
            {
                var stMienDong = await context.Stations.FirstAsync(s => s.Name == "Bến xe Miền Đông Mới");
                var stMienTay = await context.Stations.FirstAsync(s => s.Name == "Bến xe Miền Tây");
                var stDaLat = await context.Stations.FirstAsync(s => s.Name == "Bến xe Liên tỉnh Đà Lạt");
                var stNhaTrang = await context.Stations.FirstAsync(s => s.Name == "Bến xe phía Nam Nha Trang");
                var stVungTau = await context.Stations.FirstAsync(s => s.Name == "Bến xe Vũng Tàu");

                var routes = new List<Route>
                {
                    new Route
                    {
                        StartProvince = "Hồ Chí Minh",
                        EndProvince = "Lâm Đồng",
                        DistanceKm = 310,
                        DurationHours = 7.0m,
                        IsActive = true,
                        RouteStations = new List<RouteStation>
                        {
                            new RouteStation { StationId = stMienDong.StationId, StopOrder = 1, StationType = StationType.Pickup, OffsetMinutes = 0 },
                            new RouteStation { StationId = stDaLat.StationId, StopOrder = 2, StationType = StationType.Dropoff, OffsetMinutes = 420 }
                        }
                    },
                    new Route
                    {
                        StartProvince = "Hồ Chí Minh",
                        EndProvince = "Khánh Hòa",
                        DistanceKm = 430,
                        DurationHours = 8.5m,
                        IsActive = true,
                        RouteStations = new List<RouteStation>
                        {
                            new RouteStation { StationId = stMienDong.StationId, StopOrder = 1, StationType = StationType.Pickup, OffsetMinutes = 0 },
                            new RouteStation { StationId = stNhaTrang.StationId, StopOrder = 2, StationType = StationType.Dropoff, OffsetMinutes = 510 }
                        }
                    },
                    new Route
                    {
                        StartProvince = "Hồ Chí Minh",
                        EndProvince = "Bà Rịa - Vũng Tàu",
                        DistanceKm = 105,
                        DurationHours = 2.5m,
                        IsActive = true,
                        RouteStations = new List<RouteStation>
                        {
                            new RouteStation { StationId = stMienTay.StationId, StopOrder = 1, StationType = StationType.Pickup, OffsetMinutes = 0 },
                            new RouteStation { StationId = stVungTau.StationId, StopOrder = 2, StationType = StationType.Dropoff, OffsetMinutes = 150 }
                        }
                    }
                };

                await context.Routes.AddRangeAsync(routes);
                await context.SaveChangesAsync();
            }

            // 5. BusTypes Seed (2 Types: 34-Berth Sleeper Bus and 22-Cabin Limousine)
            if (!await context.BusTypes.AnyAsync())
            {
                var sleeper34Layout = BuildSeatLayout(totalDecks: 2, seatsPerDeck: 17, prefixDeckA: "A", prefixDeckB: "B", cols: 3);
                var limo22Layout = BuildSeatLayout(totalDecks: 2, seatsPerDeck: 11, prefixDeckA: "A", prefixDeckB: "B", cols: 2);

                var busTypes = new List<BusType>
                {
                    new BusType
                    {
                        Name = "Xe giường nằm 34 chỗ 2 tầng",
                        TotalSeats = 34,
                        TotalDecks = 2,
                        SeatLayoutJson = sleeper34Layout
                    },
                    new BusType
                    {
                        Name = "Limousine 22 phòng 2 tầng",
                        TotalSeats = 22,
                        TotalDecks = 2,
                        SeatLayoutJson = limo22Layout
                    }
                };

                await context.BusTypes.AddRangeAsync(busTypes);
                await context.SaveChangesAsync();
            }

            // 6. Buses Seed (4 Buses with realistic license plates)
            if (!await context.Buses.AnyAsync())
            {
                var sleeperType = await context.BusTypes.FirstAsync(bt => bt.Name.Contains("34"));
                var limoType = await context.BusTypes.FirstAsync(bt => bt.Name.Contains("22"));

                var buses = new List<Bus>
                {
                    new Bus
                    {
                        BusTypeId = sleeperType.BusTypeId,
                        LicensePlate = "51B-234.56",
                        Amenities = "Wifi tốc độ cao, Nước uống, Khăn lạnh, Cổng sạc USB",
                        IsActive = true
                    },
                    new Bus
                    {
                        BusTypeId = sleeperType.BusTypeId,
                        LicensePlate = "51B-888.88",
                        Amenities = "Wifi, Nước uống, Đèn đọc sách, Chăn gối diệt khuẩn",
                        IsActive = true
                    },
                    new Bus
                    {
                        BusTypeId = limoType.BusTypeId,
                        LicensePlate = "51B-999.99",
                        Amenities = "Màn hình giải trí riêng, Tai nghe, Ghế massage, Cổng sạc Type-C, Búa thoát hiểm",
                        IsActive = true
                    },
                    new Bus
                    {
                        BusTypeId = limoType.BusTypeId,
                        LicensePlate = "72B-123.45",
                        Amenities = "Rèm che riêng tư, Tủ lạnh mini, Nước suối cao cấp, Cổng sạc nhanh",
                        IsActive = true
                    }
                };

                await context.Buses.AddRangeAsync(buses);
                await context.SaveChangesAsync();
            }

            // 7. Trips Seed (6 Trips departing tomorrow and day after tomorrow)
            if (!await context.Trips.AnyAsync())
            {
                var routeHcmDl = await context.Routes.FirstAsync(r => r.StartProvince == "Hồ Chí Minh" && r.EndProvince == "Lâm Đồng");
                var routeHcmNt = await context.Routes.FirstAsync(r => r.StartProvince == "Hồ Chí Minh" && r.EndProvince == "Khánh Hòa");
                var routeHcmVt = await context.Routes.FirstAsync(r => r.StartProvince == "Hồ Chí Minh" && r.EndProvince == "Bà Rịa - Vũng Tàu");

                var bus1 = await context.Buses.FirstAsync(b => b.LicensePlate == "51B-234.56");
                var bus2 = await context.Buses.FirstAsync(b => b.LicensePlate == "51B-888.88");
                var bus3 = await context.Buses.FirstAsync(b => b.LicensePlate == "51B-999.99");
                var bus4 = await context.Buses.FirstAsync(b => b.LicensePlate == "72B-123.45");

                var tomorrow = DateTime.Today.AddDays(1);
                var dayAfter = DateTime.Today.AddDays(2);

                var trips = new List<Trip>
                {
                    // Trip 1: HCM - Da Lat (Morning tomorrow)
                    new Trip
                    {
                        RouteId = routeHcmDl.RouteId,
                        BusId = bus1.BusId,
                        DepartureTime = tomorrow.AddHours(7),
                        ArrivalTime = tomorrow.AddHours(14),
                        BasePrice = 280000m,
                        Status = TripStatus.Scheduled
                    },
                    // Trip 2: HCM - Da Lat (Night tomorrow - Limousine)
                    new Trip
                    {
                        RouteId = routeHcmDl.RouteId,
                        BusId = bus3.BusId,
                        DepartureTime = tomorrow.AddHours(22).AddMinutes(30),
                        ArrivalTime = dayAfter.AddHours(5).AddMinutes(30),
                        BasePrice = 380000m,
                        Status = TripStatus.Scheduled
                    },
                    // Trip 3: HCM - Nha Trang (Morning tomorrow)
                    new Trip
                    {
                        RouteId = routeHcmNt.RouteId,
                        BusId = bus2.BusId,
                        DepartureTime = tomorrow.AddHours(8),
                        ArrivalTime = tomorrow.AddHours(16).AddMinutes(30),
                        BasePrice = 320000m,
                        Status = TripStatus.Scheduled
                    },
                    // Trip 4: HCM - Nha Trang (Night tomorrow)
                    new Trip
                    {
                        RouteId = routeHcmNt.RouteId,
                        BusId = bus2.BusId,
                        DepartureTime = tomorrow.AddHours(21),
                        ArrivalTime = dayAfter.AddHours(5).AddMinutes(30),
                        BasePrice = 350000m,
                        Status = TripStatus.Scheduled
                    },
                    // Trip 5: HCM - Vung Tau (Morning tomorrow - Limousine)
                    new Trip
                    {
                        RouteId = routeHcmVt.RouteId,
                        BusId = bus4.BusId,
                        DepartureTime = tomorrow.AddHours(9),
                        ArrivalTime = tomorrow.AddHours(11).AddMinutes(30),
                        BasePrice = 190000m,
                        Status = TripStatus.Scheduled
                    },
                    // Trip 6: HCM - Vung Tau (Afternoon day after - Limousine)
                    new Trip
                    {
                        RouteId = routeHcmVt.RouteId,
                        BusId = bus4.BusId,
                        DepartureTime = dayAfter.AddHours(14),
                        ArrivalTime = dayAfter.AddHours(16).AddMinutes(30),
                        BasePrice = 190000m,
                        Status = TripStatus.Scheduled
                    }
                };

                await context.Trips.AddRangeAsync(trips);
                await context.SaveChangesAsync();
            }
        }

        private static string BuildSeatLayout(int totalDecks, int seatsPerDeck, string prefixDeckA, string prefixDeckB, int cols)
        {
            var decks = new List<object>();

            for (int d = 1; d <= totalDecks; d++)
            {
                var prefix = d == 1 ? prefixDeckA : prefixDeckB;
                var deckName = d == 1 ? "Tầng dưới (A)" : "Tầng trên (B)";
                var seats = new List<object>();

                for (int s = 1; s <= seatsPerDeck; s++)
                {
                    int row = ((s - 1) / cols) + 1;
                    int col = ((s - 1) % cols) + 1;
                    string code = $"{prefix}{s:D2}";

                    seats.Add(new
                    {
                        code = code,
                        row = row,
                        col = col,
                        type = "Standard"
                    });
                }

                decks.Add(new
                {
                    deckNumber = d,
                    deckName = deckName,
                    rows = (int)Math.Ceiling((double)seatsPerDeck / cols),
                    cols = cols,
                    seats = seats
                });
            }

            return JsonSerializer.Serialize(new { decks }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
