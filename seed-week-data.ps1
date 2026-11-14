# Script thêm 100+ tuyến xe 2 chiều và lịch trình dồi dào cho ngày hôm nay và 7 ngày tới
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

# -------------------------------------------------------------
# 1. BỔ SUNG THÊM BẾN XE MỚI Ở CÁC TỈNH THÀNH TRỌNG ĐIỂM
# -------------------------------------------------------------
Write-Host ">>> 1. Kiểm tra và bổ sung các Bến xe còn thiếu..." -ForegroundColor Yellow

$newStations = @(
    @{ Name="Bến xe Trung tâm Thanh Hóa"; Province="Thanh Hóa"; Address="Đường Bà Triệu, Phường Trường Thi, TP. Thanh Hóa, Tỉnh Thanh Hóa"; Lat=19.806712; Lng=105.785210 },
    @{ Name="Bến xe Hà Tĩnh"; Province="Hà Tĩnh"; Address="Trần Phú, Phường Hà Huy Tập, TP. Hà Tĩnh, Tỉnh Hà Tĩnh"; Lat=18.342110; Lng=105.906420 },
    @{ Name="Bến xe Đồng Hới"; Province="Quảng Bình"; Address="Số 156 Trần Hưng Đạo, TP. Đồng Hới, Tỉnh Quảng Bình"; Lat=17.469120; Lng=106.618450 },
    @{ Name="Bến xe Đông Hà"; Province="Quảng Trị"; Address="Lê Duẩn, Phường 2, TP. Đông Hà, Tỉnh Quảng Trị"; Lat=16.812340; Lng=107.098710 },
    @{ Name="Bến xe Quảng Ngãi"; Province="Quảng Ngãi"; Address="Số 26 Lê Thánh Tôn, Phường Nghĩa Chánh, TP. Quảng Ngãi, Tỉnh Quảng Ngãi"; Lat=15.118740; Lng=108.798120 },
    @{ Name="Bến xe Nam Tuy Hòa"; Province="Phú Yên"; Address="Nguyễn Tất Thành, Phường Phú Lâm, TP. Tuy Hòa, Tỉnh Phú Yên"; Lat=13.078910; Lng=109.308710 },
    @{ Name="Bến xe Phan Rang"; Province="Ninh Thuận"; Address="Số 52 Quốc Lộ 1A, Phường Đài Sơn, TP. Phan Rang - Tháp Chàm, Tỉnh Ninh Thuận"; Lat=11.587410; Lng=108.998710 },
    @{ Name="Bến xe Kon Tum"; Province="Kon Tum"; Address="Số 281 Phan Đình Phùng, TP. Kon Tum, Tỉnh Kon Tum"; Lat=14.351230; Lng=108.001240 },
    @{ Name="Bến xe Gia Nghĩa"; Province="Đắk Nông"; Address="Đường 23/3, Phường Nghĩa Thành, TP. Gia Nghĩa, Tỉnh Đắk Nông"; Lat=11.992340; Lng=107.698710 },
    @{ Name="Bến xe Bến Tre"; Province="Bến Tre"; Address="Số 507 Ấp 1, Xã Sơn Đông, TP. Bến Tre, Tỉnh Bến Tre"; Lat=10.251240; Lng=106.358710 },
    @{ Name="Bến xe Trà Vinh"; Province="Trà Vinh"; Address="Đường Nguyễn Thị Minh Khai, Phường 7, TP. Trà Vinh, Tỉnh Trà Vinh"; Lat=9.928740; Lng=106.331240 },
    @{ Name="Bến xe Sóc Trăng"; Province="Sóc Trăng"; Address="Số 38 Lê Duẩn, Phường 3, TP. Sóc Trăng, Tỉnh Sóc Trăng"; Lat=9.598710; Lng=105.971240 },
    @{ Name="Bến xe Bạc Liêu"; Province="Bạc Liêu"; Address="Quốc Lộ 1A, Phường 7, TP. Bạc Liêu, Tỉnh Bạc Liêu"; Lat=9.289120; Lng=105.718740 },
    @{ Name="Bến xe Cà Mau"; Province="Cà Mau"; Address="Quốc Lộ 1A, Phường 6, TP. Cà Mau, Tỉnh Cà Mau"; Lat=9.176540; Lng=105.158710 },
    @{ Name="Bến xe Hậu Giang"; Province="Hậu Giang"; Address="Đường Trần Hưng Đạo, Phường 5, TP. Vị Thanh, Tỉnh Hậu Giang"; Lat=9.784510; Lng=105.471240 },
    @{ Name="Bến xe Đồng Tháp"; Province="Đồng Tháp"; Address="Số 71 Lý Thường Kiệt, Phường 2, TP. Cao Lãnh, Tỉnh Đồng Tháp"; Lat=10.458710; Lng=105.631240 },
    @{ Name="Bến xe Long An"; Province="Long An"; Address="Số 168 Hùng Vương, Phường 2, TP. Tân An, Tỉnh Long An"; Lat=10.534120; Lng=106.408710 },
    @{ Name="Bến xe Thái Bình"; Province="Thái Bình"; Address="Tỉnh Lộ 223, Phường Bồ Xuyên, TP. Thái Bình, Tỉnh Thái Bình"; Lat=20.451240; Lng=106.338710 },
    @{ Name="Bến xe Nam Định"; Province="Nam Định"; Address="Đường Điện Biên, Phường Lộc Hòa, TP. Nam Định, Tỉnh Nam Định"; Lat=20.431240; Lng=106.168710 },
    @{ Name="Bến xe Ninh Bình"; Province="Ninh Bình"; Address="Số 207 Lê Đại Hành, Phường Thanh Bình, TP. Ninh Bình, Tỉnh Ninh Bình"; Lat=20.251240; Lng=105.981240 },
    @{ Name="Bến xe Thái Nguyên"; Province="Thái Nguyên"; Address="Số 73 Đồng Quang, Phường Quang Trung, TP. Thái Nguyên, Tỉnh Thái Nguyên"; Lat=21.587410; Lng=105.831240 }
)

