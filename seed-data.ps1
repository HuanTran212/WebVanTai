# Script Thêm dữ liệu phong phú cho BusTicketDb (WebVanTai)
# Tiêu chuẩn: 
# - Không sửa source code C# / project files
# - Bổ sung các bến xe lớn tại nhiều tỉnh thành: Hà Nội, Đà Nẵng, Cần Thơ, Bình Thuận (Phan Thiết), Gia Lai (Pleiku), Đắk Lắk (Buôn Ma Thuột), Kiên Giang (Rạch Giá), An Giang (Châu Đốc), Huế, Quảng Ninh, Hải Phòng,...
# - Bổ sung tuyến xe liên tỉnh kết nối đa dạng vùng miền
# - Bổ sung trạm dừng (RouteStations) đón/trả ở các trạm trung chuyển / bến xe
# - Bổ sung 200+ xe khách với biển số đa dạng tỉnh thành (51B, 29B, 43B, 65B, 49B, 79B, 72B, 86B, 47B, 81B,...)
# - Bổ sung hàng trăm tài xế, phụ xe chuyên nghiệp
# - Bổ sung 200+ chuyến xe (Trips) giờ giấc thực tế (sáng, trưa, chiều, đêm, rạng sáng) cho các ngày tới
# - Bổ sung các đơn đặt vé (Bookings) và vé (Tickets) chân thực phục vụ tra cứu chatbot & thống kê

param (
    [string]$Server = "(localdb)\mssqllocaldb",
    [string]$Database = "BusTicketDb"
)

$cs = "Server=$Server;Database=$Database;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
$conn = New-Object System.Data.SqlClient.SqlConnection($cs);
$conn.Open();

Write-Host ">>> Đang kết nối CSDL $Database..." -ForegroundColor Cyan

function Execute-SqlNonQuery($cmdText) {
    $cmd = $conn.CreateCommand();
    $cmd.CommandText = $cmdText;
    return $cmd.ExecuteNonQuery();
}

function Execute-SqlScalar($cmdText) {
    $cmd = $conn.CreateCommand();
    $cmd.CommandText = $cmdText;
    return $cmd.ExecuteScalar();
}

# =========================================================================
# 1. THÊM BẾN XE MỚI (STATIONS)
# =========================================================================
Write-Host ">>> 1. Đang thêm các Bến Xe / Trạm Dừng trên toàn quốc..." -ForegroundColor Yellow

