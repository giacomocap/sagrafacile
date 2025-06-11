namespace SagraFacile.NET.API.Utils
{
    public static class OrderIdGenerator
    {
        /// <summary>
        /// Generates a unique, time-based, and somewhat readable Order ID.
        /// Format: ORGID-AREAID-TIMESTAMP_MS-RANDOM8CHARS
        /// </summary>
        /// <param name="organizationId">The ID of the organization.</param>
        /// <param name="areaId">The ID of the area within the organization.</param>
        /// <returns>A formatted Order ID string.</returns>
        public static string Generate(int organizationId, int areaId)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string randomPart = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(); // 8 random chars
            // Format: ORGID-AREAID-TIMESTAMP-RANDOM
            return $"{organizationId}-{areaId}-{timestamp}-{randomPart}";
        }
    }
}
