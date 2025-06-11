namespace SagraFacile.NET.API.Tests.Integration
{
    public static class TestConstants
    {
        // Seeded User Emails
        public const string SuperAdminEmail = "superadmin@test.org";
        public const string Org1AdminEmail = "org1admin@test.org";
        public const string Org2AdminEmail = "org2admin@test.org";
        public const string Org1CashierEmail = "org1cashier@test.org";
        public const string Org1WaiterEmail = "org1waiter@test.org"; // Added Waiter
        // Add other common emails or IDs if needed

        // Seeded User Names (Based on seeding logic in CustomWebApplicationFactory)
        public const string Org1CashierFirstName = "Org1"; // Assuming "Org1" was used
        public const string Org1CashierLastName = "Cashier"; // Assuming "Cashier" was used
        public const string Org1WaiterFirstName = "Org1"; // Added Waiter
        public const string Org1WaiterLastName = "Waiter"; // Added Waiter

        // Seeded Organization IDs (Assuming IDs are 1 and 2 based on previous tests)
        public const int Org1Id = 1;
        public const int Org2Id = 2;
        public const int SystemOrgId = 3; // ID for the System/Admin organization

        // Seeded Organization Names
        public const string Org1Name = "Test Org 1";
        public const string Org2Name = "Test Org 2";
        public const string SystemOrgName = "System"; // Name for the System/Admin organization
        public const string DefaultOrgName = "Sagra di Tencarola"; // Name for the default seeded org

        // Seeded Organization Slugs (Derived from names)
        public const string Org1Slug = "test-org-1";
        public const string Org2Slug = "test-org-2";
        public const string SystemOrgSlug = "system";

        // Seeded Area IDs (Assuming IDs based on previous tests)
        public const int Org1Area1Id = 1;
        public const int Org2Area1Id = 2;
        public const int Org1Area2Id = 3;

        // Seeded Area Names (Based on seeding logic in CustomWebApplicationFactory)
        public const string Org1Area1Name = "Org1 Area 1";
        public const string Org2Area1Name = "Org2 Area 1";
        public const string Org1Area2Name = "Org1 Area 2";

        // Seeded Area Slugs (Derived from names)
        public const string Org1Area1Slug = "org1-area-1";
        public const string Org2Area1Slug = "org2-area-1";
        public const string Org1Area2Slug = "org1-area-2";

        // Seeded Menu Category IDs (Assuming IDs based on seeding)
        public const int Category1Area1Id = 1; // Belongs to Area1 (Org1)
        public const int Category2Area2Id = 2; // Belongs to Area2 (Org2)
        public const int Category3Area3Id = 3; // Belongs to Area3 (Org1)

        // Seeded Menu Item IDs (Assuming IDs based on seeding)
        public const int Item1Cat1Id = 1; // Belongs to Cat1 (Org1)
        public const int Item2Cat1Id = 2; // Belongs to Cat1 (Org1)
        public const int Item3Cat2Id = 3; // Belongs to Cat2 (Org2)
        public const int Item4Cat3Id = 4; // Belongs to Cat3 (Org1)

        // Seeded Order IDs (Using predictable string IDs from CustomWebApplicationFactory)
        public const string SeededOrder1Id = "SEED-ORDER-1"; // Org1/Area1 by CashierOrg1 (Completed)
        public const string SeededOrder2Id = "SEED-ORDER-2"; // Org2/Area2 by Org2Admin (Completed)
        public const string SeededOrder3Id = "SEED-ORDER-3"; // Org1/Area3 by CashierOrg1 (Completed)
        public const string SeededOrder4Id = "SEED-ORDER-4"; // Org1/Area1 PreOrder (PreOrder)
        public const string SeededOrder5Id = "SEED-ORDER-5"; // Org1/Area1 by CashierOrg1 (Paid) - For Waiter Test
        public const string SeededOrder6Id = "SEED-ORDER-6"; // Org1/Area1 PreOrder (PreOrder) - For Waiter Test

        // Seeded Day IDs (Assuming IDs based on seeding - adjust if necessary)
        public const int Org1OpenDayId = 1; // Assuming Day 1 for Org 1 is open
        public const int Org1ClosedDayId = 2; // Assuming Day 2 for Org 1 is closed
        public const int Org2ClosedDayId = 3; // Assuming Day 3 for Org 2 is closed (and no open day initially)

        // Default Password for seeded users (if consistent)
        public const string DefaultPassword = "Password123!"; // Example, adjust if different
    }
}
