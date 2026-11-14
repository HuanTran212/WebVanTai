using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Areas.TicketManager.ViewModels;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Models.ViewModels;
using WebVanTai.Services;

namespace WebVanTai.Areas.TicketManager.Controllers
{
    /// <summary>
    /// Phân hệ Quản lý vé: bán vé tại quầy (thanh toán tiền mặt), thu tiền đơn online giữ chỗ,
    /// hủy &amp; hoàn tiền vé, danh sách vé toàn hệ thống và in thẻ lên xe.
    /// </summary>
    [Area("TicketManager")]
    [Authorize(Roles = "TicketManager,Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _config;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            IConfiguration config,
            ILogger<DashboardController> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _config = config;
            _logger = logger;
        }

        // ─── GET /TicketManager — Danh sách chuyến sắp chạy + KPI ───────────
        [HttpGet]
        public async Task<IActionResult> Index(string? searchRoute, DateTime? date)
        {
            var todayVn = VietnamTimeHelper.Today;
            var selectedDate = (date ?? todayVn).Date;
            var nextDay = selectedDate.AddDays(1);
            var todayStartUtc = DateTime.Today.ToUniversalTime();

            var tripsQuery = _context.Trips
                .AsNoTracking()
                .Include(t => t.Route)
                .Include(t => t.Bus).ThenInclude(b => b!.BusType)
                .Include(t => t.Bus).ThenInclude(b => b!.Driver)
                .Include(t => t.Bus).ThenInclude(b => b!.Assistant)
                .Include(t => t.Tickets)
                .Where(t => (t.Status == TripStatus.Scheduled || t.Status == TripStatus.Running)
                         && t.DepartureTime >= selectedDate
                         && (date.HasValue ? t.DepartureTime < nextDay : t.DepartureTime >= todayVn));

            if (!string.IsNullOrWhiteSpace(searchRoute))
            {
                var term = searchRoute.Trim();
                tripsQuery = tripsQuery.Where(t =>
                    t.Route != null &&
                    (t.Route.StartProvince.Contains(term) || t.Route.EndProvince.Contains(term) ||
                     (t.Bus != null && t.Bus.LicensePlate.Contains(term))));
            }

            var trips = await tripsQuery
                .OrderBy(t => t.DepartureTime)
                .Take(60)
                .ToListAsync();

            // KPI: vé bán tại quầy hôm nay (đơn do nhân sự xuất hoặc thu tiền mặt)
            var todayCounterBookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Tickets)
                .Where(b => b.Status == BookingStatus.Paid
                         && b.CreatedAt >= todayStartUtc
                         && (b.SoldByUserId != null || b.PaymentMethod == PaymentMethod.Cash))
                .ToListAsync();

            var pendingCount = await _context.Bookings
                .CountAsync(b => b.Status == BookingStatus.Pending && b.ExpiredAt > DateTime.UtcNow);

            var vm = new ManagerDashboardViewModel
            {
                SelectedDate = selectedDate,
                SearchRoute = searchRoute,
                TodayCashRevenue = todayCounterBookings.Where(b => b.PaymentMethod == PaymentMethod.Cash).Sum(b => b.TotalAmount),
                TodayCounterTicketsSold = todayCounterBookings.Sum(b => b.Tickets.Count(t => t.Status != TicketStatus.Void)),
                PendingBookingsCount = pendingCount,
                Trips = trips.Select(t => new ManagerTripItemViewModel
                {
                    TripId = t.TripId,
                    RouteName = $"{t.Route?.StartProvince} → {t.Route?.EndProvince}",
                    DepartureTime = t.DepartureTime,
                    ArrivalTime = t.ArrivalTime,
                    LicensePlate = t.Bus?.LicensePlate ?? "N/A",
                    BusTypeName = t.Bus?.BusType?.Name ?? "Xe khách",
                    DriverName = t.Bus?.Driver?.FullName,
                    AssistantName = t.Bus?.Assistant?.FullName,
                    BasePrice = t.BasePrice,
                    TotalSeats = t.Bus?.BusType?.TotalSeats ?? 0,
                    OccupiedSeats = t.Tickets.Count(tk => tk.Status != TicketStatus.Void),
                    Status = t.Status
                }).ToList()
            };

            return View(vm);
        }

