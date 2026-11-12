# G-Trans — Hệ Thống Đặt Vé Xe Khách & Quản Lý Vận Tải

Ứng dụng web ASP.NET Core MVC (.NET 10) + Entity Framework Core + SQL Server LocalDB phục vụ đặt vé trực tuyến, thanh toán VNPay Sandbox, bán vé tại quầy, soát vé QR và điều hành vận tải.

## Tài khoản đăng nhập mặc định

Mật khẩu chung cho tất cả tài khoản mẫu: **`P@ssword123`**

| Vai trò (`Role`) | Email đăng nhập | Mật khẩu | Trang điều hướng sau đăng nhập |
|---|---|---|---|
| **Admin** (Quản trị hệ thống) | `admin@webvantai.vn` | `P@ssword123` | `/Admin/Dashboard` |
| **RouteManager** (Điều hành tuyến xe) | `routemanager@webvantai.vn` | `P@ssword123` | `/RouteManager/Dashboard` |
| **TicketManager** (Quản lý vé / Quầy vé) | `ticketmanager@webvantai.vn` | `P@ssword123` | `/TicketManager/Dashboard` |
| **TicketInspector** (Nhân viên soát vé) | `inspector@webvantai.vn` | `P@ssword123` | `/TicketInspector/Dashboard` |
| **Customer** (Khách hàng mẫu) | `customer@webvantai.vn` | `P@ssword123` | `/Payment/MyTickets` |
| **Customer** (Khách hàng mẫu 2) | `customer@futabus.vn` | `P@ssword123` | `/Payment/MyTickets` |

> Các tài khoản và role được khởi tạo tự động khi chạy ứng dụng trong `WebVanTai/Data/DbInitializer.cs` và `WebVanTai/Services/ExtraDataSeeder.cs`. Trang đăng nhập: `/Identity/Account/Login`.

## Chạy nhanh dự án (Local)

```bash
dotnet run --project WebVanTai/WebVanTai.csproj --launch-profile https
```

Mở trình duyệt tại `https://localhost:7164` (hồ sơ `https` bắt buộc nếu thử thanh toán VNPay Sandbox để nhận callback `ReturnUrl`).

## Cấu hình Email SMTP (Google App Password)

Để gửi email xác nhận đặt vé (kèm mã QR) và email liên hệ (`/Home/Contact`), điền thông tin tài khoản Gmail và App Password 16 ký tự vào mục `Smtp` trong `WebVanTai/appsettings.json`:

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "EnableSsl": true,
  "SenderName": "G-Trans Express",
  "SenderEmail": "your-email@gmail.com",
  "AppPassword": "xxxx xxxx xxxx xxxx",
  "AdminEmail": "hotro@gtrans.vn"
}
```
