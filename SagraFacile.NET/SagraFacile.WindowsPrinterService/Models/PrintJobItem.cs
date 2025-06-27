using System;

namespace SagraFacile.WindowsPrinterService.Models
{
    public class PrintJobItem
    {
        public Guid JobId { get; }
        public byte[] RawData { get; }
        public string ContentType { get; }
        public string? PaperSize { get; }
        public DateTime QueuedTime { get; }

        public PrintJobItem(Guid jobId, byte[] rawData, string contentType = "application/vnd.escpos", string? paperSize = null)
        {
            JobId = jobId;
            RawData = rawData;
            ContentType = contentType;
            PaperSize = paperSize;
            QueuedTime = DateTime.UtcNow;
        }
    }
}
