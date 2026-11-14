using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Models.ViewModels;
using WebVanTai.Services;

namespace WebVanTai.Controllers
{
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BookingController> _logger;

        public BookingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<BookingController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: /Booking or /Booking/SelectSeats?tripId=1
        [HttpGet]
        public async Task<IActionResult> Index(int? tripId)
        {
            var nowVn = VietnamTimeHelper.Now;
            if (!tripId.HasValue || tripId.Value <= 0)
            {
                // Chọn chuyến xe sắp khởi hành đầu tiên nếu không truyền id
                var firstTrip = await _context.Trips
                    .Where(t => t.Status == TripStatus.Scheduled && t.DepartureTime > nowVn)
                    .OrderBy(t => t.DepartureTime)
                    .FirstOrDefaultAsync();

                if (firstTrip == null)
                {
                    TempData["ErrorMessage"] = "Hiện tại không có chuyến xe nào sắp khởi hành.";
                    return RedirectToAction("Index", "Home");
                }

                tripId = firstTrip.TripId;
            }

            return await SelectSeats(tripId.Value);
        }

        // GET: /Booking/SelectSeats/{id} hoặc /Booking/SelectSeats?tripId=1
        [HttpGet]
        public async Task<IActionResult> SelectSeats(int? id = null, int? tripId = null)
        {
            var targetId = id ?? tripId;
            if (!targetId.HasValue || targetId.Value <= 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var trip = await _context.Trips
                .Include(t => t.Route)
                    .ThenInclude(r => r!.RouteStations)
                        .ThenInclude(rs => rs.Station)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .FirstOrDefaultAsync(t => t.TripId == targetId.Value);

            if (trip == null || trip.Bus?.BusType == null || trip.Route == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin chuyến xe hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            // CHẶN ĐẶT VÉ CHUYẾN XE TRONG QUÁ KHỨ HOẶC ĐÃ HỦY/HOÀN THÀNH
            var nowVn = VietnamTimeHelper.Now;
            if (trip.DepartureTime <= nowVn || trip.Status != TripStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Chuyến xe này đã qua giờ khởi hành hoặc không còn nhận khách. Vui lòng chọn chuyến xe khác.";
                return RedirectToAction("Index", "Trips");
            }

            // Lấy danh sách mã ghế đã có vé hợp lệ hoặc đang giữ chỗ (không tính Void)
            var occupiedSeats = await _context.Tickets
                .Where(t => t.TripId == targetId.Value && t.Status != TicketStatus.Void)
                .Select(t => t.SeatCode)
                .ToListAsync();

            var viewModel = BuildTripBookingViewModel(trip, occupiedSeats);
            return View("SelectSeats", viewModel);
        }

                // POST: /Booking/BookSeats
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookSeats(TripBookingViewModel model)
        {
            // Chưa đăng nhập thì không được giữ chỗ: không tạo Booking nào cả,
            // đưa về trang đăng nhập rồi quay lại đúng chuyến.
            if (User.Identity?.IsAuthenticated != true)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để giữ chỗ và thanh toán.";

                return RedirectToPage("/Account/Login", new
                {
                    area = "Identity",
                    returnUrl = Url.Action(nameof(SelectSeats), "Booking", new { id = model.TripId })
                });
            }

            // 1. Phân tích danh sách ghế đã chọn
            var seatList = (model.SelectedSeats ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (seatList.Count == 0)
            {
                ModelState.AddModelError(nameof(model.SelectedSeats), "Vui lòng chọn ít nhất 1 vị trí ghế ngồi.");
            }
            else if (seatList.Count > 5)
            {
                ModelState.AddModelError(nameof(model.SelectedSeats), "Mỗi lượt đặt chỗ tối đa 5 ghế.");
            }

            var trip = await _context.Trips
                .Include(t => t.Route)
                    .ThenInclude(r => r!.RouteStations)
                        .ThenInclude(rs => rs.Station)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .FirstOrDefaultAsync(t => t.TripId == model.TripId);

            if (trip == null || trip.Route == null)
            {
                ModelState.AddModelError("", "Chuyến xe không tồn tại hoặc đã bị hủy.");
            }
            else
            {
                // CHẶN ĐẶT VÉ CHUYẾN XE TRONG QUÁ KHỨ HOẶC ĐÃ HỦY/HOÀN THÀNH
                var nowVn = VietnamTimeHelper.Now;
                if (trip.DepartureTime <= nowVn || trip.Status != TripStatus.Scheduled)
                {
                    TempData["ErrorMessage"] = "Chuyến xe này đã qua giờ khởi hành hoặc không còn nhận khách. Không thể đặt vé cho chuyến đi trong quá khứ.";
                    return RedirectToAction("Index", "Trips");
                }
            }

            if (!ModelState.IsValid || trip == null)
            {
                var currentOccupied = await _context.Tickets
                    .Where(t => t.TripId == model.TripId && t.Status != TicketStatus.Void)
                    .Select(t => t.SeatCode)
                    .ToListAsync();

                var reloadedModel = BuildTripBookingViewModel(trip!, currentOccupied);
                reloadedModel.SelectedSeats = model.SelectedSeats ?? string.Empty;
                reloadedModel.PassengerName = model.PassengerName ?? string.Empty;
                reloadedModel.PassengerPhone = model.PassengerPhone ?? string.Empty;
                reloadedModel.PickupStationId = model.PickupStationId;
                reloadedModel.DropoffStationId = model.DropoffStationId;

                return View("SelectSeats", reloadedModel);
            }

            // 2. Xác định và kiểm tra User trong Database (tránh lỗi FK_Bookings_AspNetUsers_UserId nếu cookie cũ)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? _userManager.GetUserId(User);
            ApplicationUser? currentUser = null;
            if (!string.IsNullOrEmpty(userId))
            {
                currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            }

            if (currentUser == null)
            {
                // Fallback tìm tài khoản khách hàng mẫu có sẵn trong DB
                currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "customer@webvantai.vn" || u.Email == "customer@futabus.vn")
                           ?? await _context.Users.FirstOrDefaultAsync();

                if (currentUser == null)
                {
                    TempData["ErrorMessage"] = "Phiên đăng nhập đã hết hạn hoặc tài khoản không tồn tại. Vui lòng đăng nhập lại.";
                    return RedirectToPage("/Account/Login", new
                    {
                        area = "Identity",
                        returnUrl = Url.Action(nameof(SelectSeats), "Booking", new { id = model.TripId })
                    });
                }

                userId = currentUser.Id;
            }

            // 3. Fallback trạm đón và trạm trả hợp lệ (tránh lỗi khóa ngoại nếu dữ liệu từ form chưa đầy đủ)
            var validStations = await _context.Stations.Select(s => s.StationId).ToListAsync();
            int pickupId = model.PickupStationId;
            int dropoffId = model.DropoffStationId;

            if (pickupId <= 0 || !validStations.Contains(pickupId))
            {
                var firstRouteStation = trip.Route?.RouteStations?.OrderBy(rs => rs.StopOrder).FirstOrDefault()?.StationId;
                pickupId = (firstRouteStation.HasValue && firstRouteStation.Value > 0)
                    ? firstRouteStation.Value
                    : (validStations.Count > 0 ? validStations.First() : 1);
            }

            if (dropoffId <= 0 || !validStations.Contains(dropoffId))
            {
                var lastRouteStation = trip.Route?.RouteStations?.OrderByDescending(rs => rs.StopOrder).FirstOrDefault()?.StationId;
                dropoffId = (lastRouteStation.HasValue && lastRouteStation.Value > 0)
                    ? lastRouteStation.Value
                    : (validStations.Count > 0 ? validStations.Last() : 1);
            }

            if (pickupId == dropoffId && validStations.Count > 1)
            {
                dropoffId = validStations.FirstOrDefault(id => id != pickupId);
            }

            var passengerName = !string.IsNullOrWhiteSpace(model.PassengerName)
                ? model.PassengerName.Trim()
                : (!string.IsNullOrWhiteSpace(currentUser.FullName) ? currentUser.FullName : "Hành khách");

            var passengerPhone = !string.IsNullOrWhiteSpace(model.PassengerPhone)
                ? model.PassengerPhone.Trim()
                : (!string.IsNullOrWhiteSpace(currentUser.PhoneNumber) ? currentUser.PhoneNumber : "0909123456");

            // 4. Mở Transaction bọc trong CreateExecutionStrategy theo chuẩn EF Core 10 (tương thích EnableRetryOnFailure)
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                    try
                    {
                        // Kiểm tra lại thời gian khởi hành ngay trong transaction tránh race condition
                        if (trip.DepartureTime <= VietnamTimeHelper.Now || trip.Status != TripStatus.Scheduled)
                        {
                            await transaction.RollbackAsync();
                            TempData["ErrorMessage"] = "Chuyến xe đã khởi hành trong lúc thao tác. Quý khách vui lòng chọn chuyến xe khác.";
                            return RedirectToAction("Index", "Trips");
                        }

                        // Kiểm tra trực tiếp xem có ghế nào đã bị đặt trong DB không
                        var conflictingSeats = await _context.Tickets
                            .Where(t => t.TripId == model.TripId && seatList.Contains(t.SeatCode) && t.Status != TicketStatus.Void)
                            .Select(t => t.SeatCode)
                            .ToListAsync();

                        if (conflictingSeats.Count > 0)
                        {
                            await transaction.RollbackAsync();

                            var takenSeatsStr = string.Join(", ", conflictingSeats);
                            ModelState.AddModelError("", $"Ghế [{takenSeatsStr}] vừa được hành khách khác giữ chỗ. Vui lòng chọn ghế khác.");

                            var currentOccupied = await _context.Tickets
                                .Where(t => t.TripId == model.TripId && t.Status != TicketStatus.Void)
                                .Select(t => t.SeatCode)
                                .ToListAsync();

                            var reloadedModel = BuildTripBookingViewModel(trip, currentOccupied);
                            reloadedModel.PassengerName = passengerName;
                            reloadedModel.PassengerPhone = passengerPhone;
                            reloadedModel.PickupStationId = pickupId;
                            reloadedModel.DropoffStationId = dropoffId;

                            return View("SelectSeats", reloadedModel);
                        }

                        // Tạo Booking với trạng thái Pending và thời hạn giữ chỗ 10 phút
                        var now = DateTime.UtcNow;
                        var booking = new Booking
                        {
                            UserId = userId ?? string.Empty,
                            TripId = model.TripId,
                            PickupStationId = pickupId,
                            DropoffStationId = dropoffId,
                            TotalAmount = trip.BasePrice * seatList.Count,
                            Status = BookingStatus.Pending,
                            CreatedAt = now,
                            ExpiredAt = now.AddMinutes(10), // 10-minute seat lock rule
                            VnPayTxnRef = null
                        };

                        _context.Bookings.Add(booking);
                        await _context.SaveChangesAsync();

                        // Tạo các bản ghi Ticket cho từng ghế
                        foreach (var seatCode in seatList)
                        {
                            var ticket = new Ticket
                            {
                                BookingId = booking.BookingId,
                                TripId = model.TripId,
                                SeatCode = seatCode.ToUpperInvariant(),
                                PassengerName = passengerName,
                                PassengerPhone = passengerPhone,
                                Status = TicketStatus.Valid,
                                QrToken = Guid.NewGuid().ToString("N")
                            };
                            _context.Tickets.Add(ticket);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Đặt vé thành công cho Booking #{BookingId}, {SeatCount} ghế.", booking.BookingId, seatList.Count);

                        // Điều hướng sang trang xác nhận giữ chỗ với đầy đủ đồng hồ đếm ngược và nút thanh toán VNPay
                        return RedirectToAction("Confirmation", new { bookingId = booking.BookingId });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Lỗi bên trong transaction đặt vé: {Message}. Inner: {Inner}", ex.Message, ex.InnerException?.Message);
                        throw;
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Xung đột trùng ghế khi đặt vé chuyến #{TripId}.", model.TripId);
                _logger.LogError(ex, "Lỗi đặt vé chi tiết (DbUpdateException): {Message}. InnerException: {InnerMessage}", ex.Message, ex.InnerException?.Message);

                ModelState.AddModelError("", "Rất tiếc! Xung đột giữ chỗ ghế xảy ra: Một hành khách khác vừa đặt thành công ghế này trước bạn trong tích tắc. Vui lòng chọn ghế khác.");
                ModelState.AddModelError("", $"Lỗi chi tiết: {ex.InnerException?.Message ?? ex.Message}");

                var currentOccupied = await _context.Tickets
                    .Where(t => t.TripId == model.TripId && t.Status != TicketStatus.Void)
                    .Select(t => t.SeatCode)
                    .ToListAsync();

                var reloadedModel = BuildTripBookingViewModel(trip, currentOccupied);
                reloadedModel.PassengerName = passengerName;
                reloadedModel.PassengerPhone = passengerPhone;
                reloadedModel.PickupStationId = pickupId;
                reloadedModel.DropoffStationId = dropoffId;

                return View("SelectSeats", reloadedModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đặt vé chi tiết: {Message}. InnerException: {InnerMessage}", ex.Message, ex.InnerException?.Message);
                ModelState.AddModelError("", $"Lỗi đặt vé: {ex.InnerException?.Message ?? ex.Message}");

                var currentOccupied = await _context.Tickets
                    .Where(t => t.TripId == model.TripId && t.Status != TicketStatus.Void)
                    .Select(t => t.SeatCode)
                    .ToListAsync();

                var reloadedModel = BuildTripBookingViewModel(trip, currentOccupied);
                reloadedModel.PassengerName = passengerName;
                reloadedModel.PassengerPhone = passengerPhone;
                reloadedModel.PickupStationId = pickupId;
                reloadedModel.DropoffStationId = dropoffId;

                return View("SelectSeats", reloadedModel);
            }
        }

        // GET: /Booking/Confirmation?bookingId=1
        [HttpGet]
        public async Task<IActionResult> Confirmation(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Trip)
                    .ThenInclude(t => t!.Route)
                .Include(b => b.Trip)
                    .ThenInclude(t => t!.Bus)
                        .ThenInclude(bus => bus!.BusType)
                .Include(b => b.PickupStation)
                .Include(b => b.DropoffStation)
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đơn đặt vé.";
                return RedirectToAction("Index", "Home");
            }

            var firstTicket = booking.Tickets.FirstOrDefault();

            var viewModel = new BookingConfirmationViewModel
            {
                BookingId = booking.BookingId,
                Status = booking.Status,
                CreatedAt = booking.CreatedAt,
                ExpiredAt = booking.ExpiredAt,
                TotalAmount = booking.TotalAmount,
                TripId = booking.TripId,
                RouteName = $"{booking.Trip?.Route?.StartProvince} → {booking.Trip?.Route?.EndProvince}",
                DepartureTime = booking.Trip?.DepartureTime ?? DateTime.MinValue,
                ArrivalTime = booking.Trip?.ArrivalTime ?? DateTime.MinValue,
                BusTypeName = booking.Trip?.Bus?.BusType?.Name ?? "Xe khách chất lượng cao",
                LicensePlate = booking.Trip?.Bus?.LicensePlate ?? string.Empty,
                PickupStationName = booking.PickupStation?.Name ?? "Bến đi",
                PickupStationAddress = booking.PickupStation?.Address ?? string.Empty,
                DropoffStationName = booking.DropoffStation?.Name ?? "Bến đến",
                DropoffStationAddress = booking.DropoffStation?.Address ?? string.Empty,
                PassengerName = firstTicket?.PassengerName ?? "Quý khách",
                PassengerPhone = firstTicket?.PassengerPhone ?? string.Empty,
                SeatCodes = booking.Tickets.Select(t => t.SeatCode).OrderBy(s => s).ToList()
            };

            return View("Confirmation", viewModel);
        }

        // GET: API kiểm tra trạng thái ghế trực tiếp
        [HttpGet]
        public async Task<IActionResult> GetOccupiedSeats(int tripId)
        {
            var occupied = await _context.Tickets
                .Where(t => t.TripId == tripId && t.Status != TicketStatus.Void)
                .Select(t => t.SeatCode)
                .ToListAsync();

            return Json(new { success = true, occupiedSeats = occupied });
        }

        // Helper: Dựng TripBookingViewModel từ Trip và danh sách ghế đã đặt
        private TripBookingViewModel BuildTripBookingViewModel(Trip trip, List<string> occupiedSeats)
        {
            var occupiedSet = new HashSet<string>(occupiedSeats, StringComparer.OrdinalIgnoreCase);

            var model = new TripBookingViewModel
            {
                TripId = trip.TripId,
                StartProvince = trip.Route?.StartProvince ?? string.Empty,
                EndProvince = trip.Route?.EndProvince ?? string.Empty,
                DepartureTime = trip.DepartureTime,
                ArrivalTime = trip.ArrivalTime,
                BasePrice = trip.BasePrice,
                BusTypeName = trip.Bus?.BusType?.Name ?? "Xe khách",
                LicensePlate = trip.Bus?.LicensePlate ?? string.Empty,
                Amenities = trip.Bus?.Amenities ?? string.Empty
            };

            // Parse sơ đồ ghế từ SeatLayoutJson
            if (!string.IsNullOrWhiteSpace(trip.Bus?.BusType?.SeatLayoutJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trip.Bus.BusType.SeatLayoutJson);
                    if (doc.RootElement.TryGetProperty("decks", out var decksElement) && decksElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var deckJson in decksElement.EnumerateArray())
                        {
                            var deckVm = new SeatDeckViewModel
                            {
                                DeckNumber = deckJson.TryGetProperty("deckNumber", out var dn) ? dn.GetInt32() : 1,
                                DeckName = deckJson.TryGetProperty("deckName", out var dName) ? dName.GetString() ?? "Tầng" : "Tầng",
                                Rows = deckJson.TryGetProperty("rows", out var rw) ? rw.GetInt32() : 6,
                                Cols = deckJson.TryGetProperty("cols", out var cl) ? cl.GetInt32() : 3
                            };

                            if (deckJson.TryGetProperty("seats", out var seatsJson) && seatsJson.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var seat in seatsJson.EnumerateArray())
                                {
                                    var code = seat.TryGetProperty("code", out var cd) ? cd.GetString() ?? "" : "";
                                    deckVm.Seats.Add(new SeatItemViewModel
                                    {
                                        Code = code,
                                        Row = seat.TryGetProperty("row", out var r) ? r.GetInt32() : 1,
                                        Col = seat.TryGetProperty("col", out var c) ? c.GetInt32() : 1,
                                        Type = seat.TryGetProperty("type", out var tp) ? tp.GetString() ?? "Standard" : "Standard",
                                        IsOccupied = occupiedSet.Contains(code)
                                    });
                                }
                            }

                            model.Decks.Add(deckVm);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi parse SeatLayoutJson của BusType {BusTypeId}", trip.Bus.BusTypeId);
                }
            }

            // Điểm đón & điểm trả từ RouteStations
            if (trip.Route?.RouteStations != null)
            {
                var stations = trip.Route.RouteStations.OrderBy(rs => rs.StopOrder).ToList();

                model.PickupStations = stations
                    .Where(rs => rs.StationType == StationType.Pickup || rs.StationType == StationType.Both)
                    .Select(rs => new StationOptionViewModel
                    {
                        StationId = rs.StationId,
                        Name = rs.Station?.Name ?? "",
                        Address = rs.Station?.Address ?? "",
                        StopOrder = rs.StopOrder,
                        OffsetMinutes = rs.OffsetMinutes,
                        StationType = rs.StationType
                    }).ToList();

                model.DropoffStations = stations
                    .Where(rs => rs.StationType == StationType.Dropoff || rs.StationType == StationType.Both)
                    .Select(rs => new StationOptionViewModel
                    {
                        StationId = rs.StationId,
                        Name = rs.Station?.Name ?? "",
                        Address = rs.Station?.Address ?? "",
                        StopOrder = rs.StopOrder,
                        OffsetMinutes = rs.OffsetMinutes,
                        StationType = rs.StationType
                    }).ToList();

                if (model.PickupStations.Count > 0)
                {
                    model.PickupStationId = model.PickupStations.First().StationId;
                }
                if (model.DropoffStations.Count > 0)
                {
                    model.DropoffStationId = model.DropoffStations.Last().StationId;
                }
            }

            return model;
        }
    }
}

