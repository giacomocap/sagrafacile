using SagraFacile.NET.API.Models;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IPdfService
    {
        Task<byte[]> CreatePdfFromHtmlAsync(Order order, string htmlTemplate);
    }
}
