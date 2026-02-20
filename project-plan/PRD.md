====================================================================
FILE 1: prd-agentic-context.md
====================================================================
# Comprehensive Agentic PRD: Mosaic Money
**Context Engineering Note:** This document is optimized for AI coding agents. It explicitly defines architectural boundaries, edge cases, negative constraints, and the trade-offs that must be navigated during implementation to prevent hallucinations and technical debt.

## 1. Product Overview & Core Philosophy
Mosaic Money is a cloud-first, single-entry personal finance application tailored specifically for a 2-user household. 
* **The Philosophy:** Standard apps offer static categorization and look backward. Mosaic Money embraces the messy reality of commingled finances, leverages AI as a conversational historian, and utilizes a robust recurring engine to forecast future cash flow.
* **The Goal:** Provide an uncompromised single source of truth that natively isolates distinct real-world workflows: Schedule C business expenses, strict 50/50 custody settlements, unstructured peer-to-peer reimbursements, and predictive subscription management.

---

## 2. Personas & Top User Journeys
**Personas:**
1. **The Architect:** Needs deep data visibility, API access, automated reimbursement matching, and an AI-driven predictive cash-flow forecast based on the semantic ledger.
2. **The Partner:** Needs clear visibility into shared household cash flow, a frictionless way to separate KDP business supply expenses from personal spending, and a simple UI to review tagged transactions.

**Top Journeys:**
1. Onboard and assign "Ownership" (Yours/Mine/Ours) to imported Plaid accounts.
2. Review the `@NeedsReview` inbox: Approve AI categorizations and clarify ambiguous joint purchases.
3. Chat with the Copilot UI about an unrecognized charge. The AI figures out it was for Cybertruck maintenance, categorizes it, and appends the conversation summary as an `AgentNote`.
4. Review the "Upcoming Bills" dashboard to see how the impending property tax and monthly software subscriptions impact the "Safe to Spend" calculation.

---

## 3. Feature Specifications, Boundaries & Trade-offs

### Module 1: Core Ledger & Dual-Track Notes
**The Context:** A transaction's bank description is rarely enough to understand its purpose six months later.
* **Can Do:** * Implement a `Category` -> `Subcategory` hierarchy.
  * Implement a **Dual-Track Note System**: `UserNote` (manual text) and `AgentNote` (AI summaries).
  * Map raw Plaid JSON payloads into a `RawTransaction` table (`JSONB`), then upsert into a strongly-typed `EnrichedTransaction` table.
* **Cannot Do:** * DO NOT implement a double-entry accounting system (no debits/credits). 
* **Trade-offs & Build-Time Considerations:** * *Data Bloat vs. Context:* Appending every Copilot chat interaction to the `AgentNote` could bloat the database. *Decision:* The agent must strictly synthesize a concise 1-2 sentence summary of the resolution, not the raw chat transcript.
  * *Plaid Webhook Timing:* Pending transactions often change amounts when they post (e.g., adding a tip). The upsert logic must carefully match Plaid's `transaction_id` and handle state changes without destroying user-entered notes.

### Module 2: Semantic Ledger Search & Memory
**The Context:** Users need to query their financial history using natural language (e.g., "How much did I spend on the woodworking hobby last year?").
* **Can Do:** * Concatenate `Description + UserNote + AgentNote + Category` and embed it using PostgreSQL's `azure_ai` extension. Store in a `pgvector` column.
* **Cannot Do:** * DO NOT query Plaid for historical search. All semantic search must happen locally in PostgreSQL.
* **Trade-offs & Build-Time Considerations:**
  * *Compute Cost vs. Real-Time Accuracy:* Generating vector embeddings on every single CRUD operation can throttle the database. *Decision:* Embeddings should be generated asynchronously via an Aspire background queue (RabbitMQ/Redis) immediately after a transaction is saved, rather than blocking the HTTP request.

### Module 3: Recurring & Subscription Forecasting
**The Context:** Users need to know their true liquidity by subtracting upcoming fixed bills and variable utilities from their current balance.
* **Can Do:** * Create a `RecurringItem` entity supporting complex frequencies.
  * Auto-link newly ingested `EnrichedTransaction` records to their parent `RecurringItem` to mark the bill as "Paid" for the current cycle.
