using PuppeteerSharp;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using Scriban;
using QRCoder;
using System.IO;
using System;
using System.Linq;
using PuppeteerSharp.Media;

namespace SagraFacile.NET.API.Services
{
    public class PdfService : IPdfService
    {
        private readonly ILogger<PdfService> _logger;

        public PdfService(ILogger<PdfService> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> CreatePdfFromHtmlAsync(Order order, string htmlTemplate, string? paperSize = null)
        {
            try
            {
                // 1. Populate HTML template with Scriban
                var template = Template.Parse(htmlTemplate);
                
                var itemsByCategory = order.OrderItems
                    .GroupBy(oi => oi.MenuItem?.MenuCategory?.Name ?? "Senza Categoria")
                    .Select(g => new { CategoryName = g.Key, Items = g.ToList() })
                    .ToList();

                var scribanModel = new {
                    order,
                    items_by_category = itemsByCategory,
                    qr_code_base64 = GenerateQrCodeBase64(order.Id)
                };
                var finalHtml = await template.RenderAsync(scribanModel);

                // 2. Generate PDF from HTML using Puppeteer Sharp
                // The browser fetch should be done at startup, not per-request.
                await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } });
                await using var page = await browser.NewPageAsync();
                
                await page.SetContentAsync(finalHtml);

                var pdfOptions = new PdfOptions();
                if (!string.IsNullOrEmpty(paperSize))
                {
                    var format = GetPaperFormat(paperSize);
                    if (format is not null)
                    {
                        pdfOptions.Format = format;
                    }
                    else
                    {
                        _logger.LogWarning("Unsupported paper size '{PaperSize}'. Using default.", paperSize);
                    }
                }

                var pdfData = await page.PdfDataAsync(pdfOptions);

                return pdfData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create PDF for Order ID {OrderId}", order.Id);
                throw;
            }
        }

        private string GenerateQrCodeBase64(string orderId)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode($"Sagrafacile_Order_{orderId}", QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImageBytes = qrCode.GetGraphic(20);
                return $"data:image/png;base64,{Convert.ToBase64String(qrCodeImageBytes)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate QR code for Order ID: {OrderId}", orderId);
                return string.Empty; // Return empty string if QR generation fails
            }
        }

        private static PaperFormat? GetPaperFormat(string paperSize)
        {
            return paperSize.ToLowerInvariant() switch
            {
                "a4" => PaperFormat.A4,
                "a5" => PaperFormat.A5,
                "letter" => PaperFormat.Letter,
                "legal" => PaperFormat.Legal,
                "tabloid" => PaperFormat.Tabloid,
                "ledger" => PaperFormat.Ledger,
                "a0" => PaperFormat.A0,
                "a1" => PaperFormat.A1,
                "a2" => PaperFormat.A2,
                "a3" => PaperFormat.A3,
                "a6" => PaperFormat.A6,
                _ => null,
            };
        }
    }
}
