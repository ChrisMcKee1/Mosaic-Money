namespace MosaicMoney.Api.Domain.Ledger.Taxonomy;

public sealed record TaxonomyCategorySeed(
    string StableKey,
    string Name,
    int DisplayOrder,
    IReadOnlyList<TaxonomySubcategorySeed> Subcategories);

public sealed record TaxonomySubcategorySeed(
    string StableKey,
    string Name,
    bool IsBusinessExpense = false);

public static class SystemTaxonomySeedManifest
{
    public static IReadOnlyList<TaxonomyCategorySeed> Categories { get; } =
    [
        new TaxonomyCategorySeed(
            StableKey: "housing",
            Name: "Housing",
            DisplayOrder: 1,
            Subcategories:
            [
                new TaxonomySubcategorySeed("rent", "Rent"),
                new TaxonomySubcategorySeed("mortgage", "Mortgage"),
                new TaxonomySubcategorySeed("hoa", "HOA Fees"),
                new TaxonomySubcategorySeed("home-maintenance", "Home Maintenance"),
                new TaxonomySubcategorySeed("property-tax", "Property Tax"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "utilities",
            Name: "Utilities",
            DisplayOrder: 2,
            Subcategories:
            [
                new TaxonomySubcategorySeed("austin-energy", "Austin Energy"),
                new TaxonomySubcategorySeed("water", "Water"),
                new TaxonomySubcategorySeed("gas", "Gas"),
                new TaxonomySubcategorySeed("internet", "Internet"),
                new TaxonomySubcategorySeed("mobile", "Mobile Phone"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "food",
            Name: "Food",
            DisplayOrder: 3,
            Subcategories:
            [
                new TaxonomySubcategorySeed("heb-grocery", "HEB Grocery"),
                new TaxonomySubcategorySeed("costco-grocery", "Costco Grocery"),
                new TaxonomySubcategorySeed("dining", "Dining"),
                new TaxonomySubcategorySeed("coffee", "Coffee"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "transportation",
            Name: "Transportation",
            DisplayOrder: 4,
            Subcategories:
            [
                new TaxonomySubcategorySeed("heb-fuel", "HEB Fuel"),
                new TaxonomySubcategorySeed("fuel", "Fuel"),
                new TaxonomySubcategorySeed("rideshare", "Rideshare"),
                new TaxonomySubcategorySeed("parking", "Parking"),
                new TaxonomySubcategorySeed("vehicle-maintenance", "Vehicle Maintenance"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "healthcare",
            Name: "Healthcare",
            DisplayOrder: 5,
            Subcategories:
            [
                new TaxonomySubcategorySeed("medical", "Medical"),
                new TaxonomySubcategorySeed("pharmacy", "Pharmacy"),
                new TaxonomySubcategorySeed("dental", "Dental"),
                new TaxonomySubcategorySeed("vision", "Vision"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "income",
            Name: "Income",
            DisplayOrder: 6,
            Subcategories:
            [
                new TaxonomySubcategorySeed("payroll", "Payroll"),
                new TaxonomySubcategorySeed("interest-income", "Interest Income"),
                new TaxonomySubcategorySeed("refund", "Refund"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "transfers",
            Name: "Transfers",
            DisplayOrder: 7,
            Subcategories:
            [
                new TaxonomySubcategorySeed("internal-transfer", "Internal Transfer"),
                new TaxonomySubcategorySeed("credit-card-payment", "Credit Card Payment"),
                new TaxonomySubcategorySeed("savings-transfer", "Savings Transfer"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "business",
            Name: "Business",
            DisplayOrder: 8,
            Subcategories:
            [
                new TaxonomySubcategorySeed("business-travel", "Business Travel", IsBusinessExpense: true),
                new TaxonomySubcategorySeed("business-software", "Business Software", IsBusinessExpense: true),
                new TaxonomySubcategorySeed("business-meals", "Business Meals", IsBusinessExpense: true),
                new TaxonomySubcategorySeed("office-supplies", "Office Supplies", IsBusinessExpense: true),
            ]),
    ];
}
