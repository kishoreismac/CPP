using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Cpp.Api;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options) { public DbSet<Account> Accounts => Set<Account>(); public DbSet<DeliverToLocation> DeliverToLocations => Set<DeliverToLocation>(); public DbSet<Product> Products => Set<Product>(); public DbSet<Order> Orders => Set<Order>(); public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>(); }
public static class SeedData
{
    static string BuildItemNumber(string shortName, int sequence, ISet<int> usedNumericParts)
    {
        var prefix = new string(shortName
          .Where(char.IsLetter)
          .Take(2)
          .Select(char.ToUpperInvariant)
          .ToArray())
          .PadRight(2, 'X');

        for (var attempt = 0; ; attempt++)
        {
            var source = Encoding.UTF8.GetBytes($"{shortName}|{sequence}|{attempt}");
            var hash = SHA256.HashData(source);
            var numericPart = 100000 + (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % 900000);
            if (usedNumericParts.Add(numericPart)) return $"{prefix}{numericPart:000000}";
        }
    }

    public static void Seed(AppDb db)
    {
        db.Database.EnsureCreated();
        var seededAccounts = new[]
        {
         new Account { Id = "adrian", AccountNumber = "9953592", Name = "MFA-ADRIAN", Address = "438 NW County Road 11002, Adrian, MO 64720", City = "Adrian", PostalCode = "64720", SoldToName = "WFU-MEMBER-1001", ContactEmail = "orders.adrian@example.com", ShippingInstructions = "Deliver during business hours. Contact location before unloading." },
         new Account { Id = "boonville", AccountNumber = "9952609", Name = "MFA-BOONVILLE", Address = "1605 Radio Hill Road, Boonville, MO 65233", City = "Boonville", PostalCode = "65233", SoldToName = "WFU-MEMBER-1001", ContactEmail = "orders.boonville@example.com", ShippingInstructions = "Call receiving department thirty minutes before arrival.", RequiresCustomerPo = true },
         new Account { Id = "gallatin", AccountNumber = "9144592", Name = "MFA-GALLATIN", Address = "24395 State Highway 6, Gallatin, MO 64640", City = "Gallatin", PostalCode = "64640", SoldToName = "WFU-MEMBER-1002", ContactEmail = "orders.gallatin@example.com", ShippingInstructions = "Use east receiving entrance." },
         new Account { Id = "columbia", AccountNumber = "9284410", Name = "MFA-COLUMBIA", Address = "1200 Commerce Drive, Columbia, MO 65201", City = "Columbia", PostalCode = "65201", SoldToName = "WFU-MEMBER-1003", ContactEmail = "orders.columbia@example.com", ShippingInstructions = "Appointments required for bulk deliveries.", RequiresCustomerPo = true },
         new Account { Id = "springfield", AccountNumber = "9361180", Name = "MFA-SPRINGFIELD", Address = "3100 West Farm Road, Springfield, MO 65803", City = "Springfield", PostalCode = "65803", SoldToName = "WFU-MEMBER-1004", ContactEmail = "orders.springfield@example.com", ShippingInstructions = "Use the south receiving dock." },
         new Account { Id = "sedalia", AccountNumber = "9472205", Name = "MFA-SEDALIA", Address = "2250 East Broadway Boulevard, Sedalia, MO 65301", City = "Sedalia", PostalCode = "65301", SoldToName = "WFU-MEMBER-1005", ContactEmail = "orders.sedalia@example.com", ShippingInstructions = "Call the receiving desk before delivery.", RequiresCustomerPo = true }
        };
        foreach (var account in seededAccounts.Where(account => !db.Accounts.Any(existing => existing.Id == account.Id))) db.Accounts.Add(account);
        var deliveryAccounts = new[] { ("adrian", "9953592", "Adrian", "64720"), ("boonville", "9952609", "Boonville", "65233"), ("gallatin", "9144592", "Gallatin", "64640"), ("columbia", "9284410", "Columbia", "65201"), ("springfield", "9361180", "Springfield", "65803"), ("sedalia", "9472205", "Sedalia", "65301") };
        foreach (var a in deliveryAccounts)
        {
            var locations = new[] { new DeliverToLocation { Id = $"{a.Item1}-main", AccountNumber = a.Item2 + "-01", Name = $"{a.Item3} Main Receiving", AddressLine1 = $"100 {a.Item3} Receiving Way", City = a.Item3, PostalCode = a.Item4, ContactName = "Receiving Desk", ContactPhone = "555-0100", IsDefault = true, ShipToAccountId = a.Item1 }, new DeliverToLocation { Id = $"{a.Item1}-bulk", AccountNumber = a.Item2 + "-02", Name = $"{a.Item3} Bulk Facility", AddressLine1 = $"200 {a.Item3} Bulk Lane", City = a.Item3, PostalCode = a.Item4, ContactName = "Bulk Coordinator", ContactPhone = "555-0101", ShipToAccountId = a.Item1 } };
            foreach (var location in locations.Where(location => !db.DeliverToLocations.Any(existing => existing.Id == location.Id))) db.DeliverToLocations.Add(location);
        }
        var names = new[] { "WU STERLING BLUE DGA 2.5G", "WU STERLING BLUE DGA 125G", "WU STERLING BLUE 1W 125G", "WU STERLING BLUE 1W 250G", "SEQUENCE 2.5G", "SEQUENCE BULK", "THUNDER MASTER 1W 265G", "THUNDER MASTER 2.5G", "CORNERSTONE 5 PLUS", "CORNERSTONE PLUS BULK", "ROUNDUP POWERMAX 3", "ROUNDUP POWERMAX 3 BULK", "LIBERTY 280 SL", "ATRAZINE 4L", "DICAMBA DMA", "2,4-D AMINE 4", "AZOXYSTROBIN 2SC", "PROPICONAZOLE 4EC", "BIFENTHRIN 2EC", "LAMBDA-CYHALOTHRIN CS", "CROP OIL CONCENTRATE", "NONIONIC SURFACTANT 90", "AMS WATER CONDITIONER", "SOYBEAN INOCULANT PLUS", "CORN BIOLOGICAL STARTER", "Crop Oil Select", "Water Conditioner X", "BioStart Inoculant", "RootRise Biological", "MicroNutrient Zinc", "Foliar Feed 10-10-10", "Nitrogen Stabilizer", "Seed Protect FS", "Orchard Clean", "Pasture Pro" };
        var prices = new[] { 38.27m, 38.05m, 38.05m, 37.55m, 41.72m, 44.83m, 24.15m, 22.88m, 28.45m, 26.90m, 46.25m, 43.75m, 52.35m, 19.85m, 32.75m, 18.95m, 76.40m, 61.25m, 58.30m, 64.15m, 16.70m, 12.45m, 18.20m, 29.80m, 42.50m };
        var inventories = new[] { 145, 18, 7, 4, 62, 500, 10, 38, 74, 280, 22, 0, 31, 44, 12, 48, 19, 8, 36, 13, 92, 118, 160, 24, 6 };
        var usedNumericParts = new HashSet<int>();
        for (var i = 0; i < names.Length; i++)
        {
            var productId = $"p{i + 1:00}";
            var itemNumber = BuildItemNumber(names[i], i + 1, usedNumericParts);
            var existingProduct = db.Products.Find(productId);
            if (existingProduct is not null)
            {
                // Keep existing databases aligned with the searchable mnemonic SKU scheme.
                existingProduct.ItemNumber = itemNumber;
                continue;
            }

            var n = names[i];
            var glyph = i < 12;
            var inventory = i < inventories.Length ? inventories[i] : 60 + i;
            var limited = inventory > 0 && inventory <= 13;
            var unavailable = inventory == 0;
            db.Products.Add(new Product { Id = productId, ItemNumber = itemNumber, Name = n, ShortName = n, Supplier = i < 4 || i is 8 or 9 or 14 || i is >= 20 and <= 22 ? "WinField United" : i is 4 or 5 ? "Syngenta" : i is 6 or 7 ? "Albaugh Chemical Corporation" : i is 10 or 11 ? "Bayer" : i == 12 ? "BASF" : i is 13 or 15 ? "Generic Crop Solutions" : i is 16 or 17 ? "Crop Health Sciences" : i is 18 or 19 ? "FieldGuard" : i is 23 or 24 ? "Biological Crop Systems" : "Demo Ag Sciences", Category = i < 16 ? "Herbicide" : i < 18 ? "Fungicide" : i < 20 ? "Insecticide" : i < 23 ? "Adjuvant" : "Biological/Inoculant", ProductLine = n.Split(' ')[0], ActiveIngredients = glyph ? "Glyphosate" : i == 12 ? "Glufosinate" : i == 13 ? "Atrazine" : i == 14 ? "Dicamba" : i == 15 ? "2,4-D" : "Demonstration active ingredient", Gtin = $"00012345{i:00000}", PackageSize = i is 1 or 2 ? "135 GA IBC(s)" : i is 3 or 6 ? "275 GA IBC(s)" : i is 5 or 9 or 11 ? "Bulk Gallon(s)" : "2 x 2.5 GA Case(s)", Uom = i is 1 or 2 or 3 or 6 ? "IBC" : i is 5 or 9 or 11 ? "GA" : i == 22 ? "BAG" : i is 23 or 24 ? "UNIT" : "CASE", Price = i < prices.Length ? prices[i] : 42.50m + i, PriceUom = i >= 22 ? i == 22 ? "BAG" : "UNIT" : "GA", Favorite = i < 4, AvailableInventory = inventory, StoplightStatus = unavailable ? "No Availability" : limited ? "Limited Availability" : "Available", Orderable = !unavailable });
        }
        var today = DateTime.UtcNow.Date;
        var seededOrders = new[]
        {
            new Order { Id = "DRAFT1001", ShipToAccountId = "adrian", SoldToName = "WFU-MEMBER-1001", ContactEmail = "orders.adrian@example.com", ShippingInstructions = "Call receiving.", Status = OrderState.Draft, CreatedAt = today.AddDays(-1), UpdatedAt = today.AddHours(-3), RequestedArrivalDate = today.AddDays(7), Lines = [new("p01", 2, "CASE", 38.27m, today.AddDays(7))] },
            new Order { Id = "DRAFT1002", ShipToAccountId = "sedalia", SoldToName = "WFU-MEMBER-1005", ContactEmail = "orders.sedalia@example.com", ShippingInstructions = "Call the receiving desk before delivery.", CustomerPo = "PO-SED-1042", Status = OrderState.Draft, CreatedAt = today.AddDays(-2), UpdatedAt = today.AddHours(-5), RequestedArrivalDate = today.AddDays(8), Lines = [new("p23", 8, "BAG", 18.20m, today.AddDays(8))] },
            new Order { Id = "ORDER2001", WebOrderNumber = "WEB-2026-2001", ShipToAccountId = "boonville", SoldToName = "WFU-MEMBER-1001", ContactEmail = "orders.boonville@example.com", ShippingInstructions = "North dock", CustomerPo = "PO-DEMO-1", Status = OrderState.Submitted, CreatedAt = today.AddDays(-5), UpdatedAt = today.AddDays(-2), SubmittedAt = today.AddDays(-2), RequestedArrivalDate = today.AddDays(3), Lines = [new("p04", 4, "IBC", 37.55m, today.AddDays(3)), new("p21", 10, "CASE", 16.70m, today.AddDays(3))] },
            new Order { Id = "ORDER2002", WebOrderNumber = "WEB-2026-2002", ShipToAccountId = "gallatin", SoldToName = "WFU-MEMBER-1002", ContactEmail = "orders.gallatin@example.com", ShippingInstructions = "Use east receiving entrance.", Status = OrderState.Submitted, CreatedAt = today.AddDays(-12), UpdatedAt = today.AddDays(-10), SubmittedAt = today.AddDays(-10), RequestedArrivalDate = today.AddDays(-4), Lines = [new("p05", 3, "CASE", 41.72m, today.AddDays(-4)), new("p13", 2, "CASE", 52.35m, today.AddDays(-4))] },
            new Order { Id = "ORDER2003", WebOrderNumber = "WEB-2026-2003", ShipToAccountId = "columbia", SoldToName = "WFU-MEMBER-1003", ContactEmail = "orders.columbia@example.com", ShippingInstructions = "Appointments required.", CustomerPo = "PO-COL-7781", Status = OrderState.Submitted, CreatedAt = today.AddDays(-20), UpdatedAt = today.AddDays(-18), SubmittedAt = today.AddDays(-18), RequestedArrivalDate = today.AddDays(-12), Lines = [new("p17", 4, "CASE", 76.40m, today.AddDays(-12)), new("p18", 2, "CASE", 61.25m, today.AddDays(-12))] },
            new Order { Id = "ORDER2004", WebOrderNumber = "WEB-2026-2004", ShipToAccountId = "springfield", SoldToName = "WFU-MEMBER-1004", ContactEmail = "orders.springfield@example.com", ShippingInstructions = "Use the south receiving dock.", Status = OrderState.Submitted, CreatedAt = today.AddDays(-30), UpdatedAt = today.AddDays(-28), SubmittedAt = today.AddDays(-28), RequestedArrivalDate = today.AddDays(-22), Lines = [new("p19", 6, "CASE", 58.30m, today.AddDays(-22))] }
        };
        foreach (var seededOrder in seededOrders)
        {
            var existing = db.Orders.Find(seededOrder.Id);
            if (existing is null) db.Orders.Add(seededOrder);
            else if (existing.Lines.Count == 0) existing.Lines = seededOrder.Lines;
        }
        db.SaveChanges();
    }
}