$stationsData = @(
    # Hà Nội
    @{ Name="Bến xe Mỹ Đình"; Province="Hà Nội"; Address="Số 20 Phạm Hùng, Phường Mỹ Đình 2, Quận Nam Từ Liêm, Hà Nội"; Lat=21.028511; Lng=105.777718 },
    @{ Name="Bến xe Giáp Bát"; Province="Hà Nội"; Address="Km 6 Đường Giải Phóng, Phường Giáp Bát, Quận Hoàng Mai, Hà Nội"; Lat=20.981442; Lng=105.841520 },
    @{ Name="Bến xe Nước Ngầm"; Province="Hà Nội"; Address="Số 01 Ngọc Hồi, Phường Hoàng Liệt, Quận Hoàng Mai, Hà Nội"; Lat=20.966231; Lng=105.839845 },
    
    # Đà Nẵng
    @{ Name="Bến xe Trung tâm Đà Nẵng"; Province="Đà Nẵng"; Address="Số 159 Tôn Đức Thắng, Phường Hòa Minh, Quận Liên Chiểu, Đà Nẵng"; Lat=16.059210; Lng=108.172430 },
    @{ Name="Trạm trung chuyển Ngũ Hành Sơn"; Province="Đà Nẵng"; Address="Số 28 Hồ Xuân Hương, Phường Mỹ An, Quận Ngũ Hành Sơn, Đà Nẵng"; Lat=16.035410; Lng=108.243510 },

    # Cần Thơ & Miền Tây
    @{ Name="Bến xe Trung tâm Cần Thơ"; Province="Cần Thơ"; Address="Khu Đô Thị Nam Cần Thơ, Quốc Lộ 1A, Phường Hưng Thạnh, Quận Cái Răng, Cần Thơ"; Lat=10.005120; Lng=105.772340 },
    @{ Name="Bến xe Rạch Sỏi - Rạch Giá"; Province="Kiên Giang"; Address="Số 168 Mai Thị Hồng Hạnh, Phường Rạch Sỏi, TP. Rạch Giá, Tỉnh Kiên Giang"; Lat=9.962310; Lng=105.143210 },
    @{ Name="Bến xe Châu Đốc"; Province="An Giang"; Address="Đường Tôn Đức Thắng, Phường Vĩnh Mỹ, TP. Châu Đốc, Tỉnh An Giang"; Lat=10.697410; Lng=105.121540 },
    @{ Name="Bến xe Vĩnh Long"; Province="Vĩnh Long"; Address="Đinh Tiên Hoàng, Phường 8, TP. Vĩnh Long, Tỉnh Vĩnh Long"; Lat=10.231210; Lng=105.954320 },
    @{ Name="Bến xe Tiền Giang"; Province="Tiền Giang"; Address="Ấp 2, Xã Đạo Thạnh, TP. Mỹ Tho, Tỉnh Tiền Giang"; Lat=10.364520; Lng=106.354120 },

    # Tây Nguyên
    @{ Name="Bến xe Phía Nam Buôn Ma Thuột"; Province="Đắk Lắk"; Address="Số 71 Nguyễn Tất Thành, TP. Buôn Ma Thuột, Tỉnh Đắk Lắk"; Lat=12.678410; Lng=108.045610 },
    @{ Name="Bến xe Đức Long Gia Lai"; Province="Gia Lai"; Address="Số 43 Lý Nam Đế, Phường Trà Bá, TP. Pleiku, Tỉnh Gia Lai"; Lat=13.965420; Lng=108.012430 },
    @{ Name="Bến xe Bảo Lộc"; Province="Lâm Đồng"; Address="Số 280 Trần Phú, Phường 1, TP. Bảo Lộc, Tỉnh Lâm Đồng"; Lat=11.543210; Lng=107.809120 },

    # Duyên hải Nam Trung Bộ & Đông Nam Bộ
    @{ Name="Bến xe Phan Thiết"; Province="Bình Thuận"; Address="Số 01 Từ Văn Tư, Phường Phú Trinh, TP. Phan Thiết, Tỉnh Bình Thuận"; Lat=10.932410; Lng=108.102340 },
    @{ Name="Bến xe Mũi Né"; Province="Bình Thuận"; Address="Huỳnh Thúc Kháng, Phường Mũi Né, TP. Phan Thiết, Tỉnh Bình Thuận"; Lat=10.945210; Lng=108.287410 },
    @{ Name="Bến xe Quy Nhơn"; Province="Bình Định"; Address="Số 71 Tây Sơn, Phường Ghềnh Ráng, TP. Quy Nhơn, Tỉnh Bình Định"; Lat=13.754120; Lng=109.213450 },
    @{ Name="Bến xe Cam Ranh"; Province="Khánh Hòa"; Address="Số 01 Lê Duẩn, Phường Cam Lộc, TP. Cam Ranh, Tỉnh Khánh Hòa"; Lat=11.921340; Lng=109.152340 },
    @{ Name="Bến xe Biên Hòa"; Province="Đồng Nai"; Address="Số 04 Nguyễn Ái Quốc, Phường Quang Vinh, TP. Biên Hòa, Tỉnh Đồng Nai"; Lat=10.957810; Lng=106.818450 },
    @{ Name="Bến xe Bình Dương"; Province="Bình Dương"; Address="Đường 30/4, Phường Phú Hòa, TP. Thủ Dầu Một, Tỉnh Bình Dương"; Lat=10.978450; Lng=106.669810 },
    @{ Name="Bến xe Tây Ninh"; Province="Tây Ninh"; Address="Số 136 Trưng Nữ Vương, Khu phố 1, Phường 2, TP. Tây Ninh, Tỉnh Tây Ninh"; Lat=11.312450; Lng=106.098710 },

    # Miền Bắc & Bắc Trung Bộ
    @{ Name="Bến xe Phía Nam Huế"; Province="Thừa Thiên Huế"; Address="Số 57 An Dương Vương, Phường An Đông, TP. Huế, Thừa Thiên Huế"; Lat=16.448710; Lng=107.601240 },
    @{ Name="Bến xe Vinh"; Province="Nghệ An"; Address="Số 77 Lê Lợi, Phường Lê Lợi, TP. Vinh, Tỉnh Nghệ An"; Lat=18.679120; Lng=105.674310 },
    @{ Name="Bến xe Bãi Cháy"; Province="Quảng Ninh"; Address="Số 17 đường 279, Phường Bãi Cháy, TP. Hạ Long, Tỉnh Quảng Ninh"; Lat=20.971240; Lng=107.025410 },
    @{ Name="Bến xe Cầu Rào"; Province="Hải Phòng"; Address="Số 01 Thiên Lôi, Phường Đằng Giang, Quận Ngô Quyền, Hải Phòng"; Lat=20.834120; Lng=106.698710 },
    @{ Name="Bến xe Sa Pa"; Province="Lào Cai"; Address="Đường Điện Biên Phủ, Thị xã Sa Pa, Tỉnh Lào Cai"; Lat=22.336410; Lng=103.843120 }
)

