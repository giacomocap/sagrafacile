namespace SagraFacile.WindowsPrinterService.Models
{
    public class PrintJobItem
    {
        public string JobId { get; }
        // public string TargetWindowsPrinterName { get; } // Removed - printer is determined by profile
        public byte[] RawData { get; }
        public DateTime QueuedTime { get; }

        public PrintJobItem(string jobId, byte[] rawData) // targetWindowsPrinterName removed
        {
            JobId = jobId;
            // TargetWindowsPrinterName = targetWindowsPrinterName; // Removed
            RawData = rawData;
            QueuedTime = DateTime.UtcNow;
        }
    }
}