foreach ($st in $newStations) {
    $chk = Execute-SqlScalar "SELECT COUNT(*) FROM Stations WHERE Name = N'$($st.Name)'";
    if ($chk -eq 0) {
        Execute-SqlNonQuery @"
INSERT INTO Stations (Name, Province, Address, Latitude, Longitude)
VALUES (N'$($st.Name)', N'$($st.Province)', N'$($st.Address)', $($st.Lat), $($st.Lng));
"@ | Out-Null
    }
}

$totalStations = Execute-SqlScalar "SELECT COUNT(*) FROM Stations";
Write-Host "   -> Tổng số bến xe hiện tại: $totalStations" -ForegroundColor Green

# -------------------------------------------------------------
# 2. XÂY DỰNG 100+ TUYẾN XE 2 CHIỀU GIỮA CÁC TỈNH THÀNH
# -------------------------------------------------------------
Write-Host ">>> 2. Đang tạo các tuyến xe 2 chiều kết nối các tỉnh thành..." -ForegroundColor Yellow

# Danh sách các cặp kết nối 2 chiều (Province A <-> Province B, Km, Hours)
$routePairs = @(
    # Các tuyến hiện có cần đảm bảo đủ chiều về
    @{ P1="Hồ Chí Minh"; P2="Lâm Đồng"; Km=310; H=7.0 },
    @{ P1="Hồ Chí Minh"; P2="Khánh Hòa"; Km=430; H=8.5 },
    @{ P1="Hồ Chí Minh"; P2="Bà Rịa - Vũng Tàu"; Km=105; H=2.5 },
    @{ P1="Hồ Chí Minh"; P2="Cần Thơ"; Km=165; H=3.5 },
    @{ P1="Hồ Chí Minh"; P2="Bình Thuận"; Km=200; H=4.0 },
    @{ P1="Hồ Chí Minh"; P2="Đà Nẵng"; Km=950; H=18.0 },
    @{ P1="Hồ Chí Minh"; P2="Đắk Lắk"; Km=350; H=8.0 },
    @{ P1="Hồ Chí Minh"; P2="Gia Lai"; Km=530; H=11.5 },
    @{ P1="Hồ Chí Minh"; P2="Kiên Giang"; Km=250; H=5.5 },
    @{ P1="Hồ Chí Minh"; P2="An Giang"; Km=240; H=5.5 },
    @{ P1="Hồ Chí Minh"; P2="Tây Ninh"; Km=95; H=2.2 },
    @{ P1="Hồ Chí Minh"; P2="Hà Nội"; Km=1720; H=32.0 },
    @{ P1="Hà Nội"; P2="Lào Cai"; Km=320; H=5.5 },
    @{ P1="Hà Nội"; P2="Quảng Ninh"; Km=160; H=3.0 },
    @{ P1="Hà Nội"; P2="Hải Phòng"; Km=120; H=2.0 },
    @{ P1="Hà Nội"; P2="Đà Nẵng"; Km=760; H=14.0 },
    @{ P1="Hà Nội"; P2="Nghệ An"; Km=300; H=5.5 },
    @{ P1="Đà Nẵng"; P2="Thừa Thiên Huế"; Km=100; H=2.0 },
    @{ P1="Đà Nẵng"; P2="Lâm Đồng"; Km=650; H=13.0 },
    @{ P1="Đà Nẵng"; P2="Gia Lai"; Km=380; H=8.0 },
    @{ P1="Cần Thơ"; P2="Lâm Đồng"; Km=460; H=10.0 },
    @{ P1="Cần Thơ"; P2="Bà Rịa - Vũng Tàu"; Km=260; H=5.5 },
    @{ P1="Lâm Đồng"; P2="Khánh Hòa"; Km=140; H=3.5 },
    @{ P1="Khánh Hòa"; P2="Bình Thuận"; Km=220; H=4.5 },

    # Các tuyến 2 chiều mới từ TP.HCM đi các tỉnh Miền Tây & Miền Trung & Tây Nguyên
    @{ P1="Hồ Chí Minh"; P2="Tiền Giang"; Km=70; H=1.5 },
    @{ P1="Hồ Chí Minh"; P2="Vĩnh Long"; Km=135; H=3.0 },
    @{ P1="Hồ Chí Minh"; P2="Đồng Tháp"; Km=150; H=3.5 },
    @{ P1="Hồ Chí Minh"; P2="Bến Tre"; Km=85; H=2.0 },
    @{ P1="Hồ Chí Minh"; P2="Trà Vinh"; Km=130; H=3.0 },
    @{ P1="Hồ Chí Minh"; P2="Sóc Trăng"; Km=230; H=5.0 },
    @{ P1="Hồ Chí Minh"; P2="Hậu Giang"; Km=210; H=4.5 },
    @{ P1="Hồ Chí Minh"; P2="Bạc Liêu"; Km=280; H=6.0 },
    @{ P1="Hồ Chí Minh"; P2="Cà Mau"; Km=350; H=7.5 },
    @{ P1="Hồ Chí Minh"; P2="Bình Định"; Km=650; H=12.5 },
    @{ P1="Hồ Chí Minh"; P2="Phú Yên"; Km=540; H=10.5 },
    @{ P1="Hồ Chí Minh"; P2="Ninh Thuận"; Km=340; H=6.5 },
    @{ P1="Hồ Chí Minh"; P2="Quảng Ngãi"; Km=760; H=14.5 },
    @{ P1="Hồ Chí Minh"; P2="Thừa Thiên Huế"; Km=1040; H=20.0 },
    @{ P1="Hồ Chí Minh"; P2="Quảng Bình"; Km=1200; H=23.0 },
    @{ P1="Hồ Chí Minh"; P2="Nghệ An"; Km=1420; H=27.0 },
    @{ P1="Hồ Chí Minh"; P2="Thanh Hóa"; Km=1560; H=29.0 },
    @{ P1="Hồ Chí Minh"; P2="Đắk Nông"; Km=260; H=6.0 },
    @{ P1="Hồ Chí Minh"; P2="Kon Tum"; Km=580; H=12.5 },
    @{ P1="Hồ Chí Minh"; P2="Đồng Nai"; Km=35; H=1.0 },
    @{ P1="Hồ Chí Minh"; P2="Bình Dương"; Km=30; H=1.0 },

    # Tuyến 2 chiều từ Hà Nội đi các tỉnh
    @{ P1="Hà Nội"; P2="Ninh Bình"; Km=95; H=1.8 },
    @{ P1="Hà Nội"; P2="Nam Định"; Km=90; H=1.7 },
    @{ P1="Hà Nội"; P2="Thái Bình"; Km=110; H=2.2 },
    @{ P1="Hà Nội"; P2="Thái Nguyên"; Km=80; H=1.5 },
    @{ P1="Hà Nội"; P2="Thanh Hóa"; Km=160; H=3.0 },
    @{ P1="Hà Nội"; P2="Hà Tĩnh"; Km=350; H=6.5 },
    @{ P1="Hà Nội"; P2="Quảng Bình"; Km=500; H=9.5 },
    @{ P1="Hà Nội"; P2="Quảng Trị"; Km=600; H=11.5 },
    @{ P1="Hà Nội"; P2="Thừa Thiên Huế"; Km=670; H=13.0 },
    @{ P1="Hà Nội"; P2="Lâm Đồng"; Km=1420; H=28.0 },
    @{ P1="Hà Nội"; P2="Khánh Hòa"; Km=1300; H=25.0 },

    # Tuyến 2 chiều từ Đà Nẵng
    @{ P1="Đà Nẵng"; P2="Quảng Ngãi"; Km=130; H=2.5 },
    @{ P1="Đà Nẵng"; P2="Bình Định"; Km=300; H=5.5 },
    @{ P1="Đà Nẵng"; P2="Phú Yên"; Km=410; H=7.5 },
    @{ P1="Đà Nẵng"; P2="Khánh Hòa"; Km=530; H=10.0 },
    @{ P1="Đà Nẵng"; P2="Ninh Thuận"; Km=630; H=12.0 },
    @{ P1="Đà Nẵng"; P2="Bình Thuận"; Km=750; H=14.0 },
    @{ P1="Đà Nẵng"; P2="Quảng Trị"; Km=160; H=3.0 },
    @{ P1="Đà Nẵng"; P2="Quảng Bình"; Km=260; H=5.0 },
    @{ P1="Đà Nẵng"; P2="Hà Tĩnh"; Km=420; H=8.0 },
    @{ P1="Đà Nẵng"; P2="Nghệ An"; Km=470; H=9.0 },
    @{ P1="Đà Nẵng"; P2="Thanh Hóa"; Km=610; H=11.5 },
    @{ P1="Đà Nẵng"; P2="Đắk Lắk"; Km=530; H=11.0 },
    @{ P1="Đà Nẵng"; P2="Kon Tum"; Km=290; H=6.0 },

    # Tuyến 2 chiều liên tỉnh Miền Tây
    @{ P1="Cần Thơ"; P2="Kiên Giang"; Km=115; H=2.5 },
    @{ P1="Cần Thơ"; P2="An Giang"; Km=120; H=2.5 },
    @{ P1="Cần Thơ"; P2="Cà Mau"; Km=180; H=3.8 },
    @{ P1="Cần Thơ"; P2="Bạc Liêu"; Km=115; H=2.3 },
    @{ P1="Cần Thơ"; P2="Sóc Trăng"; Km=65; H=1.5 },
    @{ P1="Cần Thơ"; P2="Hậu Giang"; Km=50; H=1.0 },
    @{ P1="Cần Thơ"; P2="Đồng Tháp"; Km=85; H=2.0 },
    @{ P1="Cần Thơ"; P2="Vĩnh Long"; Km=35; H=0.8 },
    @{ P1="Cần Thơ"; P2="Tiền Giang"; Km=105; H=2.2 },
    @{ P1="Cần Thơ"; P2="Bến Tre"; Km=120; H=2.5 },
    @{ P1="Cần Thơ"; P2="Đà Nẵng"; Km=1100; H=21.0 },
    @{ P1="Cần Thơ"; P2="Hà Nội"; Km=1880; H=35.0 },

    # Tuyến 2 chiều liên tỉnh Miền Trung & Tây Nguyên
    @{ P1="Lâm Đồng"; P2="Bình Thuận"; Km=160; H=4.0 },
    @{ P1="Lâm Đồng"; P2="Ninh Thuận"; Km=110; H=2.8 },
    @{ P1="Lâm Đồng"; P2="Đắk Lắk"; Km=210; H=4.8 },
    @{ P1="Lâm Đồng"; P2="Gia Lai"; Km=390; H=8.5 },
    @{ P1="Lâm Đồng"; P2="Bình Định"; Km=330; H=7.0 },
    @{ P1="Khánh Hòa"; P2="Đắk Lắk"; Km=180; H=4.0 },
    @{ P1="Khánh Hòa"; P2="Gia Lai"; Km=360; H=7.5 },
    @{ P1="Khánh Hòa"; P2="Phú Yên"; Km=120; H=2.5 },
    @{ P1="Khánh Hòa"; P2="Ninh Thuận"; Km=100; H=2.0 },
    @{ P1="Đắk Lắk"; P2="Gia Lai"; Km=180; H=3.5 },
    @{ P1="Đắk Lắk"; P2="Kon Tum"; Km=230; H=4.5 },
    @{ P1="Đắk Lắk"; P2="Đắk Nông"; Km=130; H=2.8 },
    @{ P1="Gia Lai"; P2="Kon Tum"; Km=50; H=1.0 },
    @{ P1="Gia Lai"; P2="Bình Định"; Km=170; H=3.5 },

    # Tuyến 2 chiều Miền Bắc mở rộng
    @{ P1="Hải Phòng"; P2="Quảng Ninh"; Km=70; H=1.2 },
    @{ P1="Hải Phòng"; P2="Lào Cai"; Km=440; H=7.0 },
    @{ P1="Hải Phòng"; P2="Nghệ An"; Km=330; H=6.0 },
    @{ P1="Hải Phòng"; P2="Đà Nẵng"; Km=820; H=15.0 },
    @{ P1="Quảng Ninh"; P2="Lào Cai"; Km=480; H=7.5 },
    @{ P1="Quảng Ninh"; P2="Nghệ An"; Km=370; H=7.0 },
    @{ P1="Thanh Hóa"; P2="Nghệ An"; Km=140; H=2.5 },
    @{ P1="Nghệ An"; P2="Hà Tĩnh"; Km=50; H=1.0 },
    @{ P1="Nghệ An"; P2="Thừa Thiên Huế"; Km=370; H=7.0 },
    @{ P1="Thừa Thiên Huế"; P2="Quảng Trị"; Km=70; H=1.5 },
    @{ P1="Quảng Trị"; P2="Quảng Bình"; Km=100; H=2.0 }
)