foreach ($s in $stationsData) {
    $check = Execute-SqlScalar "SELECT COUNT(*) FROM Stations WHERE Name = N'$($s.Name)'";
    if ($check -eq 0) {
        $insertSql = @"
INSERT INTO Stations (Name, Province, Address, Latitude, Longitude)
VALUES (N'$($s.Name)', N'$($s.Province)', N'$($s.Address)', $($s.Lat), $($s.Lng));
"@
        Execute-SqlNonQuery $insertSql | Out-Null
    }
}
$totalStations = Execute-SqlScalar "SELECT COUNT(*) FROM Stations";
Write-Host "   -> Hiện có tổng cộng: $totalStations Bến xe / Trạm dừng" -ForegroundColor Green

# =========================================================================
# 2. THÊM CÁC TUYẾN XE MỚI (ROUTES) & TRẠM DỪNG (ROUTESTATIONS)
# =========================================================================
Write-Host ">>> 2. Đang thêm Tuyến xe liên tỉnh kết nối Bắc - Trung - Nam..." -ForegroundColor Yellow

$routesData = @(
    # Tuyến từ Hồ Chí Minh
    @{ Start="Hồ Chí Minh"; End="Cần Thơ"; Km=165; Hours=3.5; Stations=@("Bến xe Miền Tây", "Bến xe Tiền Giang", "Bến xe Vĩnh Long", "Bến xe Trung tâm Cần Thơ") },
    @{ Start="Hồ Chí Minh"; End="Bình Thuận"; Km=200; Hours=4.0; Stations=@("Bến xe Miền Đông Mới", "Bến xe Biên Hòa", "Bến xe Phan Thiết", "Bến xe Mũi Né") },
    @{ Start="Hồ Chí Minh"; End="Đà Nẵng"; Km=950; Hours=18.0; Stations=@("Bến xe Miền Đông Mới", "Bến xe Phan Thiết", "Bến xe Cam Ranh", "Bến xe phía Nam Nha Trang", "Bến xe Quy Nhơn", "Bến xe Trung tâm Đà Nẵng") },
    @{ Start="Hồ Chí Minh"; End="Đắk Lắk"; Km=350; Hours=8.0; Stations=@("Bến xe Miền Đông Mới", "Bến xe Bình Dương", "Bến xe Phía Nam Buôn Ma Thuột") },
    @{ Start="Hồ Chí Minh"; End="Gia Lai"; Km=530; Hours=11.5; Stations=@("Bến xe Miền Đông Mới", "Bến xe Phía Nam Buôn Ma Thuột", "Bến xe Đức Long Gia Lai") },
    @{ Start="Hồ Chí Minh"; End="Kiên Giang"; Km=250; Hours=5.5; Stations=@("Bến xe Miền Tây", "Bến xe Tiền Giang", "Bến xe Trung tâm Cần Thơ", "Bến xe Rạch Sỏi - Rạch Giá") },
    @{ Start="Hồ Chí Minh"; End="An Giang"; Km=240; Hours=5.5; Stations=@("Bến xe Miền Tây", "Bến xe Tiền Giang", "Bến xe Châu Đốc") },
    @{ Start="Hồ Chí Minh"; End="Tây Ninh"; Km=95; Hours=2.2; Stations=@("Bến xe Miền Tây", "Bến xe Tây Ninh") },
    @{ Start="Hồ Chí Minh"; End="Hà Nội"; Km=1720; Hours=32.0; Stations=@("Bến xe Miền Đông Mới", "Bến xe Phan Thiết", "Bến xe phía Nam Nha Trang", "Bến xe Quy Nhơn", "Bến xe Trung tâm Đà Nẵng", "Bến xe Phía Nam Huế", "Bến xe Vinh", "Bến xe Nước Ngầm") },
    
    # Tuyến từ Hà Nội
    @{ Start="Hà Nội"; End="Lào Cai"; Km=320; Hours=5.5; Stations=@("Bến xe Mỹ Đình", "Bến xe Sa Pa") },
    @{ Start="Hà Nội"; End="Quảng Ninh"; Km=160; Hours=3.0; Stations=@("Bến xe Mỹ Đình", "Bến xe Bãi Cháy") },
    @{ Start="Hà Nội"; End="Hải Phòng"; Km=120; Hours=2.0; Stations=@("Bến xe Giáp Bát", "Bến xe Cầu Rào") },
    @{ Start="Hà Nội"; End="Đà Nẵng"; Km=760; Hours=14.0; Stations=@("Bến xe Giáp Bát", "Bến xe Vinh", "Bến xe Phía Nam Huế", "Bến xe Trung tâm Đà Nẵng") },
    @{ Start="Hà Nội"; End="Nghệ An"; Km=300; Hours=5.5; Stations=@("Bến xe Nước Ngầm", "Bến xe Vinh") },

    # Tuyến từ Đà Nẵng
    @{ Start="Đà Nẵng"; End="Thừa Thiên Huế"; Km=100; Hours=2.0; Stations=@("Bến xe Trung tâm Đà Nẵng", "Bến xe Phía Nam Huế") },
    @{ Start="Đà Nẵng"; End="Lâm Đồng"; Km=650; Hours=13.0; Stations=@("Bến xe Trung tâm Đà Nẵng", "Bến xe Quy Nhơn", "Bến xe phía Nam Nha Trang", "Bến xe Liên tỉnh Đà Lạt") },
    @{ Start="Đà Nẵng"; End="Gia Lai"; Km=380; Hours=8.0; Stations=@("Bến xe Trung tâm Đà Nẵng", "Bến xe Quy Nhơn", "Bến xe Đức Long Gia Lai") },

    # Tuyến từ Cần Thơ & Đà Lạt
    @{ Start="Cần Thơ"; End="Lâm Đồng"; Km=460; Hours=10.0; Stations=@("Bến xe Trung tâm Cần Thơ", "Bến xe Tiền Giang", "Bến xe Bảo Lộc", "Bến xe Liên tỉnh Đà Lạt") },
    @{ Start="Cần Thơ"; End="Bà Rịa - Vũng Tàu"; Km=260; Hours=5.5; Stations=@("Bến xe Trung tâm Cần Thơ", "Bến xe Tiền Giang", "Bến xe Vũng Tàu") },
    @{ Start="Lâm Đồng"; End="Khánh Hòa"; Km=140; Hours=3.5; Stations=@("Bến xe Liên tỉnh Đà Lạt", "Bến xe phía Nam Nha Trang") },
    @{ Start="Khánh Hòa"; End="Bình Thuận"; Km=220; Hours=4.5; Stations=@("Bến xe phía Nam Nha Trang", "Bến xe Cam Ranh", "Bến xe Phan Thiết") }
)

