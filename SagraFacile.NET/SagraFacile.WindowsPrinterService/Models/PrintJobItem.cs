using System;

namespace SagraFacile.WindowsPrinterService.Models
{
    public class PrintJobItem
    {
        public Guid JobId { get; }
        public byte[] RawData { get; }
        public DateTime QueuedTime { get; }

        public PrintJobItem(Guid jobId, byte[] rawData)
        {
            JobId = jobId;
            RawData = rawData;
            QueuedTime = DateTime.UtcNow;
        }
    }
}
