namespace SagraFacile.WindowsPrinterService.Models
{
    public class PrintJobItem
    {
        public string JobId { get; }
        public string TargetWindowsPrinterName { get; }
        public byte[] RawData { get; }
        public DateTime QueuedTime { get; }

        public PrintJobItem(string jobId, string targetWindowsPrinterName, byte[] rawData)
        {
            JobId = jobId;
            TargetWindowsPrinterName = targetWindowsPrinterName;
            RawData = rawData;
            QueuedTime = DateTime.UtcNow;
        }
    }
}
