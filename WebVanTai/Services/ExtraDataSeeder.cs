using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;

namespace WebVanTai.Services
{
    /// <summary>
    /// Seed dữ liệu bổ sung cho các tính năng mở rộng (Quản lý vé, tổ lái).
    /// Tách riêng khỏi Data/DbInitializer.cs (thuộc quyền Member 1 — Team Lead)
    /// để không phải sửa file trong vùng Data/; chỉ được gọi sau DbInitializer.SeedAsync.
    /// Idempotent — an toàn khi chạy lại nhiều lần.
    /// </summary>
    public static class ExtraDataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await SeedTicketManagerAsync(roleManager, userManager);
            await SeedCrewMembersAsync(context);
        }

        private static async Task SeedTicketManagerAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            const string roleName = "TicketManager";
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }

            const string email = "ticketmanager@webvantai.vn";
            var existing = await userManager.FindByEmailAsync(email);
            if (existing == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = "Quản lý vé",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, "P@ssword123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, roleName);
                }
            }
        }

        private static async Task SeedCrewMembersAsync(ApplicationDbContext context)
        {
            // 1. Seed danh sách tổ lái nếu chưa có
            if (!await context.CrewMembers.AnyAsync())
            {
                var crew = new List<CrewMember>
                {
                    new CrewMember { FullName = "Nguyễn Văn Tài", PhoneNumber = "0903111222", LicenseNumber = "GPLX-E-51001", Role = CrewRole.Driver, IsActive = true },
                    new CrewMember { FullName = "Trần Hoàng Nam", PhoneNumber = "0903111333", LicenseNumber = "GPLX-E-51002", Role = CrewRole.Driver, IsActive = true },
                    new CrewMember { FullName = "Lê Minh Đức", PhoneNumber = "0903111444", LicenseNumber = "GPLX-E-51003", Role = CrewRole.Driver, IsActive = true },
                    new CrewMember { FullName = "Phạm Quốc Bảo", PhoneNumber = "0903111555", LicenseNumber = "GPLX-E-72004", Role = CrewRole.Driver, IsActive = true },
                    new CrewMember { FullName = "Võ Thanh Tùng", PhoneNumber = "0908222111", Role = CrewRole.Assistant, IsActive = true },
                    new CrewMember { FullName = "Đặng Hữu Phước", PhoneNumber = "0908222333", Role = CrewRole.Assistant, IsActive = true },
                    new CrewMember { FullName = "Huỳnh Tấn Phát", PhoneNumber = "0908222444", Role = CrewRole.Assistant, IsActive = true },
                    new CrewMember { FullName = "Bùi Văn Khoa", PhoneNumber = "0908222555", Role = CrewRole.Assistant, IsActive = true }
                };

                await context.CrewMembers.AddRangeAsync(crew);
                await context.SaveChangesAsync();
            }

            // 2. Tự động phân công tài xế / lơ xe cho các xe chưa có tổ lái
            var drivers = await context.CrewMembers.Where(c => c.Role == CrewRole.Driver && c.IsActive).OrderBy(c => c.CrewMemberId).ToListAsync();
            var assistants = await context.CrewMembers.Where(c => c.Role == CrewRole.Assistant && c.IsActive).OrderBy(c => c.CrewMemberId).ToListAsync();
            var buses = await context.Buses.OrderBy(b => b.BusId).ToListAsync();

            var usedDrivers = buses.Where(b => b.DriverId.HasValue).Select(b => b.DriverId!.Value).ToHashSet();
            var usedAssistants = buses.Where(b => b.AssistantId.HasValue).Select(b => b.AssistantId!.Value).ToHashSet();
            bool updated = false;

            foreach (var bus in buses)
            {
                if (!bus.DriverId.HasValue)
                {
                    var freeDriver = drivers.FirstOrDefault(d => !usedDrivers.Contains(d.CrewMemberId));
                    if (freeDriver != null)
                    {
                        bus.DriverId = freeDriver.CrewMemberId;
                        usedDrivers.Add(freeDriver.CrewMemberId);
                        updated = true;
                    }
                }
                if (!bus.AssistantId.HasValue)
                {
                    var freeAssistant = assistants.FirstOrDefault(a => !usedAssistants.Contains(a.CrewMemberId));
                    if (freeAssistant != null)
                    {
                        bus.AssistantId = freeAssistant.CrewMemberId;
                        usedAssistants.Add(freeAssistant.CrewMemberId);
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                await context.SaveChangesAsync();
            }
        }
    }
}
