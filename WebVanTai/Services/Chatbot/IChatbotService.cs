using System.Security.Claims;
using WebVanTai.Models.ViewModels;

namespace WebVanTai.Services.Chatbot
{
    public interface IChatbotService
    {
        Task<ChatbotResponse> ProcessMessageAsync(
            string userMessage,
            List<ChatbotMessageDto>? history,
            ClaimsPrincipal user);

        Task<List<string>> GetSuggestionsAsync(ClaimsPrincipal user);
        bool IsOpenAiConfigured();
        string GetModelName();
    }
}
