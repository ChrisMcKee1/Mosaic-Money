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
            StableKey: "housing-utilities",
            Name: "Housing & Utilities",
            DisplayOrder: 1,
            Subcategories:
            [
                new TaxonomySubcategorySeed("rent-mortgage", "Rent / Mortgage"),
                new TaxonomySubcategorySeed("home-improvement-repairs", "Home Improvement & Repairs"),
                new TaxonomySubcategorySeed("property-taxes", "Property Taxes"),
                new TaxonomySubcategorySeed("hoa-fees", "HOA Fees"),
                new TaxonomySubcategorySeed("electricity", "Electricity"),
                new TaxonomySubcategorySeed("water-trash", "Water & Trash"),
                new TaxonomySubcategorySeed("natural-gas", "Natural Gas"),
                new TaxonomySubcategorySeed("internet-cable", "Internet & Cable"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "transportation",
            Name: "Transportation",
            DisplayOrder: 2,
            Subcategories:
            [
                new TaxonomySubcategorySeed("auto-loan-lease", "Auto Loan / Lease"),
                new TaxonomySubcategorySeed("gas", "Gas"),
                new TaxonomySubcategorySeed("ev-charging", "EV Charging"),
                new TaxonomySubcategorySeed("car-maintenance-repairs", "Car Maintenance & Repairs"),
                new TaxonomySubcategorySeed("tolls-parking", "Tolls & Parking"),
                new TaxonomySubcategorySeed("public-transit-rideshare", "Public Transit & Rideshare"),
                new TaxonomySubcategorySeed("registration-dmv-fees", "Registration & DMV Fees"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "food-dining",
            Name: "Food & Dining",
            DisplayOrder: 3,
            Subcategories:
            [
                new TaxonomySubcategorySeed("groceries", "Groceries"),
                new TaxonomySubcategorySeed("restaurants", "Restaurants"),
                new TaxonomySubcategorySeed("coffee-shops", "Coffee Shops"),
                new TaxonomySubcategorySeed("alcohol-bars", "Alcohol & Bars"),
                new TaxonomySubcategorySeed("food-delivery", "Food Delivery"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "insurance-healthcare",
            Name: "Insurance & Healthcare",
            DisplayOrder: 4,
            Subcategories:
            [
                new TaxonomySubcategorySeed("health-insurance", "Health Insurance"),
                new TaxonomySubcategorySeed("homeowners-renters-insurance", "Homeowners / Renters Insurance"),
                new TaxonomySubcategorySeed("auto-insurance", "Auto Insurance"),
                new TaxonomySubcategorySeed("life-insurance", "Life Insurance"),
                new TaxonomySubcategorySeed("phone-electronics-insurance", "Phone / Electronics Insurance"),
                new TaxonomySubcategorySeed("doctors-copays", "Doctors & Copays"),
                new TaxonomySubcategorySeed("pharmacy-medications", "Pharmacy & Medications"),
                new TaxonomySubcategorySeed("dental-vision", "Dental & Vision"),
                new TaxonomySubcategorySeed("gym-fitness", "Gym & Fitness"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "shopping-personal-care",
            Name: "Shopping & Personal Care",
            DisplayOrder: 5,
            Subcategories:
            [
                new TaxonomySubcategorySeed("clothing-apparel", "Clothing & Apparel"),
                new TaxonomySubcategorySeed("beauty-personal-care", "Beauty & Personal Care"),
                new TaxonomySubcategorySeed("haircuts-grooming", "Haircuts & Grooming"),
                new TaxonomySubcategorySeed("electronics-gadgets", "Electronics & Gadgets"),
                new TaxonomySubcategorySeed("home-goods-furnishings", "Home Goods & Furnishings"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "subscriptions-services",
            Name: "Subscriptions & Services",
            DisplayOrder: 6,
            Subcategories:
            [
                new TaxonomySubcategorySeed("streaming-services", "Streaming Services"),
                new TaxonomySubcategorySeed("software-ai-services", "Software & AI Services"),
                new TaxonomySubcategorySeed("building-hosting-services", "Building & Hosting Services"),
                new TaxonomySubcategorySeed("delivery-subscriptions", "Delivery Subscriptions"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "entertainment-hobbies",
            Name: "Entertainment & Hobbies",
            DisplayOrder: 7,
            Subcategories:
            [
                new TaxonomySubcategorySeed("woodworking-woodshop", "Woodworking / Woodshop"),
                new TaxonomySubcategorySeed("legos", "Legos"),
                new TaxonomySubcategorySeed("video-games", "Video Games"),
                new TaxonomySubcategorySeed("fantasy-sports-sports-betting", "Fantasy Sports & Sports Betting"),
                new TaxonomySubcategorySeed("live-events-amusement-parks", "Live Events & Amusement Parks"),
                new TaxonomySubcategorySeed("wine-tastings", "Wine & Tastings"),
                new TaxonomySubcategorySeed("movies-theater", "Movies & Theater"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "children-family",
            Name: "Children & Family",
            DisplayOrder: 8,
            Subcategories:
            [
                new TaxonomySubcategorySeed("child-support", "Child Support"),
                new TaxonomySubcategorySeed("extracurriculars-sports", "Extracurriculars & Sports"),
                new TaxonomySubcategorySeed("child-clothing-shoes", "Child Clothing & Shoes"),
                new TaxonomySubcategorySeed("toys-games", "Toys & Games"),
                new TaxonomySubcategorySeed("childcare-babysitting", "Childcare & Babysitting"),
                new TaxonomySubcategorySeed("school-supplies-tuition", "School Supplies & Tuition"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "financial-taxes-fees",
            Name: "Financial, Taxes & Fees",
            DisplayOrder: 9,
            Subcategories:
            [
                new TaxonomySubcategorySeed("federal-irs-taxes", "Federal / IRS Taxes"),
                new TaxonomySubcategorySeed("state-local-taxes", "State / Local Taxes"),
                new TaxonomySubcategorySeed("sales-tax", "Sales Tax"),
                new TaxonomySubcategorySeed("bank-fees", "Bank Fees"),
                new TaxonomySubcategorySeed("credit-card-annual-fees", "Credit Card Annual Fees"),
                new TaxonomySubcategorySeed("accounting-tax-prep", "Accounting & Tax Prep"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "loans-debt",
            Name: "Loans & Debt",
            DisplayOrder: 10,
            Subcategories:
            [
                new TaxonomySubcategorySeed("personal-loans", "Personal Loans"),
                new TaxonomySubcategorySeed("student-loans", "Student Loans"),
                new TaxonomySubcategorySeed("credit-card-payments", "Credit Card Payments"),
                new TaxonomySubcategorySeed("medical-debt", "Medical Debt"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "personal-miscellaneous",
            Name: "Personal & Miscellaneous",
            DisplayOrder: 11,
            Subcategories:
            [
                new TaxonomySubcategorySeed("charities-donations", "Charities & Donations"),
                new TaxonomySubcategorySeed("gifts", "Gifts"),
                new TaxonomySubcategorySeed("pet-care", "Pet Care"),
                new TaxonomySubcategorySeed("mail-logistics", "Mail & Logistics"),
                new TaxonomySubcategorySeed("work-related-expenses", "Work-Related Expenses"),
                new TaxonomySubcategorySeed("cash-withdrawals-atm", "Cash Withdrawals / ATM"),
                new TaxonomySubcategorySeed("unbudgeted-slush-fund", "Unbudgeted / Slush Fund"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "travel-vacation",
            Name: "Travel & Vacation",
            DisplayOrder: 12,
            Subcategories:
            [
                new TaxonomySubcategorySeed("airfare", "Airfare"),
                new TaxonomySubcategorySeed("hotels-lodging", "Hotels & Lodging"),
                new TaxonomySubcategorySeed("rental-cars", "Rental Cars"),
                new TaxonomySubcategorySeed("vacation-dining", "Vacation Dining"),
                new TaxonomySubcategorySeed("experiences-tours", "Experiences & Tours"),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "business-investment",
            Name: "Business & Investment",
            DisplayOrder: 13,
            Subcategories:
            [
                new TaxonomySubcategorySeed("sole-proprietorship-inventory", "Sole Proprietorship / Inventory", IsBusinessExpense: true),
                new TaxonomySubcategorySeed("market-booth-fees", "Market & Booth Fees", IsBusinessExpense: true),
                new TaxonomySubcategorySeed("shipping-packaging-supplies", "Shipping & Packaging Supplies", IsBusinessExpense: true),
                new TaxonomySubcategorySeed("real-estate-investment", "Real Estate Investment", IsBusinessExpense: true),
                new TaxonomySubcategorySeed("crypto-web3-investments", "Crypto / Web3 Investments", IsBusinessExpense: true),
            ]),
        new TaxonomyCategorySeed(
            StableKey: "special-situations",
            Name: "Special Situations",
            DisplayOrder: 14,
            Subcategories:
            [
                new TaxonomySubcategorySeed("major-life-events", "Major Life Events"),
                new TaxonomySubcategorySeed("legal-fees", "Legal Fees"),
                new TaxonomySubcategorySeed("education-certifications", "Education & Certifications"),
                new TaxonomySubcategorySeed("alimony", "Alimony"),
            ]),
    ];
}