        // ─── GET /TicketManager/Dashboard/Sell?tripId=9 ─────────────────────
        [HttpGet]
        public async Task<IActionResult> Sell(int tripId)
        {
            var trip = await LoadTripForSellAsync(tripId);
            if (trip == null || trip.Route == null || trip.Bus?.BusType == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chuyến xe hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var nowVn = VietnamTimeHelper.Now;
            if (trip.DepartureTime <= nowVn || trip.Status != TripStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Chuyến xe đã qua giờ khởi hành hoặc không còn nhận khách. Không thể bán vé tại quầy.";
                return RedirectToAction(nameof(Index));
            }

            return View(await BuildSellViewModelAsync(trip));
        }

        // ─── POST /TicketManager/Dashboard/Sell — Thu tiền mặt & xuất vé ────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sell(ManagerSellViewModel input)
        {
            var trip = await LoadTripForSellAsync(input.TripId);
            if (trip == null || trip.Route == null || trip.Bus?.BusType == null)
            {
                TempData["ErrorMessage"] = "Chuyến xe không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            var nowVn = VietnamTimeHelper.Now;
            if (trip.DepartureTime <= nowVn || trip.Status != TripStatus.Scheduled)
            {
                ModelState.AddModelError("", "Chuyến xe đã qua giờ khởi hành hoặc không còn nhận khách. Không thể xuất vé cho chuyến trong quá khứ.");
                return View(await BuildSellViewModelAsync(trip, input));
            }

            var seats = (input.SelectedSeats ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant())
                .Distinct()
                .ToList();

            if (seats.Count == 0)
                ModelState.AddModelError(nameof(input.SelectedSeats), "Vui lòng chọn ít nhất 1 ghế.");
            else if (seats.Count > 10)
                ModelState.AddModelError(nameof(input.SelectedSeats), "Mỗi lần bán tại quầy tối đa 10 ghế.");

            var staffId = _userManager.GetUserId(User)!;
            ApplicationUser? linkedCustomer = null;
            if (!string.IsNullOrWhiteSpace(input.LinkedCustomerId))
            {
                linkedCustomer = await _context.Users.FirstOrDefaultAsync(u => u.Id == input.LinkedCustomerId);
            }

            if (!ModelState.IsValid)
            {
                return View(await BuildSellViewModelAsync(trip, input));
            }

            // Khách vãng lai không có tài khoản: đơn ghi nhận dưới tên nhân sự xuất vé
            var bookingUserId = linkedCustomer?.Id ?? staffId;
            var totalAmount = trip.BasePrice * seats.Count;
            int createdBookingId = 0;
            string? conflictError = null;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                try
                {
                    if (trip.DepartureTime <= VietnamTimeHelper.Now || trip.Status != TripStatus.Scheduled)
                    {
                        conflictError = "Chuyến xe đã khởi hành trong lúc thao tác. Không thể xuất vé.";
                        await tx.RollbackAsync();
                        return;
                    }

                    var taken = await _context.Tickets
                        .Where(t => t.TripId == trip.TripId && seats.Contains(t.SeatCode) && t.Status != TicketStatus.Void)
                        .Select(t => t.SeatCode)
                        .ToListAsync();

                    if (taken.Count > 0)
                    {
                        conflictError = $"Ghế [{string.Join(", ", taken)}] vừa được hành khách khác đặt.";
                        await tx.RollbackAsync();
                        return;
                    }

                    var nowUtc = DateTime.UtcNow;
                    var booking = new Booking
                    {
                        UserId = bookingUserId,
                        SoldByUserId = staffId,
                        TripId = trip.TripId,
                        PickupStationId = input.PickupStationId,
                        DropoffStationId = input.DropoffStationId,
                        TotalAmount = totalAmount,
                        Status = BookingStatus.Paid,
                        PaymentMethod = PaymentMethod.Cash,
                        ContactEmail = string.IsNullOrWhiteSpace(input.ContactEmail) ? linkedCustomer?.Email : input.ContactEmail.Trim(),
                        CreatedAt = nowUtc,
                        ExpiredAt = nowUtc.AddMinutes(10)
                    };

                    _context.Bookings.Add(booking);
                    await _context.SaveChangesAsync();

                    foreach (var seatCode in seats)
                    {
                        _context.Tickets.Add(new Ticket
                        {
                            BookingId = booking.BookingId,
                            TripId = trip.TripId,
                            SeatCode = seatCode,
                            PassengerName = input.PassengerName.Trim(),
                            PassengerPhone = input.PassengerPhone.Trim(),
                            Status = TicketStatus.Valid
                        });
                    }
                    await _context.SaveChangesAsync();

                    // Sinh QrToken cho từng vé sau khi có TicketId
                    var savedTickets = await _context.Tickets.Where(t => t.BookingId == booking.BookingId).ToListAsync();
                    foreach (var tk in savedTickets)
                    {
                        tk.QrToken = QrTokenGenerator.Generate(tk.TicketId, trip.TripId, _config);
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    createdBookingId = booking.BookingId;
                }
                catch (DbUpdateException)
                {
                    await tx.RollbackAsync();
                    conflictError = "Ghế vừa được hành khách khác đặt đồng thời. Vui lòng chọn lại.";
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            if (conflictError != null)
            {
                ModelState.AddModelError(string.Empty, conflictError);
                return View(await BuildSellViewModelAsync(trip, input));
            }

            // Gửi email xác nhận vé nếu có địa chỉ email
            if (!string.IsNullOrWhiteSpace(input.ContactEmail) || linkedCustomer != null)
            {
                await _emailService.SendBookingConfirmationEmailAsync(createdBookingId, input.ContactEmail);
            }

            _logger.LogInformation("Quản lý vé xuất vé thành công Booking #{BookingId} ({Seats}) bởi {Staff}",
                createdBookingId, string.Join(",", seats), User.Identity?.Name);

            TempData["SuccessMessage"] = $"Xuất vé thành công cho đơn #{createdBookingId} ({string.Join(", ", seats)}) — đã thu tiền mặt {totalAmount:N0} đ.";
            TempData["PrintBookingId"] = createdBookingId;
            return RedirectToAction(nameof(Bookings), new { search = createdBookingId.ToString() });
        }

        // ─── GET .../LookupCustomer?query=email|sdt ─────────────────────────
        [HttpGet]
        public async Task<IActionResult> LookupCustomer(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new ManagerCustomerLookupViewModel { Found = false });

            var term = query.Trim();
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == term || u.PhoneNumber == term);

            if (user == null)
                return Json(new ManagerCustomerLookupViewModel { Found = false });

            return Json(new ManagerCustomerLookupViewModel
            {
                Found = true,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            });
        }

        // ─── GET /TicketManager/Dashboard/Bookings — Đơn vé & thu tiền ──────
        [HttpGet]
        public async Task<IActionResult> Bookings(string? search, BookingStatus? status, PaymentMethod? paymentMethod)
        {
            var query = _context.Bookings
                .AsNoTracking()
                .Include(b => b.User)
                .Include(b => b.SoldByUser)
                .Include(b => b.Trip).ThenInclude(t => t!.Route)
                .Include(b => b.Trip).ThenInclude(t => t!.Bus)
                .Include(b => b.Tickets)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                if (int.TryParse(term.TrimStart('#'), out var bookingId))
                {
                    query = query.Where(b => b.BookingId == bookingId);
                }
                else
                {
                    query = query.Where(b =>
                        b.Tickets.Any(t => t.PassengerName.Contains(term) || t.PassengerPhone.Contains(term) || t.SeatCode.Contains(term)) ||
                        (b.User != null && (b.User.Email!.Contains(term) || b.User.FullName.Contains(term))) ||
                        (b.ContactEmail != null && b.ContactEmail.Contains(term)));
                }
            }

            if (status.HasValue)
                query = query.Where(b => b.Status == status.Value);

            if (paymentMethod.HasValue)
                query = query.Where(b => b.PaymentMethod == paymentMethod.Value);

            var list = await query
                .OrderByDescending(b => b.CreatedAt)
                .Take(80)
                .ToListAsync();

            var items = list.Select(b =>
            {
                var firstTicket = b.Tickets.FirstOrDefault();
                return new ManagerBookingListItemViewModel
                {
                    BookingId = b.BookingId,
                    RouteName = $"{b.Trip?.Route?.StartProvince} → {b.Trip?.Route?.EndProvince}",
                    DepartureTime = b.Trip?.DepartureTime ?? DateTime.MinValue,
                    LicensePlate = b.Trip?.Bus?.LicensePlate ?? "N/A",
                    PassengerName = firstTicket?.PassengerName ?? b.User?.FullName ?? "Khách lẻ",
                    PassengerPhone = firstTicket?.PassengerPhone ?? b.User?.PhoneNumber ?? "",
                    CustomerEmail = !string.IsNullOrWhiteSpace(b.ContactEmail) ? b.ContactEmail : b.User?.Email,
                    SeatCodes = string.Join(", ", b.Tickets.OrderBy(t => t.SeatCode).Select(t => t.SeatCode)),
                    TotalAmount = b.TotalAmount,
                    RefundedAmount = b.RefundedAmount,
                    Status = b.Status,
                    PaymentMethod = b.PaymentMethod,
                    CreatedAt = b.CreatedAt,
                    ExpiredAt = b.ExpiredAt,
                    SoldByName = b.SoldByUser?.FullName
                };
            }).ToList();

            ViewBag.Search = search;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedPaymentMethod = paymentMethod;
            return View(items);
        }

        // ─── POST .../CollectCash — Thu tiền mặt cho đơn online giữ chỗ ─────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CollectCash(int bookingId)
        {
            var staffId = _userManager.GetUserId(User)!;
            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt vé.";
                return RedirectToAction(nameof(Bookings));
            }

            if (booking.Status != BookingStatus.Pending || DateTime.UtcNow >= booking.ExpiredAt)
            {
                TempData["ErrorMessage"] = "Đơn đặt vé đã hết hạn giữ chỗ hoặc đã được xử lý.";
                return RedirectToAction(nameof(Bookings), new { search = bookingId.ToString() });
            }

            booking.Status = BookingStatus.Paid;
            booking.PaymentMethod = PaymentMethod.Cash;
            booking.SoldByUserId = staffId;

            foreach (var ticket in booking.Tickets.Where(t => t.Status != TicketStatus.Void))
            {
                ticket.QrToken = QrTokenGenerator.Generate(ticket.TicketId, booking.TripId, _config);
            }

            await _context.SaveChangesAsync();
            await _emailService.SendBookingConfirmationEmailAsync(bookingId);

            TempData["SuccessMessage"] = $"Đã thu tiền mặt và xuất vé thành công cho đơn #{bookingId}.";
            TempData["PrintBookingId"] = bookingId;
            return RedirectToAction(nameof(Bookings), new { search = bookingId.ToString() });
        }

