using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WebVanTai.Models.ViewModels;
using WebVanTai.Services.Chatbot;

namespace WebVanTai.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(IChatbotService chatbotService, ILogger<ChatbotController> logger)
        {
            _chatbotService = chatbotService;
            _logger = logger;
        }

        /// <summary>
        /// Xử lý tin nhắn chat từ người dùng hoặc nhân viên qua OpenAI AI Engine (cấu hình bảo mật trên server)
        /// </summary>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatbotRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { success = false, message = "Tin nhắn không được để trống." });
            }

            try
            {
                var response = await _chatbotService.ProcessMessageAsync(
                    request.Message,
                    request.History,
                    User);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message: {Message}", request.Message);
                return StatusCode(500, new ChatbotResponse
                {
                    Success = false,
                    Reply = "Xin lỗi quý khách, hệ thống đang bận xử lý hoặc kết nối bị gián đoạn. Vui lòng thử lại sau giây lát.",
                    Intent = "error",
                    IsOpenAi = false
                });
            }
        }

        /// <summary>
        /// Lấy danh sách câu hỏi gợi ý phù hợp với vai trò người dùng (Khách vs Nhân viên)
        /// </summary>
        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions()
        {
            var suggestions = await _chatbotService.GetSuggestionsAsync(User);
            return Ok(new { success = true, suggestions });
        }

        /// <summary>
        /// Trả về trạng thái hoạt động của Chatbot và thông tin người dùng
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var isStaff = User.Identity?.IsAuthenticated == true && (
                User.IsInRole("Admin") ||
                User.IsInRole("RouteManager") ||
                User.IsInRole("TicketManager") ||
                User.IsInRole("TicketInspector"));

            return Ok(new
            {
                isOpenAiConfigured = _chatbotService.IsOpenAiConfigured(),
                isAuthenticated = User.Identity?.IsAuthenticated == true,
                userName = User.Identity?.Name,
                isStaff = isStaff
            });
        }
    }
}