foreach ($r in $routesData) {
    $chk = Execute-SqlScalar "SELECT COUNT(*) FROM Routes WHERE StartProvince = N'$($r.Start)' AND EndProvince = N'$($r.End)'";
    if ($chk -eq 0) {
        $insR = @"
INSERT INTO Routes (StartProvince, EndProvince, DistanceKm, DurationHours, IsActive)
VALUES (N'$($r.Start)', N'$($r.End)', $($r.Km), $($r.Hours), 1);
SELECT SCOPE_IDENTITY();
"@
        $routeId = [int](Execute-SqlScalar $insR);

        # Thêm RouteStations
        $stationsList = $r.Stations;
        for ($i = 0; $i -lt $stationsList.Length; $i++) {
            $stName = $stationsList[$i];
            $stId = Execute-SqlScalar "SELECT TOP 1 StationId FROM Stations WHERE Name = N'$stName'";
            if ($stId -ne $null -and $stId -gt 0) {
                $stopOrder = $i + 1;
                $stationType = if ($i -eq 0) { 0 } elseif ($i -eq $stationsList.Length - 1) { 1 } else { 2 }; # 0=Pickup, 1=Dropoff, 2=Both
                $offsetMins = [int](($r.Hours * 60) * ($i / ($stationsList.Length - 1)));
                Execute-SqlNonQuery @"
INSERT INTO RouteStations (RouteId, StationId, StopOrder, StationType, OffsetMinutes)
VALUES ($routeId, $stId, $stopOrder, $stationType, $offsetMins);
"@ | Out-Null
            }
        }
    }
}

$totalRoutes = Execute-SqlScalar "SELECT COUNT(*) FROM Routes";
$totalRouteStations = Execute-SqlScalar "SELECT COUNT(*) FROM RouteStations";
Write-Host "   -> Hiện có: $totalRoutes Tuyến đường & $totalRouteStations Trạm dừng trên các tuyến" -ForegroundColor Green

# =========================================================================
# 3. THÊM TÀI XẾ & PHỤ XE (CREW MEMBERS)
# =========================================================================
Write-Host ">>> 3. Đang bổ sung đội ngũ Tài xế & Phụ xe (Crew Members)..." -ForegroundColor Yellow

$hoList = @("Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý");
$demList = @("Văn", "Hữu", "Thanh", "Minh", "Đức", "Quốc", "Tuấn", "Hoàng", "Xuân", "Thành", "Ngọc", "Bảo", "Trọng", "Đình", "Tiến");
$tenList = @("Dũng", "Tuấn", "Hùng", "Cường", "Nam", "Bình", "Huy", "Long", "Khoa", "Phong", "Sơn", "Trung", "Thắng", "Phúc", "Khang", "Thịnh", "Đạt", "Lâm", "Hải", "Vinh");

