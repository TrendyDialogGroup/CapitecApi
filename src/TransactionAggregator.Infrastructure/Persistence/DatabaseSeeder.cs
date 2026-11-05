using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregator.Domain.Entities;

namespace TransactionAggregator.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, ILogger logger)
    {
        // Relational providers use migrations; in-memory (tests) uses EnsureCreated.
        if (context.Database.IsRelational())
            await context.Database.MigrateAsync();
        else
            await context.Database.EnsureCreatedAsync();

        if (!await context.Categories.AnyAsync())
        {
            logger.LogInformation("Seeding categories and categorization rules...");

            var categories = new[]
            {
                Category.Create("Groceries", "Supermarkets, food stores"),
                Category.Create("Transport", "Ride-hailing, fuel, parking"),
                Category.Create("Dining", "Restaurants, fast food, coffee shops"),
                Category.Create("Utilities", "Electricity, water, internet"),
                Category.Create("Travel", "Hotels, flights, accommodation"),
                Category.Create("Entertainment", "Movies, streaming, gaming"),
                Category.Create("Healthcare", "Pharmacies, clinics, hospitals"),
                Category.Create("Shopping", "Retail, clothing, electronics"),
                Category.Create("Other", "Uncategorised transactions"),
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();

            var cat = categories.ToDictionary(c => c.Name, c => c.Id);

            var rules = new[]
            {
                // Groceries
                CategorizationRule.Create("checkers", cat["Groceries"], priority: 10),
                CategorizationRule.Create("woolworths food", cat["Groceries"], priority: 10),
                CategorizationRule.Create("pick n pay", cat["Groceries"], priority: 10),
                CategorizationRule.Create("spar", cat["Groceries"], priority: 10),
                CategorizationRule.Create("shoprite", cat["Groceries"], priority: 10),

                // Transport
                CategorizationRule.Create("uber", cat["Transport"], priority: 10),
                CategorizationRule.Create("bolt", cat["Transport"], priority: 10),
                CategorizationRule.Create("indriver", cat["Transport"], priority: 10),
                CategorizationRule.Create("engen", cat["Transport"], priority: 8),
                CategorizationRule.Create("shell", cat["Transport"], priority: 8),
                CategorizationRule.Create("bp ", cat["Transport"], priority: 8),

                // Dining
                CategorizationRule.Create("mcdonald", cat["Dining"], priority: 10),
                CategorizationRule.Create("kfc", cat["Dining"], priority: 10),
                CategorizationRule.Create("steers", cat["Dining"], priority: 10),
                CategorizationRule.Create("wimpy", cat["Dining"], priority: 10),
                CategorizationRule.Create("nando", cat["Dining"], priority: 10),
                CategorizationRule.Create("starbucks", cat["Dining"], priority: 10),

                // Utilities
                CategorizationRule.Create("eskom", cat["Utilities"], priority: 10),
                CategorizationRule.Create("city power", cat["Utilities"], priority: 10),
                CategorizationRule.Create("telkom", cat["Utilities"], priority: 10),
                CategorizationRule.Create("mtn", cat["Utilities"], priority: 9),
                CategorizationRule.Create("vodacom", cat["Utilities"], priority: 9),
                CategorizationRule.Create("cell c", cat["Utilities"], priority: 9),

                // Travel
                CategorizationRule.Create("airbnb", cat["Travel"], priority: 10),
                CategorizationRule.Create("booking.com", cat["Travel"], priority: 10),
                CategorizationRule.Create("kulula", cat["Travel"], priority: 10),
                CategorizationRule.Create("flysafair", cat["Travel"], priority: 10),
                CategorizationRule.Create("comair", cat["Travel"], priority: 10),

                // Entertainment
                CategorizationRule.Create("netflix", cat["Entertainment"], priority: 10),
                CategorizationRule.Create("showmax", cat["Entertainment"], priority: 10),
                CategorizationRule.Create("dstv", cat["Entertainment"], priority: 10),
                CategorizationRule.Create("steam", cat["Entertainment"], priority: 10),

                // Healthcare
                CategorizationRule.Create("dischem", cat["Healthcare"], priority: 10),
                CategorizationRule.Create("clicks", cat["Healthcare"], priority: 10),
                CategorizationRule.Create("netcare", cat["Healthcare"], priority: 10),
                CategorizationRule.Create("mediclinic", cat["Healthcare"], priority: 10),

                // Shopping
                CategorizationRule.Create("woolworths", cat["Shopping"], priority: 5), // lower — WW Food wins
                CategorizationRule.Create("mr price", cat["Shopping"], priority: 10),
                CategorizationRule.Create("h&m", cat["Shopping"], priority: 10),
                CategorizationRule.Create("zara", cat["Shopping"], priority: 10),
                CategorizationRule.Create("takealot", cat["Shopping"], priority: 10),
            };

            await context.CategorizationRules.AddRangeAsync(rules);
            await context.SaveChangesAsync();

            logger.LogInformation("Seeding complete — {CatCount} categories, {RuleCount} rules",
                categories.Length, rules.Length);
        }
    }
}
