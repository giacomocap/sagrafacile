namespace SagraFacile.WindowsPrinterService.Models
{
    public class PrintJobItem
    {
        public string JobId { get; }
        public byte[] RawData { get; }
        public DateTime QueuedAt { get; }

        public PrintJobItem(string jobId, byte[] rawData)
        {
            JobId = jobId;
            RawData = rawData;
            QueuedAt = DateTime.UtcNow;
        }
    }
}