$currentCrew = [int](Execute-SqlScalar "SELECT COUNT(*) FROM CrewMembers");
$targetCrew = 150;
if ($currentCrew -lt $targetCrew) {
    $needed = $targetCrew - $currentCrew;
    for ($i = 1; $i -le $needed; $i++) {
        $ho = $hoList[(Get-Random -Maximum $hoList.Length)];
        $dem = $demList[(Get-Random -Maximum $demList.Length)];
        $ten = $tenList[(Get-Random -Maximum $tenList.Length)];
        $fullName = "$ho $dem $ten";
        $phone = "09" + (Get-Random -Minimum 10000000 -Maximum 99999999);
        $role = if ($i % 2 -eq 0) { 0 } else { 1 }; # 0=Driver, 1=Assistant
        $licNum = if ($role -eq 0) { "GPLX-E-" + (Get-Random -Minimum 100000 -Maximum 999999) } else { "NULL" };
        
        $licVal = if ($licNum -eq "NULL") { "NULL" } else { "N'$licNum'" };
        Execute-SqlNonQuery @"
INSERT INTO CrewMembers (FullName, PhoneNumber, LicenseNumber, Role, IsActive, CreatedAt)
VALUES (N'$fullName', '$phone', $licVal, $role, 1, SYSUTCDATETIME());
"@ | Out-Null
    }
}
$totalCrew = Execute-SqlScalar "SELECT COUNT(*) FROM CrewMembers";
Write-Host "   -> Hiện có: $totalCrew Tài xế & Phụ xe" -ForegroundColor Green

# =========================================================================
# 4. THÊM LOẠI XE MỚI & 200+ XE KHÁCH (BUSES)
# =========================================================================
Write-Host ">>> 4. Đang thêm các dòng xe khách và tạo đội 200+ xe..." -ForegroundColor Yellow

# Thêm 2 loại xe phổ biến khác: Ghế ngồi VIP 28 chỗ và Limousine Cung điện 24 phòng
$chkBt3 = Execute-SqlScalar "SELECT COUNT(*) FROM BusTypes WHERE Name LIKE N'%28%'";
if ($chkBt3 -eq 0) {
    # Tạo layout 28 chỗ
    $layout28 = '{"decks":[{"deckNumber":1,"deckName":"Tầng 1","rows":7,"cols":4,"seats":[';
    $seats = @();
    for ($r=1; $r -le 7; $r++) {
        for ($c=1; $c -le 4; $c++) {
            $num = ($r - 1)*4 + $c;
            $code = "A{0:D2}" -f $num;
            $seats += '{"code":"' + $code + '","row":' + $r + ',"col":' + $c + ',"type":"Standard"}';
        }
    }
    $layout28 += ($seats -join ',') + ']}]}';
    Execute-SqlNonQuery @"
INSERT INTO BusTypes (Name, TotalSeats, TotalDecks, SeatLayoutJson)
VALUES (N'Xe Universe VIP 28 ghế ngả', 28, 1, N'$layout28');
"@ | Out-Null
}

$chkBt4 = Execute-SqlScalar "SELECT COUNT(*) FROM BusTypes WHERE Name LIKE N'%Cung điện%'";
if ($chkBt4 -eq 0) {
    $layout24 = '{"decks":[{"deckNumber":1,"deckName":"Tầng dưới (A)","rows":6,"cols":2,"seats":[';
    $seatsA = @();
    for ($i=1; $i -le 12; $i++) {
        $code = "A{0:D2}" -f $i;
        $row = [math]::Ceiling($i/2);
        $col = if ($i%2 -eq 1) { 1 } else { 2 };
        $seatsA += '{"code":"' + $code + '","row":' + $row + ',"col":' + $col + ',"type":"VIP"}';
    }
    $layout24 += ($seatsA -join ',') + ']},{"deckNumber":2,"deckName":"Tầng trên (B)","rows":6,"cols":2,"seats":[';
    $seatsB = @();
    for ($i=1; $i -le 12; $i++) {
        $code = "B{0:D2}" -f $i;
        $row = [math]::Ceiling($i/2);
        $col = if ($i%2 -eq 1) { 1 } else { 2 };
        $seatsB += '{"code":"' + $code + '","row":' + $row + ',"col":' + $col + ',"type":"VIP"}';
    }
    $layout24 += ($seatsB -join ',') + ']}]}';
    Execute-SqlNonQuery @"
INSERT INTO BusTypes (Name, TotalSeats, TotalDecks, SeatLayoutJson)
VALUES (N'Limousine Cung điện Hoàng Gia 24 phòng', 24, 2, N'$layout24');
"@ | Out-Null
}

# Lấy danh sách BusTypeIds
$busTypeIds = @();
$cmdBT = $conn.CreateCommand();
$cmdBT.CommandText = "SELECT BusTypeId FROM BusTypes";
$rBT = $cmdBT.ExecuteReader();
while ($rBT.Read()) { $busTypeIds += [int]$rBT[0]; }
$rBT.Close();