# Hàm trợ giúp lấy 1 trạm tiêu biểu cho tỉnh
function Get-StationForProvince($prov) {
    $stId = Execute-SqlScalar "SELECT TOP 1 StationId FROM Stations WHERE Province = N'$prov'";
    return $stId;
}

# Tạo 2 chiều cho mỗi cặp (A -> B và B -> A)
foreach ($pair in $routePairs) {
    $pA = $pair.P1;
    $pB = $pair.P2;
    $km = $pair.Km;
    $hours = $pair.H;

    # Chiều đi: A -> B
    $chkAB = Execute-SqlScalar "SELECT COUNT(*) FROM Routes WHERE StartProvince = N'$pA' AND EndProvince = N'$pB'";
    if ($chkAB -eq 0) {
        $insAB = @"
INSERT INTO Routes (StartProvince, EndProvince, DistanceKm, DurationHours, IsActive)
VALUES (N'$pA', N'$pB', $km, $hours, 1);
SELECT SCOPE_IDENTITY();
"@
        $rIdAB = [int](Execute-SqlScalar $insAB);
        $stA = Get-StationForProvince $pA;
        $stB = Get-StationForProvince $pB;
        if ($stA -ne $null -and $stB -ne $null) {
            Execute-SqlNonQuery @"
INSERT INTO RouteStations (RouteId, StationId, StopOrder, StationType, OffsetMinutes)
VALUES ($rIdAB, $stA, 1, 0, 0),
       ($rIdAB, $stB, 2, 1, $([int]($hours * 60)));
"@ | Out-Null
        }
    }

    # Chiều về: B -> A
    $chkBA = Execute-SqlScalar "SELECT COUNT(*) FROM Routes WHERE StartProvince = N'$pB' AND EndProvince = N'$pA'";
    if ($chkBA -eq 0) {
        $insBA = @"
INSERT INTO Routes (StartProvince, EndProvince, DistanceKm, DurationHours, IsActive)
VALUES (N'$pB', N'$pA', $km, $hours, 1);
SELECT SCOPE_IDENTITY();
"@
        $rIdBA = [int](Execute-SqlScalar $insBA);
        $stA = Get-StationForProvince $pA;
        $stB = Get-StationForProvince $pB;
        if ($stA -ne $null -and $stB -ne $null) {
            Execute-SqlNonQuery @"
INSERT INTO RouteStations (RouteId, StationId, StopOrder, StationType, OffsetMinutes)
VALUES ($rIdBA, $stB, 1, 0, 0),
       ($rIdBA, $stA, 2, 1, $([int]($hours * 60)));
"@ | Out-Null
        }
    }
}

