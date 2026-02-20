# System Architecture & Agent Guidelines

## 1. Agent Guardrails & 2026 Tech Stack
When generating code for this repository, the AI agent MUST adhere to the following architectural constraints based on the early 2026 technology landscape:
* **Backend:** C# 14 / .NET 10 LTS (v10.0.3+). Use Minimal APIs. 
* **Orchestration:** .NET Aspire 13.2 (Daily Build). The platform is natively polyglot, orchestrating the C# API, Background Workers, and the Next.js frontend via `AddJavaScriptApp`.
* **Agentic Observability:** Aspire 13.2 automatically provisions a **Model Context Protocol (MCP) Server**. This allows the Copilot coding agent to directly query live resources, stream logs, and inspect traces during development without requiring human copy-pasting.
* **Web Frontend:** Next.js 16 LTS (App Router) using React 19 and Tailwind CSS.
* **Mobile Frontend:** React Native via Expo SDK 55. Shares 60-70% of business logic with the web app.
* **Database:** PostgreSQL 18.2+ using Entity Framework Core 10. **Must leverage `azure_ai` extension and `pgvector` with DiskANN indexing.**
* **AI Framework:** Microsoft Agent Framework (MAF) 1.0 RC + `Microsoft.Extensions.AI`. *Note: Copilot is the UI, not the orchestration engine.*

---

## 2. Polyglot System Context & AI Topology

This diagram illustrates the macro-level boundaries. Notice how Aspire 13.2 orchestrates both the UI and API, while the MCP server provides live debug context directly to the coding agent.

```mermaid
flowchart TB
    subgraph Client [Client Applications]
        UI[Next.js 16 Web / Copilot UI]
        Mobile[Expo 55 React Native]
    end

    subgraph AppHost [Aspire 13.2 AppHost]
        API[C# 14 Minimal API]
        Worker[Background Job Worker]
        MCP[Aspire MCP Server]
    end

    subgraph Intelligence Tier [Data & AI Execution]
        DB[(PostgreSQL 18)]
        PGAI[azure_ai & pgvector Semantic Operators]
        DB --- PGAI
        
        MAF[Microsoft Agent Framework 1.0 RC]
        FIQ[Foundry IQ & Azure AI Search]
        MAF --- FIQ
    end

    subgraph Developer [Dev Tools]
        Copilot[Copilot Coding Agent]
    end

    Copilot <-->|Reads Logs/Traces via MCP| MCP
    UI <-->|REST / JWT| API
    Mobile <-->|REST / JWT| API
    
    API <--> DB
    API <--> MAF
    
    Worker <--> DB
    Worker -->|Batch Fetch| Plaid
```

---

## 3. Core Data Model (Entity Relationship Diagram)

This schema maps the relationships required to support single-entry accounting, multi-month amortization, the dual-track note system, and the recurring subscription engine.

```mermaid
erDiagram
    CATEGORY ||--o{ SUBCATEGORY : contains
    SUBCATEGORY ||--o{ ENRICHED_TRANSACTION : categorizes
    
    HOUSEHOLD ||--o{ USER : contains
    HOUSEHOLD ||--o{ ACCOUNT : owns
    HOUSEHOLD ||--o{ RECURRING_ITEM : tracks
    HOUSEHOLD ||--o{ SINKING_FUND : tracks
    USER ||--o{ ENRICHED_TRANSACTION : "NeedsReviewBy"
    ACCOUNT ||--o{ ENRICHED_TRANSACTION : "posts_to"
    RECURRING_ITEM ||--o{ ENRICHED_TRANSACTION : "fulfills"
    
    RECURRING_ITEM {
        uuid Id PK
        string MerchantName
        decimal ExpectedAmount
        boolean IsVariable "True for utilities, False for subscriptions"
        string Frequency "Weekly, Monthly, Annually, etc."
        date NextDueDate
    }
    
    ENRICHED_TRANSACTION ||--o{ TRANSACTION_SPLIT : "can_have"
    ENRICHED_TRANSACTION {
        uuid Id PK
        uuid AccountId FK
        uuid RecurringItemId FK "Nullable. Links actual charge to the subscription template"
        decimal Amount
        date TransactionDate
        uuid SubcategoryId FK
        string UserNote "Human-entered context"
        string AgentNote "AI-generated justification or chat summary"
        boolean ExcludeFromBudget "Business Toggle"
        boolean IsExtraPrincipal "Debt Spike Toggle"
        vector SemanticEmbedding "pgvector indexing for Description + Notes"
        uuid NeedsReviewByUserId FK
    }

    TRANSACTION_SPLIT {
        uuid Id PK
        uuid ParentTransactionId FK
        decimal Amount
        uuid SubcategoryId FK
        string UserNote
        int AmortizationMonths
    }
```

---

## 4. C# Entity Framework Core Context (Grounding Snippet)
*Agent Instruction: Utilize C# 14 features like the `field` keyword for simplified property validation. Strictly adhere to this single-entry ledger model.*

