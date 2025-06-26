namespace SagraFacile.WindowsPrinterService.Models
{
    public enum LocalPrinterType
    {
        /// <summary>
        /// A standard Windows printer that uses GDI (e.g., Laser, Inkjet).
        /// </summary>
        Standard = 0,

        /// <summary>
        /// A printer that accepts raw ESC/POS commands (e.g., most thermal receipt printers).
        /// </summary>
        EscPos = 1
    }
}