$totalRoutes = Execute-SqlScalar "SELECT COUNT(*) FROM Routes";
Write-Host "   -> Tổng số tuyến đường hiện tại: $totalRoutes (Đã có đầy đủ 2 chiều)" -ForegroundColor Green

# -------------------------------------------------------------
# 3. THÊM CHUYẾN XE (TRIPS) CHO HÔM NAY VÀ 7 NGÀY TỚI
# -------------------------------------------------------------
Write-Host ">>> 3. Đang tạo các chuyến xe dồi dào cho Hôm Nay (14/11/2026) và 7 ngày tới (15/11 - 21/11/2026)..." -ForegroundColor Yellow

# Lấy danh sách tất cả các Tuyến đang hoạt động
$routesList = @();
$cmdR = $conn.CreateCommand();
$cmdR.CommandText = "SELECT RouteId, DurationHours, DistanceKm FROM Routes WHERE IsActive = 1";
$rR = $cmdR.ExecuteReader();
while ($rR.Read()) {
    $routesList += @{
        RouteId = [int]$rR[0];
        DurationHours = [decimal]$rR[1];
        DistanceKm = [int]$rR[2];
    };
}
$rR.Close();

# Lấy danh sách xe
$busesList = @();
$cmdB = $conn.CreateCommand();
$cmdB.CommandText = "SELECT BusId, BusTypeId FROM Buses WHERE IsActive = 1";
$rB = $cmdB.ExecuteReader();
while ($rB.Read()) {
    $busesList += @{
        BusId = [int]$rB[0];
        BusTypeId = [int]$rB[1];
    };
}
$rB.Close();

