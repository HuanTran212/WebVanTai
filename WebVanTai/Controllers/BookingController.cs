using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Models.ViewModels;

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
            if (!tripId.HasValue || tripId.Value <= 0)
            {
                // Chọn chuyến xe sắp khởi hành đầu tiên nếu không truyền id
                var firstTrip = await _context.Trips
                    .Where(t => t.Status == TripStatus.Scheduled && t.DepartureTime > DateTime.UtcNow)
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

        // GET: /Booking/SelectSeats/{id}
        [HttpGet]
        public async Task<IActionResult> SelectSeats(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.Route)
                    .ThenInclude(r => r!.RouteStations)
                        .ThenInclude(rs => rs.Station)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .FirstOrDefaultAsync(t => t.TripId == id);

            if (trip == null || trip.Bus?.BusType == null || trip.Route == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin chuyến xe hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            // Lấy danh sách mã ghế đã có vé hợp lệ hoặc đang giữ chỗ (không tính Void)
            var occupiedSeats = await _context.Tickets
                .Where(t => t.TripId == id && t.Status != TicketStatus.Void)
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

            // 2. Mở Transaction để xử lý đặt vé & giữ chỗ an toàn đa luồng
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
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
                    reloadedModel.PassengerName = model.PassengerName ?? string.Empty;
                    reloadedModel.PassengerPhone = model.PassengerPhone ?? string.Empty;
                    reloadedModel.PickupStationId = model.PickupStationId;
                    reloadedModel.DropoffStationId = model.DropoffStationId;

                    return View("SelectSeats", reloadedModel);
                }

                // Xác định UserId khách hàng
                string userId;
                if (User.Identity?.IsAuthenticated == true)
                {
                    userId = _userManager.GetUserId(User)!;
                }
                else
                {
                    // Nếu là khách vãng lai (chưa đăng nhập), tìm hoặc tạo tài khoản khách hàng theo SĐT
                    var existingCustomer = await _userManager.FindByNameAsync(model.PassengerPhone)
                        ?? await _userManager.FindByEmailAsync("customer@webvantai.vn");

                    if (existingCustomer != null)
                    {
                        userId = existingCustomer.Id;
                    }
                    else
                    {
                        var guestUser = new ApplicationUser
                        {
                            UserName = model.PassengerPhone,
                            PhoneNumber = model.PassengerPhone,
                            FullName = model.PassengerName,
                            Email = $"{model.PassengerPhone}@customer.webvantai.vn",
                            EmailConfirmed = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        var createResult = await _userManager.CreateAsync(guestUser, "P@ssword123");
                        if (createResult.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(guestUser, "Customer");
                            userId = guestUser.Id;
                        }
                        else
                        {
                            // Fallback tài khoản admin nếu có lỗi tạo
                            var fallbackAdmin = await _userManager.FindByEmailAsync("admin@webvantai.vn");
                            userId = fallbackAdmin?.Id ?? Guid.NewGuid().ToString();
                        }
                    }
                }

                // 3. Tạo Booking với trạng thái Pending và thời hạn giữ chỗ 10 phút
                var now = DateTime.UtcNow;
                var booking = new Booking
                {
                    UserId = userId,
                    TripId = model.TripId,
                    PickupStationId = model.PickupStationId,
                    DropoffStationId = model.DropoffStationId,
                    TotalAmount = trip.BasePrice * seatList.Count,
                    Status = BookingStatus.Pending,
                    CreatedAt = now,
                    ExpiredAt = now.AddMinutes(10) // 10-minute seat lock rule
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // 4. Tạo các bản ghi Ticket cho từng ghế
                foreach (var seatCode in seatList)
                {
                    var ticket = new Ticket
                    {
                        BookingId = booking.BookingId,
                        TripId = model.TripId,
                        SeatCode = seatCode.ToUpperInvariant(),
                        PassengerName = model.PassengerName.Trim(),
                        PassengerPhone = model.PassengerPhone.Trim(),
                        Status = TicketStatus.Valid,
                        QrToken = Guid.NewGuid().ToString("N")
                    };
                    _context.Tickets.Add(ticket);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Đặt vé thành công cho Booking #{BookingId}, {SeatCount} ghế.", booking.BookingId, seatList.Count);

                return RedirectToAction(nameof(Confirmation), new { bookingId = booking.BookingId });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Xung đột trùng ghế khi đặt vé chuyến #{TripId}.", model.TripId);

                ModelState.AddModelError("", "Rất tiếc! Xung đột giữ chỗ ghế xảy ra: Một hành khách khác vừa đặt thành công ghế này trước bạn trong tích tắc. Vui lòng chọn ghế khác.");

                var currentOccupied = await _context.Tickets
                    .Where(t => t.TripId == model.TripId && t.Status != TicketStatus.Void)
                    .Select(t => t.SeatCode)
                    .ToListAsync();

                var reloadedModel = BuildTripBookingViewModel(trip, currentOccupied);
                reloadedModel.PassengerName = model.PassengerName ?? string.Empty;
                reloadedModel.PassengerPhone = model.PassengerPhone ?? string.Empty;
                reloadedModel.PickupStationId = model.PickupStationId;
                reloadedModel.DropoffStationId = model.DropoffStationId;

                return View("SelectSeats", reloadedModel);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi bất ngờ khi xử lý đặt vé.");
                ModelState.AddModelError("", "Có lỗi xảy ra trong quá trình đặt vé. Vui lòng thử lại sau.");

                var currentOccupied = await _context.Tickets
                    .Where(t => t.TripId == model.TripId && t.Status != TicketStatus.Void)
                    .Select(t => t.SeatCode)
                    .ToListAsync();

                var reloadedModel = BuildTripBookingViewModel(trip, currentOccupied);
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