```csharp
public class EnrichedTransaction 
{
    public Guid Id { get; set; }
    public string PlaidTransactionId { get; set; } = string.Empty; // Idempotency key
    public decimal Amount { get; set; }
    
    // C# 14 Field-Backed Property for validation
    public DateTime Date 
    { 
        get; 
        set => field = value > DateTime.UtcNow ? throw new ArgumentException("Future dates invalid") : value; 
    }
    
    // Attribution & Hierarchy
    public Guid? SubcategoryId { get; set; }
    public Guid? RecurringItemId { get; set; }
    public Guid? NeedsReviewByUserId { get; set; } 
    
    // Dual-Track Notes
    public string? UserNote { get; set; }
    public string? AgentNote { get; set; }
    
    // Isolation Toggles
    public bool ExcludeFromBudget { get; set; } 
    public bool IsExtraPrincipal { get; set; }
    
    // Concurrency & Audit
    public DateTime LastModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = "System"; 
}
```

---

## 5. Semantic Ingestion & Recurring Matcher Flow

This flow demonstrates how the Aspire worker detects if an incoming transaction fulfills an expected subscription, marking it "paid" for the month, while simultaneously using PostgreSQL's semantic operators to vectorize notes.

```mermaid
sequenceDiagram
    participant Plaid as Plaid API
    participant Worker as Aspire Worker (.NET 10)
    participant DB as PostgreSQL 18
    participant MAF as Microsoft Agent Framework

    Worker->>Plaid: Fetch /transactions/sync
    Plaid-->>Worker: Return JSON delta
    Worker->>DB: INSERT into RawTransaction (JSONB)
    
    Note over DB: Step 1: In-Database Classification & Embedding
    Worker->>DB: UPDATE EnrichedTransaction SET Subcategory = azure_ai.extract(RawDescription)
    Worker->>DB: UPDATE EnrichedTransaction SET SemanticEmbedding = azure_ai.embed(Description + UserNote + AgentNote)
    
    Note over Worker,DB: Step 2: Recurring Bill Detection
    Worker->>DB: Query Active RecurringItems due within +/- 5 days
    Worker->>DB: Link EnrichedTransaction.RecurringItemId if matched
    DB-->>Worker: Update NextDueDate on RecurringItem
    
    Note over Worker,MAF: Step 3: Fallback for Ambiguous Items
    Worker->>DB: Check for Low Confidence (< 0.85)
    alt Is Ambiguous
        Worker->>MAF: Trigger Graph Workflow
        MAF-->>Worker: Return Contextual Suggestion
        Worker->>DB: Tag @NeedsReviewByUserId
    end
```

---

## 6. Agentic Human-in-the-Loop (HITL) Communication Flow
**Context Constraint:** The MAF agent must NEVER execute external messaging APIs autonomously. It acts purely as a drafter and memory-keeper.

```mermaid
sequenceDiagram
    participant DB as Postgres Ledger
    participant MAF as Agent Framework 1.0 RC
    participant UI as Copilot UI (User)
    participant Thomas as Thomas (External)

    DB->>MAF: Trigger Categorization on $150 Zelle
    MAF->>UI: Flag as @NeedsReview (Reason: Unknown Zelle)
    
    UI->>MAF: "I don't know what this is. Draft a text to Thomas."
    Note over MAF: Foundry IQ checks historical context
    MAF->>UI: Returns Draft: "Hey man, what was the $150 Zelle for?"
    
    Note over UI,MAF: HARD STOP: Agent cannot send. Human must intervene.
    
    UI->>Thomas: User copies draft & sends SMS manually via phone
    Thomas-->>UI: Replies: "For the Austin Energy and HEB groceries"
    
    UI->>MAF: User forwards reply: "He said it's for Austin Energy and HEB"
    MAF->>DB: Uses pgvector to find the exact Austin Energy & HEB expenses
    MAF->>UI: Proposes 1:N Link (Match $50 to Austin Energy, $100 to HEB)
    
    UI->>DB: User clicks "Approve Link"
    MAF->>DB: Append AgentNote: "1:N Link approved by user after SMS clarification with Thomas on 02/20."
```

---

## 7. Needs Review State Machine
This dictates how the API handles ambiguous transactions (e.g., joint card purchases that might be for the KDP business). 

```mermaid
stateDiagram-v2
    [*] --> Ingested: Plaid Worker
    Ingested --> PostgresAI: azure_ai.generate() / pgvector similarity
    
    PostgresAI --> Categorized: High Confidence Semantic Match
    PostgresAI --> MAF_Router: Low Confidence / Ambiguous
    
    MAF_Router --> Categorized: Agent Context Match > 0.90
    MAF_Router --> NeedsReview: Agent Confidence < 0.90 OR Rule = "Always Flag Joint Card"
    
    state NeedsReview {
        [*] --> PartnerInbox
        PartnerInbox --> TaggedBusiness: ExcludeFromBudget = True
        PartnerInbox --> Split: Broken into multiple categories
        PartnerInbox --> Accepted: User confirms AI suggestion
    }
    
    NeedsReview --> Categorized
    Categorized --> [*]
```

---

## 8. Architectural Trade-offs & Implementation Directives
* **Next.js vs. Blazor:** We are using Next.js because React Native (Expo) allows us to share 60-70% of business logic and UI components with the mobile app. Blazor lacks this robust mobile code-sharing ecosystem.
* **Single-Entry vs. Double-Entry:** The agent MUST NOT implement a double-entry accounting system. Plaid transactions are single-entry. Implementing double-entry for a personal finance app triples database complexity with zero added value for our core features (attribution, budgeting, and recurring tracking).
* **In-Database AI vs. API LLM Calls:** The agent MUST prioritize PostgreSQL's `azure_ai` extension for standard categorization and vector search. LLM API calls via the Agent Framework are slow and expensive; they should be strictly reserved as a fallback for the `@NeedsReview` queue or complex conversational reasoning.