# Ngày hôm nay: 2026-11-14
$startDate = [DateTime]::Parse("2026-11-14");

# Các khung giờ xuất bến thực tế
$timeSlots = @(
    @{ H=5; M=30 },
    @{ H=6; M=00 },
    @{ H=7; M=15 },
    @{ H=8; M=30 },
    @{ H=9; M=45 },
    @{ H=11; M=00 },
    @{ H=13; M=15 },
    @{ H=14; M=30 },
    @{ H=16; M=00 },
    @{ H=17; M=30 },
    @{ H=19; M=00 },
    @{ H=20; M=30 },
    @{ H=21; M=45 },
    @{ H=23; M=00 }
);

# Đảm bảo mỗi ngày trong 8 ngày (dayOffset = 0 đến 7) có ít nhất 40 - 60 chuyến xe
for ($dayOffset = 0; $dayOffset -le 7; $dayOffset++) {
    $currentDate = $startDate.AddDays($dayOffset);
    $dateStr = $currentDate.ToString("yyyy-MM-dd");

    $tripsOnDay = [int](Execute-SqlScalar "SELECT COUNT(*) FROM Trips WHERE CAST(DepartureTime as DATE) = '$dateStr'");
    $targetForDay = 45; # Tối thiểu 45 chuyến mỗi ngày

    if ($tripsOnDay -lt $targetForDay) {
        $needed = $targetForDay - $tripsOnDay;
        Write-Host "   -> Ngày $dateStr : Đang bổ sung $needed chuyến..." -ForegroundColor Gray

        for ($k = 0; $k -lt $needed; $k++) {
            $rt = $routesList[(Get-Random -Maximum $routesList.Length)];
            $bs = $busesList[(Get-Random -Maximum $busesList.Length)];
            $slot = $timeSlots[(Get-Random -Maximum $timeSlots.Length)];

            $depTime = $currentDate.AddHours($slot.H).AddMinutes($slot.M);
            $arrTime = $depTime.AddHours([double]$rt.DurationHours);

            # Giá vé cơ bản
            $rate = if ($bs.BusTypeId -eq 2 -or $bs.BusTypeId -eq 4) { 1100 } else { 850 };
            $basePrice = [math]::Round(($rt.DistanceKm * $rate) / 10000) * 10000;
            if ($basePrice -lt 150000) { $basePrice = 150000; }
            if ($basePrice -gt 1500000) { $basePrice = 1250000; }

            $depSql = $depTime.ToString("yyyy-MM-dd HH:mm:ss");
            $arrSql = $arrTime.ToString("yyyy-MM-dd HH:mm:ss");

            Execute-SqlNonQuery @"
INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, BasePrice, Status)
VALUES ($($rt.RouteId), $($bs.BusId), '$depSql', '$arrSql', $basePrice, 0);
"@ | Out-Null
        }
    }
}

