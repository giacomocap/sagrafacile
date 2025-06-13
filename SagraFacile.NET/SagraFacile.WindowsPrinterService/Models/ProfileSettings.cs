namespace SagraFacile.WindowsPrinterService.Models
{
    public class ProfileSettings
    {
        public string? ProfileName { get; set; } // Name of the profile, used for the filename
        public string? SelectedPrinter { get; set; }
        public string? HubHostAndPort { get; set; }
        public string? InstanceGuid { get; set; }
    }
}