# Lấy danh sách Drivers và Assistants
$driverIds = @();
$assistantIds = @();
$cmdCr = $conn.CreateCommand();
$cmdCr.CommandText = "SELECT CrewMemberId, Role FROM CrewMembers WHERE IsActive = 1";
$rCr = $cmdCr.ExecuteReader();
while ($rCr.Read()) {
    if ([int]$rCr[1] -eq 0) { $driverIds += [int]$rCr[0]; }
    else { $assistantIds += [int]$rCr[0]; }
}
$rCr.Close();

# Tạo 210 xe khách
$currentBuses = [int](Execute-SqlScalar "SELECT COUNT(*) FROM Buses");
$targetBuses = 210;

$seriesPrefixes = @("51B", "50F", "29B", "30F", "43B", "65B", "49B", "79B", "72B", "86B", "47B", "81B", "77B", "60B", "61B", "14B", "15B", "75B", "37B", "68B");
$amenitiesPool = @(
    "Wifi tốc độ cao, Nước uống, Khăn lạnh, Cổng sạc USB, Đèn đọc sách",
    "Màn hình giải trí Full HD riêng từng giường, Cổng sạc Type-C, Tai nghe, Chăn nệm diệt khuẩn",
    "Ghế massage tự động, Rèm che riêng tư, Nước suối Lavie, Tủ lạnh mini, Ổ điện 220V",
    "Hệ thống đèn LED bầu trời sao, Wifi 5G, Khăn lạnh cao cấp, Dép đi trong xe, Búa thoát hiểm an toàn",
    "Cửa sạc USB-C PD 45W, Bàn làm việc gấp gọn, Rèm chống tia UV, Nước uống trà thảo mộc"
);

if ($currentBuses -lt $targetBuses) {
    $neededBuses = $targetBuses - $currentBuses;
    for ($i = 1; $i -le $neededBuses; $i++) {
        $prefix = $seriesPrefixes[(Get-Random -Maximum $seriesPrefixes.Length)];
        $numPart = "{0:D3}.{1:D2}" -f (Get-Random -Minimum 101 -Maximum 999), (Get-Random -Minimum 10 -Maximum 99);
        $plate = "$prefix-$numPart";
        
        # Kiểm tra trùng biển số
        $chkPlate = Execute-SqlScalar "SELECT COUNT(*) FROM Buses WHERE LicensePlate = '$plate'";
        if ($chkPlate -gt 0) {
            $plate = "$prefix-" + (Get-Random -Minimum 10000 -Maximum 99999);
        }

        $btId = $busTypeIds[(Get-Random -Maximum $busTypeIds.Length)];
        $amen = $amenitiesPool[(Get-Random -Maximum $amenitiesPool.Length)];
        
        $driverId = if ($driverIds.Length -gt 0) { $driverIds[(Get-Random -Maximum $driverIds.Length)] } else { "NULL" };
        $astId = if ($assistantIds.Length -gt 0) { $assistantIds[(Get-Random -Maximum $assistantIds.Length)] } else { "NULL" };

        Execute-SqlNonQuery @"
INSERT INTO Buses (BusTypeId, LicensePlate, Amenities, IsActive, DriverId, AssistantId)
VALUES ($btId, '$plate', N'$amen', 1, $driverId, $astId);
"@ | Out-Null
    }
}

$totalBuses = Execute-SqlScalar "SELECT COUNT(*) FROM Buses";
Write-Host "   -> Hiện có: $totalBuses Xe khách hoạt động" -ForegroundColor Green

# =========================================================================
# 5. THÊM 200+ CHUYẾN XE (TRIPS) VỚI ĐỦ CÁC KHUNG GIỜ VÀ TUYẾN ĐƯỜNG
# =========================================================================
Write-Host ">>> 5. Đang tạo lịch trình hơn 200 chuyến xe đa dạng tuyến đường..." -ForegroundColor Yellow

# Lấy danh sách Routes và Buses
$allRoutes = @();
$cmdR = $conn.CreateCommand();
$cmdR.CommandText = "SELECT RouteId, DurationHours, DistanceKm FROM Routes WHERE IsActive = 1";
$rR = $cmdR.ExecuteReader();
while ($rR.Read()) {
    $allRoutes += @{
        RouteId = [int]$rR[0];
        DurationHours = [decimal]$rR[1];
        DistanceKm = [int]$rR[2];
    };
}
$rR.Close();

$allBuses = @();
$cmdB = $conn.CreateCommand();
$cmdB.CommandText = "SELECT BusId, BusTypeId FROM Buses WHERE IsActive = 1";
$rB = $cmdB.ExecuteReader();
while ($rB.Read()) {
    $allBuses += @{
        BusId = [int]$rB[0];
        BusTypeId = [int]$rB[1];
    };
}
$rB.Close();