$totalTrips = Execute-SqlScalar "SELECT COUNT(*) FROM Trips";
Write-Host "   -> Hiện có: $totalTrips Chuyến xe trong CSDL" -ForegroundColor Green

# -------------------------------------------------------------
# 4. TẠO THÊM BOOKINGS VÀ TICKETS CHO CÁC CHUYẾN TRONG 7 NGÀY TỚI
# -------------------------------------------------------------
Write-Host ">>> 4. Đang tạo các Đơn đặt vé & Vé hành khách cho các chuyến tuần này..." -ForegroundColor Yellow

$userId = Execute-SqlScalar "SELECT TOP 1 Id FROM AspNetUsers WHERE Email = 'customer@webvantai.vn'";
if ($userId -eq $null) {
    $userId = Execute-SqlScalar "SELECT TOP 1 Id FROM AspNetUsers";
}

$tripsInNext7Days = @();
$cmdT = $conn.CreateCommand();
$cmdT.CommandText = @"
SELECT TOP 30 t.TripId, t.BasePrice, t.DepartureTime, r.RouteId, b.BusTypeId
FROM Trips t
JOIN Routes r ON t.RouteId = r.RouteId
JOIN Buses b ON t.BusId = b.BusId
LEFT JOIN Bookings bk ON t.TripId = bk.TripId
WHERE CAST(t.DepartureTime as DATE) >= '2026-11-14' AND CAST(t.DepartureTime as DATE) <= '2026-11-21'
GROUP BY t.TripId, t.BasePrice, t.DepartureTime, r.RouteId, b.BusTypeId
HAVING COUNT(bk.BookingId) = 0
ORDER BY t.DepartureTime ASC;
"@
$rT = $cmdT.ExecuteReader();
while ($rT.Read()) {
    $tripsInNext7Days += @{
        TripId = [int]$rT[0];
        BasePrice = [decimal]$rT[1];
        DepartureTime = [DateTime]$rT[2];
        RouteId = [int]$rT[3];
        BusTypeId = [int]$rT[4];
    };
}
$rT.Close();