* **Cannot Do:** * DO NOT treat `RecurringItem`s as actual ledger transactions. They are forward-looking templates that only affect the ledger when a matching Plaid transaction is ingested.
* **Trade-offs & Build-Time Considerations:**
  * *Strict vs. Fuzzy Matching:* A utility bill is variable. If Austin Energy is expected at $150 but comes in at $300, does the system auto-link it? *Decision:* The matcher must allow for a customizable variance percentage (e.g., +/- 30% for utilities) to auto-link, otherwise it routes to the `@NeedsReview` inbox.
  * *Cadence Drift:* Subscriptions often drift (e.g., billing every 28 days instead of exactly on the 1st). The matching logic must look for +/- 5 days around the expected due date.

### Module 4: Business & Household Isolation
**The Context:** Inventory for the coloring book business is frequently purchased on personal or joint credit cards.
* **Can Do:** * Flag any transaction or split with an `ExcludeFromBudget` boolean.
  * Route these to a distinct `BusinessCategory` (e.g., "KDP Supplies").
  * Surface ambiguous joint-card purchases in a `@NeedsReview` inbox.
* **Cannot Do:** * Business expenses must NEVER be included in the household "Safe to Spend" algorithms.
* **Trade-offs & Build-Time Considerations:**
  * *Total Isolation vs. Cash Flow Reality:* If the KDP business buys a $1,000 iPad on a joint card, the household checking account *did* lose $1,000 of liquidity, even if the budget ignores it. *Decision:* The UI must clearly differentiate between "Household Budget Burn" (green) and "Total Account Liquidity" (lower) to prevent overdrafting on shared accounts.

### Module 5: Sinking Funds & Multi-Month Amortization
**The Context:** Spiky, predictable expenses create artificial anomalies in monthly budget views.
* **Can Do:** * *Amortization:* Visually spread a large, infrequent expense across multiple future months in the budget dashboard.
  * *Sinking Funds:* Create forward-looking YNAB-style savings targets that deduct from the "Safe to Spend" pool.
* **Cannot Do:** * Amortization MUST NOT alter the actual date or amount in the `EnrichedTransaction` table.
* **Trade-offs & Build-Time Considerations:**
  * *API vs. Client Projection:* Who owns the math for amortization? *Decision:* The API should return the raw `TransactionSplit` with `AmortizationMonths` properties. The Next.js frontend is responsible for projecting those fractions across the appropriate UI budget months to keep the API payload lightweight.

### Module 6: Unstructured Reimbursements & Custody Settlements
**The Context:** Inbound transfers arrive without itemized context. Custody arrangements require strict 50/50 splitting of child-related expenses.
* **Can Do:** * Support 1:N linking (one $150 Zelle income linked to three $50 splits).
  * Generate a monthly Custody Settlement tallying all child-tagged expenses and calculating the 50/50 transfer amount owed.
* **Cannot Do:** * The system must never autonomously link and resolve reimbursements without human-in-the-loop validation.
* **Trade-offs & Build-Time Considerations:**
  * *Automation vs. Control:* Users might complain about manually approving the exact same $150 Zelle transfer from a brother every month. *Decision:* Maintain the "Cannot Do" constraint for MVP. In the future, we can introduce a "Trust this Pattern" override, but strictly scoped to specific senders.

### Module 7: Dual-Layer AI Orchestration
**The Context:** The AI acts as a categorization assistant and insight generator. Copilot is strictly the UI.
* **Can Do:** * Utilize PostgreSQL's `azure_ai` extension to instantly normalize merchant names and `pgvector` to find similar historical transactions.
  * Utilize Microsoft Agent Framework (MAF) 1.0 RC graph-based workflows and Microsoft Foundry IQ for unstructured Agentic RAG.
* **Cannot Do:** * The AI has ZERO permission to execute external API calls to send SMS or emails. It may only render a draft payload.
* **Trade-offs & Build-Time Considerations:**
  * *Cost/Latency vs. Intelligence:* LLM API calls are slow and cost money. *Decision:* Implement a strict escalation path. Attempt to categorize using deterministic rules first (0ms latency, $0 cost). Fallback to PostgreSQL Semantic Operators (fast, low cost). Only escalate to the Microsoft Agent Framework for ambiguous or `@NeedsReview` items.