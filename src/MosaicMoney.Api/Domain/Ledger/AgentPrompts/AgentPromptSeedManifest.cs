namespace MosaicMoney.Api.Domain.Ledger.AgentPrompts;

public sealed record AgentPromptSeed(
    string StableKey,
    string Title,
    string PromptText,
    int DisplayOrder);

public static class AgentPromptSeedManifest
{
    public static IReadOnlyList<AgentPromptSeed> Prompts { get; } =
    [
        new AgentPromptSeed(
            StableKey: "weekly-cash-flow-summary",
            Title: "Weekly cash flow summary",
            PromptText: "Summarize my inflows and outflows for the last 7 days with any notable spikes.",
            DisplayOrder: 0),
        new AgentPromptSeed(
            StableKey: "needs-review-transactions",
            Title: "Transactions needing review",
            PromptText: "Show transactions that likely need review and explain why each one is ambiguous.",
            DisplayOrder: 1),
        new AgentPromptSeed(
            StableKey: "safe-to-spend-update",
            Title: "Safe-to-spend update",
            PromptText: "Give me a safe-to-spend update for this week and call out major risks.",
            DisplayOrder: 2),
        new AgentPromptSeed(
            StableKey: "subscription-leak-check",
            Title: "Subscription leak check",
            PromptText: "Find recurring charges that look underused or duplicated and estimate monthly savings.",
            DisplayOrder: 3),
        new AgentPromptSeed(
            StableKey: "budget-drift-alert",
            Title: "Budget drift alert",
            PromptText: "Compare this month against my recent baseline and flag categories drifting above trend.",
            DisplayOrder: 4),
        new AgentPromptSeed(
            StableKey: "upcoming-bills-watch",
            Title: "Upcoming bills watch",
            PromptText: "List upcoming recurring bills over the next 14 days and highlight anything likely to miss.",
            DisplayOrder: 5),
    ];
}