$sampleNames = @(
    @{ Name="Trần Quốc Huy"; Phone="0913554433"; Email="huy.tran@gmail.com" },
    @{ Name="Nguyễn Mai Linh"; Phone="0988776655"; Email="linh.nguyen@gmail.com" },
    @{ Name="Lê Tấn Phát"; Phone="0905123890"; Email="phat.le@yahoo.com" },
    @{ Name="Phan Thanh Hằng"; Phone="0932114477"; Email="hang.phan@outlook.com" },
    @{ Name="Võ Minh Quân"; Phone="0979888999"; Email="quan.vo@gmail.com" }
);

foreach ($tp in $tripsInNext7Days) {
    $stPickup = Execute-SqlScalar "SELECT TOP 1 StationId FROM RouteStations WHERE RouteId = $($tp.RouteId) ORDER BY StopOrder ASC";
    $stDropoff = Execute-SqlScalar "SELECT TOP 1 StationId FROM RouteStations WHERE RouteId = $($tp.RouteId) ORDER BY StopOrder DESC";

    if ($stPickup -ne $null -and $stDropoff -ne $null) {
        $p = $sampleNames[(Get-Random -Maximum $sampleNames.Length)];
        $seatCode = "A{0:D2}" -f (Get-Random -Minimum 1 -Maximum 10);
        $totalAmount = $tp.BasePrice;
        $createdTime = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        $expiredTime = [DateTime]::UtcNow.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");
        $vnpRef = "VNP" + (Get-Random -Minimum 10000000 -Maximum 99999999);

        $insB = @"
INSERT INTO Bookings (UserId, TripId, PickupStationId, DropoffStationId, TotalAmount, Status, CreatedAt, ExpiredAt, VnPayTxnRef, ContactEmail, PaymentMethod, RefundedAmount)
VALUES ('$userId', $($tp.TripId), $stPickup, $stDropoff, $totalAmount, 1, '$createdTime', '$expiredTime', '$vnpRef', '$($p.Email)', 0, 0);
SELECT SCOPE_IDENTITY();
"@
        $bId = [int](Execute-SqlScalar $insB);
        $qrToken = [Guid]::NewGuid().ToString("N");

        Execute-SqlNonQuery @"
INSERT INTO Tickets (BookingId, TripId, SeatCode, PassengerName, PassengerPhone, QrToken, Status)
VALUES ($bId, $($tp.TripId), '$seatCode', N'$($p.Name)', '$($p.Phone)', '$qrToken', 0);
"@ | Out-Null
    }
}

$conn.Close();
Write-Host "`n>>> HOÀN TẤT BỔ SUNG DỮ LIỆU!" -ForegroundColor Green
