using Microsoft.Extensions.Logging;
using PdfiumViewer;
using System;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SagraFacile.WindowsPrinterService.Services
{
    public interface IPdfPrintingService
    {
        Task<bool> PrintPdfAsync(string printerName, byte[] pdfData, Guid jobId, string? paperSize = null);
    }

    public class PdfPrintingService : IPdfPrintingService
    {
        private readonly ILogger<PdfPrintingService> _logger;

        public PdfPrintingService(ILogger<PdfPrintingService> logger)
        {
            _logger = logger;
        }

        public Task<bool> PrintPdfAsync(string printerName, byte[] pdfData, Guid jobId, string? paperSize = null)
        {
            _logger.LogInformation($"Attempting to print PDF for job {jobId} to printer '{printerName}' using PdfiumViewer.");

            try
            {
                using (var stream = new MemoryStream(pdfData))
                {
                    // Load the PDF document from the byte array
                    using (var pdfDocument = PdfDocument.Load(stream))
                    {
                        // Create a PrintDocument object
                        using (var printDocument = pdfDocument.CreatePrintDocument())
                        {
                            printDocument.PrinterSettings.PrinterName = printerName;
                            printDocument.DocumentName = $"SagraFacile_Job_{jobId}";

                            // Set the paper size if specified
                            if (!string.IsNullOrEmpty(paperSize))
                            {
                                bool paperSizeSet = false;
                                foreach (PaperSize ps in printDocument.PrinterSettings.PaperSizes)
                                {
                                    if (ps.PaperName.Equals(paperSize, StringComparison.OrdinalIgnoreCase))
                                    {
                                        printDocument.DefaultPageSettings.PaperSize = ps;
                                        paperSizeSet = true;
                                        _logger.LogInformation($"Set paper size to '{paperSize}' for job {jobId}.");
                                        break;
                                    }
                                }
                                if (!paperSizeSet)
                                {
                                    _logger.LogWarning($"Paper size '{paperSize}' not found on printer '{printerName}'. Using printer's default for job {jobId}.");
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"No paper size specified for job {jobId}. Using printer's default.");
                            }

                            // Print the document without showing a print dialog
                            printDocument.Print();
                        }
                    }
                }

                _logger.LogInformation($"Successfully dispatched PDF print job {jobId} to printer '{printerName}'.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing PDF for job {jobId} to printer '{printerName}' using PdfiumViewer.");
                return Task.FromResult(false);
            }
        }
    }
}
