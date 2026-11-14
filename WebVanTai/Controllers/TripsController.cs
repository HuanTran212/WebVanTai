using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Services;

namespace WebVanTai.Controllers
{
    public class TripsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TripsController> _logger;

        public TripsController(ApplicationDbContext context, ILogger<TripsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Trips
        [HttpGet]
        public async Task<IActionResult> Index(
            string? startProvince,
            string? endProvince,
            DateTime? departureDate,
            string? timeRanges,
            string? busTypeIds,
            decimal? minPrice,
            decimal? maxPrice,
            int? minSeats,
            string? sortBy = "time_asc")
        {
            var filter = ParseFilterParams(startProvince, endProvince, departureDate, timeRanges, busTypeIds, minPrice, maxPrice, minSeats, sortBy);
            var (trips, allBusTypes, priceMin, priceMax) = await GetFilteredTripsAsync(filter);

            var startProvinces = await _context.Routes
                .Where(r => r.IsActive)
                .Select(r => r.StartProvince)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            var endProvinces = await _context.Routes
                .Where(r => r.IsActive)
                .Select(r => r.EndProvince)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            var viewModel = new TripSearchPageViewModel
            {
                Filter = filter,
                Trips = trips,
                AvailableStartProvinces = startProvinces,
                AvailableEndProvinces = endProvinces,
                AvailableBusTypes = allBusTypes,
                GlobalMinPrice = priceMin,
                GlobalMaxPrice = priceMax
            };

            return View(viewModel);
        }

        // GET: /Trips/Filter (AJAX/Fetch endpoint without page reload)
        [HttpGet]
        public async Task<IActionResult> Filter(
            string? startProvince,
            string? endProvince,
            DateTime? departureDate,
            string? timeRanges,
            string? busTypeIds,
            decimal? minPrice,
            decimal? maxPrice,
            int? minSeats,
            string? sortBy = "time_asc")
        {
            var filter = ParseFilterParams(startProvince, endProvince, departureDate, timeRanges, busTypeIds, minPrice, maxPrice, minSeats, sortBy);
            var (trips, _, _, _) = await GetFilteredTripsAsync(filter);

            var partialViewModel = new TripListPartialViewModel
            {
                Trips = trips,
                Filter = filter
            };

            return PartialView("_TripListPartial", partialViewModel);
        }

        // GET: /Trips/FilterTrips (Alias to ensure compatibility with client scripts)
        [HttpGet]
        public Task<IActionResult> FilterTrips(
            string? startProvince,
            string? endProvince,
            DateTime? departureDate,
            string? timeRanges,
            string? busTypeIds,
            decimal? minPrice,
            decimal? maxPrice,
            int? minSeats,
            string? sortBy = "time_asc")
        {
            return Filter(startProvince, endProvince, departureDate, timeRanges, busTypeIds, minPrice, maxPrice, minSeats, sortBy);
        }


        // GET: /Trips/Details/{id} (With Leaflet.js map and markers)
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.Route)
                    .ThenInclude(r => r!.RouteStations)
                        .ThenInclude(rs => rs.Station)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(t => t.TripId == id);

            if (trip == null || trip.Route == null || trip.Bus?.BusType == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin chi tiết của chuyến xe này.";
                return RedirectToAction(nameof(Index));
            }

            var totalSeats = trip.Bus.BusType.TotalSeats;
            var occupiedSeats = trip.Tickets.Count(tk => tk.Status != TicketStatus.Void);
            var availableSeats = Math.Max(0, totalSeats - occupiedSeats);
            var nowVn = VietnamTimeHelper.Now;
            var isPastTrip = trip.DepartureTime <= nowVn;
            var isBookable = !isPastTrip && trip.Status == TripStatus.Scheduled && availableSeats > 0;

            var stationMarkers = trip.Route.RouteStations
                .OrderBy(rs => rs.StopOrder)
                .Where(rs => rs.Station != null)
                .Select(rs => new StationMarkerViewModel
                {
                    StationId = rs.StationId,
                    Name = rs.Station!.Name,
                    Province = rs.Station.Province,
                    Address = rs.Station.Address,
                    Latitude = rs.Station.Latitude,
                    Longitude = rs.Station.Longitude,
                    StopOrder = rs.StopOrder,
                    StationType = rs.StationType,
                    OffsetMinutes = rs.OffsetMinutes,
                    EstimatedTime = trip.DepartureTime.AddMinutes(rs.OffsetMinutes)
                })
                .ToList();

