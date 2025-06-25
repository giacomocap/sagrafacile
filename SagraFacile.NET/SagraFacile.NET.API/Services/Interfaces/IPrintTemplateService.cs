using SagraFacile.NET.API.DTOs;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IPrintTemplateService
    {
        Task<(bool Success, PaginatedResult<PrintTemplateDto>? Result, string? Error)> GetAllAsync(int organizationId, QueryParameters queryParameters);
        Task<(bool Success, PrintTemplateDto? Template, string? Error)> GetByIdAsync(int id, int organizationId);
        Task<(bool Success, PrintTemplateDto? CreatedTemplate, string? Error)> CreateAsync(PrintTemplateUpsertDto createDto, int organizationId);
        Task<(bool Success, string? Error)> UpdateAsync(int id, PrintTemplateUpsertDto updateDto, int organizationId);
        Task<(bool Success, string? Error)> DeleteAsync(int id, int organizationId);
        Task<(bool Success, string? Error)> RestoreDefaultHtmlTemplatesAsync(int organizationId);
        Task<(bool Success, byte[]? PdfBytes, string? Error)> GeneratePreviewAsync(int organizationId, PreviewRequestDto previewRequest);
    }
}
