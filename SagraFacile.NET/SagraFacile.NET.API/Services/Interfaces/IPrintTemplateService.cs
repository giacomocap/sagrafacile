using SagraFacile.NET.API.DTOs;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IPrintTemplateService
    {
        Task<(bool Success, PaginatedResult<PrintTemplateDto>? Result, string? Error)> GetAllAsync(Guid organizationId, QueryParameters queryParameters);
        Task<(bool Success, PrintTemplateDto? Template, string? Error)> GetByIdAsync(int id, Guid organizationId);
        Task<(bool Success, PrintTemplateDto? CreatedTemplate, string? Error)> CreateAsync(PrintTemplateUpsertDto createDto, Guid organizationId);
        Task<(bool Success, string? Error)> UpdateAsync(int id, PrintTemplateUpsertDto updateDto, Guid organizationId);
        Task<(bool Success, string? Error)> DeleteAsync(int id, Guid organizationId);
        Task<(bool Success, string? Error)> RestoreDefaultHtmlTemplatesAsync(Guid organizationId);
        Task<(bool Success, byte[]? PdfBytes, string? Error)> GeneratePreviewAsync(Guid organizationId, PreviewRequestDto previewRequest);
    }
}