            var viewModel = new TripDetailsViewModel
            {
                TripId = trip.TripId,
                RouteId = trip.RouteId,
                StartProvince = trip.Route.StartProvince,
                EndProvince = trip.Route.EndProvince,
                DistanceKm = trip.Route.DistanceKm,
                DurationHours = trip.Route.DurationHours,
                DepartureTime = trip.DepartureTime,
                ArrivalTime = trip.ArrivalTime,
                BasePrice = trip.BasePrice,
                Status = trip.Status,
                BusId = trip.BusId,
                BusTypeName = trip.Bus.BusType.Name,
                TotalSeats = totalSeats,
                AvailableSeats = availableSeats,
                OccupiedSeats = occupiedSeats,
                LicensePlate = trip.Bus.LicensePlate,
                Amenities = trip.Bus.Amenities,
                Stations = stationMarkers,
                IsBookable = isBookable,
                IsPastTrip = isPastTrip
            };

            return View(viewModel);
        }

        // GET: /Trips/GetMapData/{id} (JSON endpoint for map markers & polylines)
        [HttpGet]
        public async Task<IActionResult> GetMapData(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.Route)
                    .ThenInclude(r => r!.RouteStations)
                        .ThenInclude(rs => rs.Station)
                .FirstOrDefaultAsync(t => t.TripId == id);

            if (trip == null || trip.Route == null)
            {
                return NotFound();
            }

            var stations = trip.Route.RouteStations
                .OrderBy(rs => rs.StopOrder)
                .Where(rs => rs.Station != null)
                .Select(rs => new
                {
                    stationId = rs.StationId,
                    name = rs.Station!.Name,
                    province = rs.Station.Province,
                    address = rs.Station.Address,
                    lat = (double)rs.Station.Latitude,
                    lng = (double)rs.Station.Longitude,
                    stopOrder = rs.StopOrder,
                    stationType = (int)rs.StationType,
                    stationTypeName = rs.StationType.ToString(),
                    offsetMinutes = rs.OffsetMinutes,
                    estimatedTime = trip.DepartureTime.AddMinutes(rs.OffsetMinutes).ToString("HH:mm dd/MM/yyyy")
                })
                .ToList();

            return Json(new
            {
                tripId = trip.TripId,
                startProvince = trip.Route.StartProvince,
                endProvince = trip.Route.EndProvince,
                departureTime = trip.DepartureTime.ToString("HH:mm dd/MM/yyyy"),
                arrivalTime = trip.ArrivalTime.ToString("HH:mm dd/MM/yyyy"),
                stations
            });
        }

        #region Helper Methods

        private static TripFilterParams ParseFilterParams(
            string? startProvince,
            string? endProvince,
            DateTime? departureDate,
            string? timeRanges,
            string? busTypeIds,
            decimal? minPrice,
            decimal? maxPrice,
            int? minSeats,
            string? sortBy)
        {
            var parsedBusTypeIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(busTypeIds))
            {
                parsedBusTypeIds = busTypeIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
            }

            var parsedTimeRanges = new List<string>();
            if (!string.IsNullOrWhiteSpace(timeRanges))
            {
                parsedTimeRanges = timeRanges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => s.ToLowerInvariant())
                    .ToList();
            }

            return new TripFilterParams
            {
                StartProvince = startProvince?.Trim(),
                EndProvince = endProvince?.Trim(),
                DepartureDate = departureDate,
                TimeRanges = parsedTimeRanges,
                BusTypeIds = parsedBusTypeIds,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinSeats = minSeats ?? 0,
                SortBy = string.IsNullOrWhiteSpace(sortBy) ? "time_asc" : sortBy
            };
        }

        private async Task<(List<TripCardViewModel> Trips, List<BusTypeFilterOption> AllBusTypes, decimal PriceMin, decimal PriceMax)>
            GetFilteredTripsAsync(TripFilterParams filter)
        {
            // Lấy danh sách BusType để hiển thị cho checkbox bộ lọc
            var allBusTypes = await _context.BusTypes
                .Select(bt => new BusTypeFilterOption
                {
                    BusTypeId = bt.BusTypeId,
                    Name = bt.Name,
                    TotalSeats = bt.TotalSeats
                })
                .ToListAsync();

            var nowVn = VietnamTimeHelper.Now;
            var query = _context.Trips
                .Include(t => t.Route)
                    .ThenInclude(r => r!.RouteStations)
                        .ThenInclude(rs => rs.Station)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .Include(t => t.Tickets)
                .Where(t => t.Status == TripStatus.Scheduled && t.DepartureTime > nowVn);

            // 1. Lọc Tỉnh Đi
            if (!string.IsNullOrWhiteSpace(filter.StartProvince))
            {
                query = query.Where(t => t.Route != null && t.Route.StartProvince == filter.StartProvince);
            }

            // 2. Lọc Tỉnh Đến
            if (!string.IsNullOrWhiteSpace(filter.EndProvince))
            {
                query = query.Where(t => t.Route != null && t.Route.EndProvince == filter.EndProvince);
            }

            // 3. Lọc Ngày Đi
            if (filter.DepartureDate.HasValue)
            {
                var targetDate = filter.DepartureDate.Value.Date;
                var nextDate = targetDate.AddDays(1);
                query = query.Where(t => t.DepartureTime >= targetDate && t.DepartureTime < nextDate);
            }

            // 4. Lọc Loại Xe (BusType)
            if (filter.BusTypeIds.Any())
            {
                query = query.Where(t => t.Bus != null && filter.BusTypeIds.Contains(t.Bus.BusTypeId));
            }

            // 5. Lọc Khoảng Giá
            if (filter.MinPrice.HasValue)
            {
                query = query.Where(t => t.BasePrice >= filter.MinPrice.Value);
            }
            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(t => t.BasePrice <= filter.MaxPrice.Value);
            }

            // Tải dữ liệu về để xử lý khung giờ và số ghế trống
            var tripsRaw = await query.ToListAsync();

            // Tính giá min/max chung
            decimal globalMin = 150000m;
            decimal globalMax = 600000m;
            if (tripsRaw.Any())
            {
                globalMin = tripsRaw.Min(t => t.BasePrice);
                globalMax = tripsRaw.Max(t => t.BasePrice);
                if (globalMin == globalMax) globalMax += 100000m;
            }

            // 6. Lọc Khung Giờ Xuất Bến
            if (filter.TimeRanges.Any())
            {
                tripsRaw = tripsRaw.Where(t =>
                {
                    var hour = t.DepartureTime.Hour;
                    foreach (var tr in filter.TimeRanges)
                    {
                        if (tr == "early" && hour >= 0 && hour < 6) return true;
                        if (tr == "morning" && hour >= 6 && hour < 12) return true;
                        if (tr == "afternoon" && hour >= 12 && hour < 18) return true;
                        if (tr == "evening" && hour >= 18 && hour <= 23) return true;
                    }
                    return false;
                }).ToList();
            }

            // 7. Chiếu thành ViewModel và tính số ghế trống
            var projectedTrips = tripsRaw.Select(t =>
            {
                var totalSeats = t.Bus?.BusType?.TotalSeats ?? 0;
                var occupiedSeats = t.Tickets.Count(tk => tk.Status != TicketStatus.Void);
                var availableSeats = Math.Max(0, totalSeats - occupiedSeats);

                var sortedStations = t.Route?.RouteStations
                    .OrderBy(rs => rs.StopOrder)
                    .Select(rs => rs.Station?.Name ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList() ?? new List<string>();

                var firstStation = sortedStations.FirstOrDefault() ?? t.Route?.StartProvince ?? "Điểm đón";
                var lastStation = sortedStations.LastOrDefault() ?? t.Route?.EndProvince ?? "Điểm trả";

                var isPast = t.DepartureTime <= nowVn;

                return new TripCardViewModel
                {
                    TripId = t.TripId,
                    RouteId = t.RouteId,
                    StartProvince = t.Route?.StartProvince ?? "",
                    EndProvince = t.Route?.EndProvince ?? "",
                    FirstStationName = firstStation,
                    LastStationName = lastStation,
                    DepartureTime = t.DepartureTime,
                    ArrivalTime = t.ArrivalTime,
                    DurationHours = t.Route?.DurationHours ?? (decimal)(t.ArrivalTime - t.DepartureTime).TotalHours,
                    DistanceKm = t.Route?.DistanceKm ?? 0,
                    BasePrice = t.BasePrice,
                    BusTypeName = t.Bus?.BusType?.Name ?? "Xe giường nằm",
                    BusTypeId = t.Bus?.BusTypeId ?? 0,
                    LicensePlate = t.Bus?.LicensePlate ?? "",
                    Amenities = t.Bus?.Amenities ?? "",
                    TotalSeats = totalSeats,
                    AvailableSeats = availableSeats,
                    OccupiedSeats = occupiedSeats,
                    IsBookable = !isPast && t.Status == TripStatus.Scheduled && availableSeats > 0,
                    IsPastTrip = isPast
                };
            }).ToList();

            // 8. Lọc Số Ghế Trống Còn Lại
            if (filter.MinSeats > 0)
            {
                projectedTrips = projectedTrips.Where(t => t.AvailableSeats >= filter.MinSeats).ToList();
            }

            // 9. Sắp Xếp
            projectedTrips = filter.SortBy switch
            {
                "time_desc" => projectedTrips.OrderByDescending(t => t.DepartureTime).ToList(),
                "price_asc" => projectedTrips.OrderBy(t => t.BasePrice).ToList(),
                "price_desc" => projectedTrips.OrderByDescending(t => t.BasePrice).ToList(),
                "seats_desc" => projectedTrips.OrderByDescending(t => t.AvailableSeats).ToList(),
                _ => projectedTrips.OrderBy(t => t.DepartureTime).ToList() // time_asc default
            };

            return (projectedTrips, allBusTypes, globalMin, globalMax);
        }

        #endregion
    }

    #region ViewModels for Trips (Member 2 Boundary)

    public class TripFilterParams
    {
        public string? StartProvince { get; set; }
        public string? EndProvince { get; set; }
        public DateTime? DepartureDate { get; set; }
        public List<string> TimeRanges { get; set; } = new(); // early, morning, afternoon, evening
        public List<int> BusTypeIds { get; set; } = new();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int MinSeats { get; set; }
        public string SortBy { get; set; } = "time_asc";
    }

    public class BusTypeFilterOption
    {
        public int BusTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TotalSeats { get; set; }
    }

    public class TripCardViewModel
    {
        public int TripId { get; set; }
        public int RouteId { get; set; }
        public string StartProvince { get; set; } = string.Empty;
        public string EndProvince { get; set; } = string.Empty;
        public string FirstStationName { get; set; } = string.Empty;
        public string LastStationName { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal DurationHours { get; set; }
        public int DistanceKm { get; set; }
        public decimal BasePrice { get; set; }
        public string BusTypeName { get; set; } = string.Empty;
        public int BusTypeId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Amenities { get; set; } = string.Empty;
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public int OccupiedSeats { get; set; }
        public bool IsBookable { get; set; } = true;
        public bool IsPastTrip { get; set; } = false;
    }

    public class TripSearchPageViewModel
    {
        public TripFilterParams Filter { get; set; } = new();
        public List<TripCardViewModel> Trips { get; set; } = new();
        public List<string> AvailableStartProvinces { get; set; } = new();
        public List<string> AvailableEndProvinces { get; set; } = new();
        public List<BusTypeFilterOption> AvailableBusTypes { get; set; } = new();
        public decimal GlobalMinPrice { get; set; }
        public decimal GlobalMaxPrice { get; set; }
    }

    public class TripListPartialViewModel
    {
        public List<TripCardViewModel> Trips { get; set; } = new();
        public TripFilterParams Filter { get; set; } = new();
    }

    public class StationMarkerViewModel
    {
        public int StationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int StopOrder { get; set; }
        public StationType StationType { get; set; }
        public int OffsetMinutes { get; set; }
        public DateTime EstimatedTime { get; set; }
    }

    public class TripDetailsViewModel
    {
        public int TripId { get; set; }
        public int RouteId { get; set; }
        public string StartProvince { get; set; } = string.Empty;
        public string EndProvince { get; set; } = string.Empty;
        public int DistanceKm { get; set; }
        public decimal DurationHours { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal BasePrice { get; set; }
        public TripStatus Status { get; set; }
        public int BusId { get; set; }
        public string BusTypeName { get; set; } = string.Empty;
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public int OccupiedSeats { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Amenities { get; set; } = string.Empty;
        public List<StationMarkerViewModel> Stations { get; set; } = new();
        public bool IsBookable { get; set; } = true;
        public bool IsPastTrip { get; set; } = false;
    }

    #endregion
}
