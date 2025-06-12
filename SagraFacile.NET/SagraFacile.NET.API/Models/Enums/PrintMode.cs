namespace SagraFacile.NET.API.Models.Enums
{
    public enum PrintMode
    {
        /// <summary>
        /// Printer prints jobs immediately upon receipt.
        /// </summary>
        Immediate,

        /// <summary>
        /// For Windows USB printers, jobs are queued in the companion app and printed on demand by staff.
        /// </summary>
        OnDemandWindows
    }
}