# Tạo chuyến xe cho 14 ngày tới (từ hôm nay đến 2 tuần sau)
# Mỗi ngày có nhiều chuyến sáng, trưa, chiều, tối, đêm
$depHours = @(5.5, 6.0, 7.0, 8.0, 9.5, 11.0, 13.0, 14.5, 16.0, 18.0, 19.5, 21.0, 22.5, 23.5);

$currentTrips = [int](Execute-SqlScalar "SELECT COUNT(*) FROM Trips");
$targetTrips = 250;

if ($currentTrips -lt $targetTrips) {
    $neededTrips = $targetTrips - $currentTrips;
    $today = [DateTime]::UtcNow.Date;

    for ($i = 0; $i -lt $neededTrips; $i++) {
        # Phân bổ từ ngày hôm nay đến 14 ngày tiếp theo
        $dayOffset = ($i % 14);
        $route = $allRoutes[(Get-Random -Maximum $allRoutes.Length)];
        $bus = $allBuses[(Get-Random -Maximum $allBuses.Length)];
        
        $hourPick = $depHours[(Get-Random -Maximum $depHours.Length)];
        $hour = [math]::Floor($hourPick);
        $minute = [int](($hourPick - $hour) * 60);

        $depTime = $today.AddDays($dayOffset).AddHours($hour).AddMinutes($minute);
        $arrTime = $depTime.AddHours([double]$route.DurationHours);

        # Tính giá vé cơ bản theo cự ly và loại xe
        $pricePerKm = if ($bus.BusTypeId -eq 2 -or $bus.BusTypeId -eq 4) { 1100 } else { 850 };
        $basePrice = [math]::Round(($route.DistanceKm * $pricePerKm) / 10000) * 10000;
        if ($basePrice -lt 150000) { $basePrice = 150000; }
        if ($basePrice -gt 1500000) { $basePrice = 1200000; }

        $status = 0; # 0 = Scheduled

        $depStr = $depTime.ToString("yyyy-MM-dd HH:mm:ss");
        $arrStr = $arrTime.ToString("yyyy-MM-dd HH:mm:ss");

        Execute-SqlNonQuery @"
INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, BasePrice, Status)
VALUES ($($route.RouteId), $($bus.BusId), '$depStr', '$arrStr', $basePrice, $status);
"@ | Out-Null
    }
}

$totalTrips = Execute-SqlScalar "SELECT COUNT(*) FROM Trips";
Write-Host "   -> Hiện có: $totalTrips Chuyến xe trong hệ thống" -ForegroundColor Green

# =========================================================================
# 6. THÊM BOOKINGS VÀ TICKETS CHÂN THỰC
# =========================================================================
Write-Host ">>> 6. Đang tạo các Đơn đặt vé & Vé hành khách thực tế..." -ForegroundColor Yellow

# Lấy UserId của Customer
$userId = Execute-SqlScalar "SELECT TOP 1 Id FROM AspNetUsers WHERE Email = 'customer@webvantai.vn'";
if ($userId -eq $null) {
    $userId = Execute-SqlScalar "SELECT TOP 1 Id FROM AspNetUsers";
}

# Lấy các chuyến xe có ít hoặc chưa có booking
$tripsWithoutBookings = @();
$cmdTwb = $conn.CreateCommand();
$cmdTwb.CommandText = @"
SELECT TOP 40 t.TripId, t.BasePrice, t.DepartureTime, r.RouteId, b.BusTypeId
FROM Trips t
JOIN Routes r ON t.RouteId = r.RouteId
JOIN Buses b ON t.BusId = b.BusId
LEFT JOIN Bookings bk ON t.TripId = bk.TripId
GROUP BY t.TripId, t.BasePrice, t.DepartureTime, r.RouteId, b.BusTypeId
HAVING COUNT(bk.BookingId) < 3
ORDER BY t.DepartureTime ASC;
"@
$rTwb = $cmdTwb.ExecuteReader();
while ($rTwb.Read()) {
    $tripsWithoutBookings += @{
        TripId = [int]$rTwb[0];
        BasePrice = [decimal]$rTwb[1];
        DepartureTime = [DateTime]$rTwb[2];
        RouteId = [int]$rTwb[3];
        BusTypeId = [int]$rTwb[4];
    };
}
$rTwb.Close();

