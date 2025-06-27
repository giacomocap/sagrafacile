using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Models;
using SagraFacile.WindowsPrinterService.Printing;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SagraFacile.WindowsPrinterService.Services
{
    public interface IPrintJobManager
    {
        void EnqueueJob(PrintJobItem jobItem);
        PrintJobItem? DequeueJob();
        int GetQueueCount();
        Task<bool> ProcessJobAsync(PrintJobItem jobItem, string printerName, string contentType);
        event EventHandler<int>? QueueCountChanged;
    }

    public class PrintJobManager : IPrintJobManager
    {
        private readonly ILogger<PrintJobManager> _logger;
        private readonly IRawPrinter _rawPrinter;
        private readonly IPdfPrintingService _pdfPrintingService;
        private readonly ConcurrentQueue<PrintJobItem> _printQueue = new ConcurrentQueue<PrintJobItem>();

        public event EventHandler<int>? QueueCountChanged;

        public PrintJobManager(
            ILogger<PrintJobManager> logger, 
            IRawPrinter rawPrinter, 
            IPdfPrintingService pdfPrintingService)
        {
            _logger = logger;
            _rawPrinter = rawPrinter;
            _pdfPrintingService = pdfPrintingService;
        }

        public void EnqueueJob(PrintJobItem jobItem)
        {
            if (jobItem == null)
            {
                _logger.LogWarning("Attempted to enqueue null job item.");
                return;
            }

            _printQueue.Enqueue(jobItem);
            _logger.LogInformation($"Job {jobItem.JobId} enqueued. Queue count: {_printQueue.Count}");
            QueueCountChanged?.Invoke(this, _printQueue.Count);
        }

        public PrintJobItem? DequeueJob()
        {
            if (_printQueue.TryDequeue(out PrintJobItem? jobItem))
            {
                _logger.LogInformation($"Dequeued job {jobItem.JobId}. Remaining in queue: {_printQueue.Count}");
                QueueCountChanged?.Invoke(this, _printQueue.Count);
                return jobItem;
            }

            _logger.LogDebug("Attempted to dequeue job, but queue is empty.");
            return null;
        }

        public int GetQueueCount()
        {
            return _printQueue.Count;
        }

        public async Task<bool> ProcessJobAsync(PrintJobItem jobItem, string printerName, string contentType)
        {
            if (jobItem == null)
            {
                _logger.LogError("ProcessJobAsync called with null jobItem.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(printerName))
            {
                _logger.LogError($"Cannot process job {jobItem.JobId}: No target printer name specified.");
                return false;
            }

            _logger.LogInformation($"Processing job {jobItem.JobId} for printer '{printerName}' with content type '{contentType}'.");

            try
            {
                bool success;
                if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    success = await _pdfPrintingService.PrintPdfAsync(printerName, jobItem.RawData, jobItem.JobId, jobItem.PaperSize);
                }
                else // Default to raw/escpos
                {
                    success = await _rawPrinter.PrintRawAsync(printerName, jobItem.RawData);
                }

                if (success)
                {
                    _logger.LogInformation($"Successfully processed job {jobItem.JobId} to {printerName}.");
                }
                else
                {
                    _logger.LogError($"Failed to process job {jobItem.JobId} to {printerName}.");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while processing job {jobItem.JobId}: {ex.Message}");
                return false;
            }
        }
    }
}
