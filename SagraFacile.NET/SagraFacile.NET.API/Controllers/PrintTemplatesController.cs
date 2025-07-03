using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    [Route("api/organizations/{organizationId}/print-templates")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class PrintTemplatesController : ControllerBase
    {
        private readonly IPrintTemplateService _printTemplateService;
        private readonly ILogger<PrintTemplatesController> _logger;

        public PrintTemplatesController(IPrintTemplateService printTemplateService, ILogger<PrintTemplatesController> logger)
        {
            _printTemplateService = printTemplateService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<PrintTemplateDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTemplates([FromRoute] Guid organizationId, [FromQuery] QueryParameters queryParameters)
        {
            _logger.LogInformation("Request to get templates for organization {OrgId}", organizationId);
            var (success, result, error) = await _printTemplateService.GetAllAsync(organizationId, queryParameters);
            if (!success)
            {
                _logger.LogWarning("Failed to get templates for org {OrgId}: {Error}", organizationId, error);
                return BadRequest(new { message = error });
            }
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PrintTemplateDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTemplateById([FromRoute] Guid organizationId, [FromRoute] int id)
        {
            _logger.LogInformation("Request to get template {TemplateId} for organization {OrgId}", id, organizationId);
            var (success, template, error) = await _printTemplateService.GetByIdAsync(id, organizationId);
            if (!success)
            {
                _logger.LogWarning("Failed to get template {TemplateId}: {Error}", id, error);
                return NotFound(new { message = error });
            }
            return Ok(template);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PrintTemplateDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateTemplate([FromRoute] Guid organizationId, [FromBody] PrintTemplateUpsertDto createDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _logger.LogInformation("Request to create template for organization {OrgId}", organizationId);
            
            if (organizationId != createDto.OrganizationId)
            {
                return BadRequest("Organization ID mismatch.");
            }

            var (success, createdTemplate, error) = await _printTemplateService.CreateAsync(createDto, organizationId);
            if (!success)
            {
                _logger.LogWarning("Failed to create template: {Error}", error);
                return BadRequest(new { message = error });
            }
            return CreatedAtAction(nameof(GetTemplateById), new { organizationId = createdTemplate.OrganizationId, id = createdTemplate.Id }, createdTemplate);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTemplate([FromRoute] Guid organizationId, [FromRoute] int id, [FromBody] PrintTemplateUpsertDto updateDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _logger.LogInformation("Request to update template {TemplateId} for organization {OrgId}", id, organizationId);

            if (organizationId != updateDto.OrganizationId)
            {
                return BadRequest("Organization ID mismatch.");
            }

            var (success, error) = await _printTemplateService.UpdateAsync(id, updateDto, organizationId);
            if (!success)
            {
                _logger.LogWarning("Failed to update template {TemplateId}: {Error}", id, error);
                return NotFound(new { message = error });
            }
            return NoContent();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTemplate([FromRoute] Guid organizationId, [FromRoute] int id)
        {
            _logger.LogInformation("Request to delete template {TemplateId} for organization {OrgId}", id, organizationId);
            var (success, error) = await _printTemplateService.DeleteAsync(id, organizationId);
            if (!success)
            {
                _logger.LogWarning("Failed to delete template {TemplateId}: {Error}", id, error);
                return NotFound(new { message = error });
            }
            return NoContent();
        }

        [HttpPost("restore-defaults")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RestoreDefaults([FromRoute] Guid organizationId)
        {
            _logger.LogInformation("Request to restore default templates for organization {OrgId}", organizationId);
            var (success, error) = await _printTemplateService.RestoreDefaultHtmlTemplatesAsync(organizationId);
            if (!success)
            {
                _logger.LogError("Failed to restore default templates for org {OrgId}: {Error}", organizationId, error);
                return BadRequest(new { message = error });
            }
            return Ok(new { message = "Default HTML templates restored successfully." });
        }

        [HttpPost("preview")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PreviewTemplate([FromRoute] Guid organizationId, [FromBody] PreviewRequestDto previewRequest)
        {
            _logger.LogInformation("Request to preview template for organization {OrgId}", organizationId);
            var (success, pdfBytes, error) = await _printTemplateService.GeneratePreviewAsync(organizationId, previewRequest);
            if (!success)
            {
                _logger.LogWarning("Failed to generate preview for org {OrgId}: {Error}", organizationId, error);
                return BadRequest(new { message = error });
            }

            return File(pdfBytes, "application/pdf", "template-preview.pdf");
        }
    }
}