$samplePassengers = @(
    @{ Name="Nguyễn Hoàng Nam"; Phone="0909123456"; Email="nam.nguyen@gmail.com" },
    @{ Name="Trần Thị Mai Phương"; Phone="0918765432"; Email="phuong.tran@gmail.com" },
    @{ Name="Lê Văn Hùng"; Phone="0987112233"; Email="hung.le@outlook.com" },
    @{ Name="Phạm Quốc Đạt"; Phone="0933445566"; Email="dat.pham@gmail.com" },
    @{ Name="Đỗ Quỳnh Như"; Phone="0902889900"; Email="nhu.do@yahoo.com" },
    @{ Name="Vũ Minh Tuấn"; Phone="0945667788"; Email="tuan.vu@gmail.com" },
    @{ Name="Bùi Thị Bích Ngọc"; Phone="0978123789"; Email="ngoc.bui@gmail.com" },
    @{ Name="Hoàng Gia Bảo"; Phone="0912998877"; Email="bao.hoang@gmail.com" },
    @{ Name="Ngô Thanh Vân"; Phone="0903332211"; Email="van.ngo@gmail.com" },
    @{ Name="Đinh Trọng Trí"; Phone="0938445522"; Email="tri.dinh@gmail.com" }
);

$seatCodesDeckA = @("A01", "A02", "A03", "A04", "A05", "A06", "A07", "A08", "A09", "A10", "A11", "A12");
$seatCodesDeckB = @("B01", "B02", "B03", "B04", "B05", "B06", "B07", "B08", "B09", "B10", "B11", "B12");

foreach ($tp in $tripsWithoutBookings) {
    # Lấy trạm đón/trả của tuyến này
    $stPickup = Execute-SqlScalar "SELECT TOP 1 StationId FROM RouteStations WHERE RouteId = $($tp.RouteId) ORDER BY StopOrder ASC";
    $stDropoff = Execute-SqlScalar "SELECT TOP 1 StationId FROM RouteStations WHERE RouteId = $($tp.RouteId) ORDER BY StopOrder DESC";

    if ($stPickup -ne $null -and $stDropoff -ne $null) {
        # Đặt 1 hoặc 2 đơn cho chuyến này
        $numOrders = Get-Random -Minimum 1 -Maximum 3;
        for ($o = 1; $o -le $numOrders; $o++) {
            $pass = $samplePassengers[(Get-Random -Maximum $samplePassengers.Length)];
            $seatsToBook = if ((Get-Random -Minimum 1 -Maximum 10) -gt 7) { 2 } else { 1 };
            $totalAmount = $tp.BasePrice * $seatsToBook;
            $createdTime = [DateTime]::UtcNow.AddHours(- (Get-Random -Minimum 2 -Maximum 72)).ToString("yyyy-MM-dd HH:mm:ss");
            $expiredTime = [DateTime]::UtcNow.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");
            $vnpTxn = "VNP" + (Get-Random -Minimum 10000000 -Maximum 99999999);

            $insBooking = @"
INSERT INTO Bookings (UserId, TripId, PickupStationId, DropoffStationId, TotalAmount, Status, CreatedAt, ExpiredAt, VnPayTxnRef, ContactEmail, PaymentMethod, RefundedAmount)
VALUES ('$userId', $($tp.TripId), $stPickup, $stDropoff, $totalAmount, 1, '$createdTime', '$expiredTime', '$vnpTxn', '$($pass.Email)', 0, 0);
SELECT SCOPE_IDENTITY();
"@
            $bookingId = [int](Execute-SqlScalar $insBooking);

            # Tìm ghế chưa đặt trong chuyến này
            for ($s = 0; $s -lt $seatsToBook; $s++) {
                $pool = if ($s -eq 0) { $seatCodesDeckA } else { $seatCodesDeckB };
                $seatCandidate = $pool[(Get-Random -Maximum $pool.Length)];
                
                # Check nếu trùng
                $seatTaken = Execute-SqlScalar "SELECT COUNT(*) FROM Tickets WHERE TripId = $($tp.TripId) AND SeatCode = '$seatCandidate' AND Status != 2";
                if ($seatTaken -gt 0) {
                    $seatCandidate = "B{0:D2}" -f (Get-Random -Minimum 1 -Maximum 15);
                }

                $qrToken = [Guid]::NewGuid().ToString("N");
                Execute-SqlNonQuery @"
INSERT INTO Tickets (BookingId, TripId, SeatCode, PassengerName, PassengerPhone, QrToken, Status)
VALUES ($bookingId, $($tp.TripId), '$seatCandidate', N'$($pass.Name)', '$($pass.Phone)', '$qrToken', 0);
"@ | Out-Null
            }
        }
    }
}

$totalBookings = Execute-SqlScalar "SELECT COUNT(*) FROM Bookings";
$totalTickets = Execute-SqlScalar "SELECT COUNT(*) FROM Tickets";
Write-Host "   -> Hiện có: $totalBookings Đơn đặt vé & $totalTickets Vé đã phát hành" -ForegroundColor Green

$conn.Close();
Write-Host "`n>>> HOÀN TẤT NẠP DỮ LIỆU THÀNH CÔNG!" -ForegroundColor Green
Write-Host "Dữ liệu đạt tiêu chuẩn: phong phú, chân thực, phủ khắp các tuyến liên tỉnh trên cả nước." -ForegroundColor Cyan