        // ─── POST .../CancelBooking — Hủy & ghi nhận hoàn tiền vé ───────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var staffId = _userManager.GetUserId(User)!;
            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt vé.";
                return RedirectToAction(nameof(Bookings));
            }

            if (booking.Status != BookingStatus.Paid)
            {
                TempData["ErrorMessage"] = "Chỉ có thể hủy đơn vé đã thanh toán.";
                return RedirectToAction(nameof(Bookings), new { search = bookingId.ToString() });
            }

            if (booking.Tickets.Any(t => t.Status == TicketStatus.CheckedIn))
            {
                TempData["ErrorMessage"] = $"Không thể hủy đơn #{bookingId} vì đã có hành khách soát vé lên xe.";
                return RedirectToAction(nameof(Bookings), new { search = bookingId.ToString() });
            }

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.RefundedAmount = booking.TotalAmount;
            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Void;
                ticket.QrToken = null;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Quản lý vé hủy Booking #{BookingId} & ghi nhận hoàn {Amount}đ bởi {Staff}.",
                bookingId, booking.RefundedAmount, User.Identity?.Name);

            TempData["SuccessMessage"] =
                $"Đã hủy đơn vé #{bookingId} và ghi nhận hoàn tiền {booking.RefundedAmount:N0} đ cho khách.";
            return RedirectToAction(nameof(Bookings), new { search = bookingId.ToString() });
        }

        // ─── GET /TicketManager/Dashboard/Tickets — Danh sách vé ────────────
        [HttpGet]
        public async Task<IActionResult> Tickets(string? search, TicketStatus? status, DateTime? date)
        {
            var query = _context.Tickets
                .AsNoTracking()
                .Include(t => t.Booking).ThenInclude(b => b!.Trip).ThenInclude(tr => tr!.Route)
                .Include(t => t.Booking).ThenInclude(b => b!.Trip).ThenInclude(tr => tr!.Bus)
                .Include(t => t.Trip)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                if (int.TryParse(term.TrimStart('#'), out var bookingId))
                {
                    query = query.Where(t => t.BookingId == bookingId);
                }
                else
                {
                    query = query.Where(t =>
                        t.PassengerName.Contains(term) ||
                        t.PassengerPhone.Contains(term) ||
                        t.SeatCode.Contains(term));
                }
            }

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (date.HasValue)
            {
                var start = date.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(t => t.Trip!.DepartureTime >= start && t.Trip.DepartureTime < end);
            }

            var tickets = await query
                .OrderByDescending(t => t.Booking!.CreatedAt)
                .Take(100)
                .ToListAsync();

            var items = tickets.Select(t => new ManagerTicketListItemViewModel
            {
                TicketId = t.TicketId,
                BookingId = t.BookingId,
                SeatCode = t.SeatCode,
                PassengerName = t.PassengerName,
                PassengerPhone = t.PassengerPhone,
                RouteName = $"{t.Booking?.Trip?.Route?.StartProvince} → {t.Booking?.Trip?.Route?.EndProvince}",
                DepartureTime = t.Booking?.Trip?.DepartureTime ?? DateTime.MinValue,
                LicensePlate = t.Booking?.Trip?.Bus?.LicensePlate ?? "N/A",
                TicketStatus = t.Status,
                BookingStatus = t.Booking?.Status ?? BookingStatus.Pending,
                PaymentMethod = t.Booking?.PaymentMethod ?? PaymentMethod.VnPay,
                CreatedAt = t.Booking?.CreatedAt ?? DateTime.MinValue
            }).ToList();

            ViewBag.Search = search;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd");
            return View(items);
        }

        // ─── Private helpers ─────────────────────────────────────────────────
        private async Task<Trip?> LoadTripForSellAsync(int tripId)
        {
            return await _context.Trips
                .Include(t => t.Route).ThenInclude(r => r!.RouteStations).ThenInclude(rs => rs.Station)
                .Include(t => t.Bus).ThenInclude(b => b!.BusType)
                .FirstOrDefaultAsync(t => t.TripId == tripId);
        }

        private async Task<ManagerSellViewModel> BuildSellViewModelAsync(Trip trip, ManagerSellViewModel? oldInput = null)
        {
            var occupied = await _context.Tickets
                .Where(t => t.TripId == trip.TripId && t.Status != TicketStatus.Void)
                .Select(t => t.SeatCode)
                .ToListAsync();
            var occupiedSet = new HashSet<string>(occupied, StringComparer.OrdinalIgnoreCase);

            var vm = new ManagerSellViewModel
            {
                TripId = trip.TripId,
                RouteName = $"{trip.Route?.StartProvince} → {trip.Route?.EndProvince}",
                DepartureTime = trip.DepartureTime,
                ArrivalTime = trip.ArrivalTime,
                BasePrice = trip.BasePrice,
                BusTypeName = trip.Bus?.BusType?.Name ?? "Xe khách",
                LicensePlate = trip.Bus?.LicensePlate ?? string.Empty,
                SelectedSeats = oldInput?.SelectedSeats ?? string.Empty,
                PassengerName = oldInput?.PassengerName ?? string.Empty,
                PassengerPhone = oldInput?.PassengerPhone ?? string.Empty,
                ContactEmail = oldInput?.ContactEmail,
                LinkedCustomerId = oldInput?.LinkedCustomerId
            };

            if (!string.IsNullOrWhiteSpace(trip.Bus?.BusType?.SeatLayoutJson))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(trip.Bus.BusType.SeatLayoutJson);
                if (doc.RootElement.TryGetProperty("decks", out var decks) && decks.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var d in decks.EnumerateArray())
                    {
                        var deckVm = new SeatDeckViewModel
                        {
                            DeckNumber = d.TryGetProperty("deckNumber", out var dn) ? dn.GetInt32() : 1,
                            DeckName = d.TryGetProperty("deckName", out var nm) ? nm.GetString() ?? "Tầng" : "Tầng",
                            Rows = d.TryGetProperty("rows", out var rw) ? rw.GetInt32() : 6,
                            Cols = d.TryGetProperty("cols", out var cl) ? cl.GetInt32() : 3
                        };
                        if (d.TryGetProperty("seats", out var seatsJson) && seatsJson.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var s in seatsJson.EnumerateArray())
                            {
                                var code = s.TryGetProperty("code", out var cd) ? cd.GetString() ?? "" : "";
                                deckVm.Seats.Add(new SeatItemViewModel
                                {
                                    Code = code,
                                    Row = s.TryGetProperty("row", out var r) ? r.GetInt32() : 1,
                                    Col = s.TryGetProperty("col", out var c) ? c.GetInt32() : 1,
                                    IsOccupied = occupiedSet.Contains(code)
                                });
                            }
                        }
                        vm.Decks.Add(deckVm);
                    }
                }
            }

            if (trip.Route?.RouteStations != null)
            {
                var ordered = trip.Route.RouteStations.OrderBy(rs => rs.StopOrder).ToList();
                vm.PickupStations = ordered
                    .Where(rs => rs.StationType == StationType.Pickup || rs.StationType == StationType.Both)
                    .Select(rs => new StationOptionViewModel
                    {
                        StationId = rs.StationId,
                        Name = rs.Station?.Name ?? "",
                        Address = rs.Station?.Address ?? ""
                    }).ToList();

                vm.DropoffStations = ordered
                    .Where(rs => rs.StationType == StationType.Dropoff || rs.StationType == StationType.Both)
                    .Select(rs => new StationOptionViewModel
                    {
                        StationId = rs.StationId,
                        Name = rs.Station?.Name ?? "",
                        Address = rs.Station?.Address ?? ""
                    }).ToList();

                vm.PickupStationId = oldInput?.PickupStationId > 0
                    ? oldInput.PickupStationId
                    : vm.PickupStations.FirstOrDefault()?.StationId ?? 0;

                vm.DropoffStationId = oldInput?.DropoffStationId > 0
                    ? oldInput.DropoffStationId
                    : vm.DropoffStations.LastOrDefault()?.StationId ?? 0;
            }

            return vm;
        }
    }
}
