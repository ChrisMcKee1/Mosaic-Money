import { createServer } from "node:http";
import { randomUUID } from "node:crypto";

const port = Number(process.env.MM_E2E_MOCK_API_PORT ?? 5055);

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

const fixture = {
  transactionsProjection: [
    {
      id: "tx-1001",
      rawTransactionDate: "2026-02-10",
      description: "Monthly Rent",
      rawAmount: -1200,
      reviewStatus: "Reviewed",
      excludeFromBudget: false,
      userNote: "Core household fixed expense",
      agentNote: "Recurring monthly pattern identified.",
      recurring: {
        isLinked: true,
        frequency: "Monthly",
      },
      reimbursement: {
        hasProposals: false,
        latestStatus: null,
      },
      splits: [],
    },
    {
      id: "tx-1002",
      rawTransactionDate: "2026-02-11",
      description: "Payroll Deposit",
      rawAmount: 3000,
      reviewStatus: "Reviewed",
      excludeFromBudget: false,
      userNote: "Main income",
      agentNote: "Income recognized from known source.",
      recurring: {
        isLinked: false,
        frequency: null,
      },
      reimbursement: {
        hasProposals: false,
        latestStatus: null,
      },
      splits: [],
    },
    {
      id: "tx-1003",
      rawTransactionDate: "2026-02-12",
      description: "Design Software Subscription",
      rawAmount: -80,
      reviewStatus: "Reviewed",
      excludeFromBudget: true,
      userNote: "Business tooling",
      agentNote: "Excluded from household budget by rule.",
      recurring: {
        isLinked: true,
        frequency: "Monthly",
      },
      reimbursement: {
        hasProposals: false,
        latestStatus: null,
      },
      splits: [
        { month: "2026-03", amount: -26.67 },
        { month: "2026-04", amount: -26.67 },
        { month: "2026-05", amount: -26.66 },
      ],
    },
    {
      id: "tx-1004",
      rawTransactionDate: "2026-02-13",
      description: "Medical Co-pay",
      rawAmount: -100,
      reviewStatus: "NeedsReview",
      excludeFromBudget: false,
      userNote: "Should be reimbursable",
      agentNote: "Potential reimbursement detected.",
      recurring: {
        isLinked: false,
        frequency: null,
      },
      reimbursement: {
        hasProposals: true,
        latestStatus: "PendingApproval",
      },
      splits: [],
    },
  ],
  recurringItems: [
    {
      id: "rec-1",
      expectedAmount: 1200,
      frequency: "Monthly",
      isActive: true,
    },
    {
      id: "rec-2",
      expectedAmount: 50,
      frequency: "Monthly",
      isActive: true,
    },
  ],
  reimbursements: [
    {
      id: "rb-1",
      status: "PendingApproval",
      proposedAmount: 100,
    },
  ],
  needsReview: [
    {
      id: "nr-100",
      description: "Unknown Coffee Merchant",
      transactionDate: "2026-02-14",
      amount: -18.25,
      reviewReason: "Ambiguous category between dining and office supplies.",
      userNote: "Looks business related.",
      agentNote: "Low deterministic confidence.",
      needsReviewByUserId: "11111111-1111-1111-1111-111111111111",
    },
    {
      id: "nr-200",
      description: "Generic Transfer",
      transactionDate: "2026-02-15",
      amount: -42,
      reviewReason: "Unable to infer transfer intent confidently.",
      userNote: "Need to classify source.",
      agentNote: "Rule conflict detected.",
      needsReviewByUserId: "22222222-2222-2222-2222-222222222222",
    },
  ],
  taxonomyByScope: {
    User: [
      {
        id: "10000000-0000-0000-0000-000000000001",
        name: "Personal Care",
        displayOrder: 0,
        isSystem: false,
        ownerType: "User",
        householdId: "11111111-1111-1111-1111-111111111111",
        ownerUserId: "11111111-1111-1111-1111-111111111111",
        isArchived: false,
        createdAtUtc: "2026-02-20T00:00:00.000Z",
        lastModifiedAtUtc: "2026-02-20T00:00:00.000Z",
        archivedAtUtc: null,
        subcategories: [
          {
            id: "20000000-0000-0000-0000-000000000001",
            categoryId: "10000000-0000-0000-0000-000000000001",
            name: "Haircuts",
            isBusinessExpense: false,
            displayOrder: 0,
            isArchived: false,
            createdAtUtc: "2026-02-20T00:00:00.000Z",
            lastModifiedAtUtc: "2026-02-20T00:00:00.000Z",
            archivedAtUtc: null,
          },
        ],
      },
    ],
    HouseholdShared: [
      {
        id: "10000000-0000-0000-0000-000000000002",
        name: "Household Groceries",
        displayOrder: 0,
        isSystem: false,
        ownerType: "HouseholdShared",
        householdId: "11111111-1111-1111-1111-111111111111",
        ownerUserId: null,
        isArchived: false,
        createdAtUtc: "2026-02-20T00:00:00.000Z",
        lastModifiedAtUtc: "2026-02-20T00:00:00.000Z",
        archivedAtUtc: null,
        subcategories: [
          {
            id: "20000000-0000-0000-0000-000000000002",
            categoryId: "10000000-0000-0000-0000-000000000002",
            name: "Weekly Groceries",
            isBusinessExpense: false,
            displayOrder: 0,
            isArchived: false,
            createdAtUtc: "2026-02-20T00:00:00.000Z",
            lastModifiedAtUtc: "2026-02-20T00:00:00.000Z",
            archivedAtUtc: null,
          },
        ],
      },
    ],
    Platform: [
      {
        id: "10000000-0000-0000-0000-000000000003",
        name: "Utilities",
        displayOrder: 0,
        isSystem: true,
        ownerType: "Platform",
        householdId: null,
        ownerUserId: null,
        isArchived: false,
        createdAtUtc: "2026-02-20T00:00:00.000Z",
        lastModifiedAtUtc: "2026-02-20T00:00:00.000Z",
        archivedAtUtc: null,
        subcategories: [
          {
            id: "20000000-0000-0000-0000-000000000003",
            categoryId: "10000000-0000-0000-0000-000000000003",
            name: "Water",
            isBusinessExpense: false,
            displayOrder: 0,
            isArchived: false,
            createdAtUtc: "2026-02-20T00:00:00.000Z",
            lastModifiedAtUtc: "2026-02-20T00:00:00.000Z",
            archivedAtUtc: null,
          },
        ],
      },
    ],
  },
};

function createState() {
  return {
    flags: {
      failHealth: false,
      failProjectionMetadata: false,
      failNeedsReview: false,
    },
    data: clone(fixture),
    assistantRunsByConversation: {},
  };
}

let state = createState();

function json(res, statusCode, payload) {
  const body = JSON.stringify(payload);
  res.writeHead(statusCode, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

function empty(res, statusCode = 204) {
  res.writeHead(statusCode);
  res.end();
}

function readJson(req) {
  return new Promise((resolve, reject) => {
    let raw = "";
    req.on("data", (chunk) => {
      raw += chunk;
    });
    req.on("end", () => {
      if (!raw) {
        resolve({});
        return;
      }
      try {
        resolve(JSON.parse(raw));
      } catch (error) {
        reject(error);
      }
    });
    req.on("error", (error) => reject(error));
  });
}

function shouldRequireApproval(message) {
  const value = String(message ?? "").toLowerCase();
  return value.includes("send")
    || value.includes("email")
    || value.includes("wire")
    || value.includes("text");
}

function getConversationRuns(conversationId) {
  if (!Array.isArray(state.assistantRunsByConversation[conversationId])) {
    state.assistantRunsByConversation[conversationId] = [];
  }

  return state.assistantRunsByConversation[conversationId];
}

const server = createServer(async (req, res) => {
  const requestUrl = new URL(req.url ?? "/", `http://${req.headers.host}`);
  const { pathname, searchParams } = requestUrl;

  if (req.method === "GET" && pathname === "/__e2e/ready") {
    json(res, 200, { ready: true });
    return;
  }

  if (req.method === "POST" && pathname === "/__e2e/reset") {
    state = createState();
    empty(res);
    return;
  }

  if (req.method === "POST" && pathname === "/__e2e/scenario") {
    const body = await readJson(req);
    state.flags = {
      ...state.flags,
      ...body,
    };
    json(res, 200, { flags: state.flags });
    return;
  }

  if (req.method === "GET" && pathname === "/api/health") {
    if (state.flags.failHealth) {
      json(res, 503, { status: "error" });
      return;
    }
    json(res, 200, { status: "ok" });
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/households") {
    json(res, 200, [{ id: "11111111-1111-1111-1111-111111111111", name: "Test Household" }]);
    return;
  }

  if (req.method === "GET" && pathname.startsWith("/api/v1/households/") && pathname.endsWith("/members")) {
    json(res, 200, []);
    return;
  }

  if (req.method === "GET" && pathname.startsWith("/api/v1/households/") && pathname.endsWith("/invites")) {
    json(res, 200, []);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/dashboard/metrics") {
    json(res, 200, {});
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/transactions/projection-metadata") {
    if (state.flags.failProjectionMetadata) {
      json(res, 500, { error: "projection metadata unavailable" });
      return;
    }
    json(res, 200, state.data.transactionsProjection);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/recurring") {
    json(res, 200, state.data.recurringItems);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/reimbursements") {
    json(res, 200, state.data.reimbursements);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/net-worth/history") {
    json(res, 200, []);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/investments/accounts") {
    json(res, 200, []);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/liabilities/accounts") {
    json(res, 200, []);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/transactions") {
    const needsReviewOnly = searchParams.get("needsReviewOnly") === "true";

    if (needsReviewOnly) {
      if (state.flags.failNeedsReview) {
        json(res, 500, { error: "needs review unavailable" });
        return;
      }
      json(res, 200, state.data.needsReview);
      return;
    }

    json(res, 200, state.data.transactionsProjection);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/search/categories") {
    const query = (searchParams.get("query") ?? "").toLowerCase();
    const categories = [
      {
        id: "33333333-3333-3333-3333-333333333333",
        name: "Dining Out",
        categoryName: "Food & Dining",
      },
      {
        id: "44444444-4444-4444-4444-444444444444",
        name: "Office Supplies",
        categoryName: "Business",
      },
    ];

    const filtered = categories.filter((category) => {
      return (
        category.name.toLowerCase().includes(query) ||
        category.categoryName.toLowerCase().includes(query)
      );
    });

    json(res, 200, filtered);
    return;
  }

  if (req.method === "GET" && pathname === "/api/v1/categories") {
    const scope = searchParams.get("scope") ?? "User";
    const categories = state.data.taxonomyByScope[scope];

    if (!categories) {
      json(res, 400, {
        error: {
          code: "validation_failed",
          message: "Scope must be one of: Platform, HouseholdShared, User.",
          traceId: "mock-trace-id",
        },
      });
      return;
    }

    json(res, 200, categories);
    return;
  }

  if (req.method === "POST" && pathname === "/api/v1/review-actions") {
    const body = await readJson(req);
    const transactionId = body.transactionId;
    const action = body.action;
    const index = state.data.needsReview.findIndex((item) => item.id === transactionId);

    if (index < 0) {
      json(res, 404, { error: "transaction not found" });
      return;
    }

    if (action === "approve" || action === "reclassify") {
      state.data.needsReview.splice(index, 1);
    } else if (action === "route_to_needs_review") {
      state.data.needsReview[index] = {
        ...state.data.needsReview[index],
        reviewReason: body.reviewReason ?? state.data.needsReview[index].reviewReason,
        needsReviewByUserId: body.needsReviewByUserId ?? state.data.needsReview[index].needsReviewByUserId,
      };
    }

    json(res, 200, {
      id: randomUUID(),
      transactionId,
      action,
      status: "accepted",
    });
    return;
  }

  if (req.method === "POST" && pathname === "/api/v1/plaid/link-tokens") {
    json(res, 200, {
      linkToken: `link-sandbox-${randomUUID()}`,
      linkSessionId: `session-${randomUUID()}`,
    });
    return;
  }

  if (req.method === "POST" && pathname.startsWith("/api/v1/plaid/link-sessions/") && pathname.endsWith("/events")) {
    json(res, 200, {
      accepted: true,
    });
    return;
  }

  if (req.method === "POST" && pathname === "/api/v1/plaid/public-token-exchange") {
    json(res, 200, {
      itemId: `item-${randomUUID()}`,
      institutionId: "ins_1",
      linkedAt: "2026-02-23T12:00:00.000Z",
    });
    return;
  }

  const assistantMessageMatch = pathname.match(/^\/api\/v1\/assistant\/conversations\/([^/]+)\/messages$/);
  if (req.method === "POST" && assistantMessageMatch) {
    const conversationId = assistantMessageMatch[1];
    const body = await readJson(req);
    const message = String(body?.message ?? "").trim();
    if (!message) {
      json(res, 400, { error: "message is required" });
      return;
    }

    const policyDisposition = shouldRequireApproval(message)
      ? "approval_required"
      : "advisory_only";

    const commandId = randomUUID();
    const correlationId = `assistant:11111111111111111111111111111111:${conversationId.replace(/-/g, "")}:${commandId.replace(/-/g, "")}`;
    const now = new Date().toISOString();

    const runs = getConversationRuns(conversationId);
    runs.unshift({
      runId: randomUUID(),
      correlationId,
      status: policyDisposition === "approval_required" ? "NeedsReview" : "Completed",
      triggerSource: "assistant_message_posted",
      failureCode: null,
      failureRationale: null,
      createdAtUtc: now,
      lastModifiedAtUtc: now,
      completedAtUtc: now,
      agentName: "Mosaic",
      agentSource: "foundry",
      agentNoteSummary:
        policyDisposition === "approval_required"
          ? "This high-impact request is waiting for approval."
          : "Mock assistant response: I reviewed your request and captured a deterministic summary.",
      latestStageOutcomeSummary:
        policyDisposition === "approval_required"
          ? "High-impact request requires approval."
          : "Foundry agent invocation completed.",
      assignmentHint: policyDisposition,
    });

    json(res, 202, {
      commandId,
      correlationId,
      conversationId,
      commandType: "assistant_message_posted",
      queue: "runtime-assistant-message-posted",
      policyDisposition,
      queuedAtUtc: now,
      status: "queued",
    });
    return;
  }

  const assistantApprovalMatch = pathname.match(/^\/api\/v1\/assistant\/conversations\/([^/]+)\/approvals\/([^/]+)$/);
  if (req.method === "POST" && assistantApprovalMatch) {
    const conversationId = assistantApprovalMatch[1];
    const approvalId = assistantApprovalMatch[2];
    const body = await readJson(req);
    const decision = String(body?.decision ?? "Approve");
    const now = new Date().toISOString();

    const runs = getConversationRuns(conversationId);
    const pendingRun = runs.find((run) => run.assignmentHint === "approval_required") ?? null;
    if (pendingRun) {
      pendingRun.status = decision.toLowerCase() === "approve" ? "Completed" : "NeedsReview";
      pendingRun.agentNoteSummary = decision.toLowerCase() === "approve"
        ? "Approval received. The action can continue under human-reviewed policy."
        : "Approval rejected. The action remains blocked and requires follow-up review.";
      pendingRun.latestStageOutcomeSummary = decision.toLowerCase() === "approve"
        ? "Human approval submitted."
        : "Human rejection submitted.";
      pendingRun.lastModifiedAtUtc = now;
      pendingRun.completedAtUtc = now;
      pendingRun.assignmentHint = decision.toLowerCase() === "approve"
        ? "approved_by_human"
        : "rejected_by_human";
    }

    json(res, 202, {
      commandId: approvalId,
      correlationId: `assistant:approval:${approvalId}`,
      conversationId,
      commandType: "assistant_approval_submitted",
      queue: "runtime-assistant-message-posted",
      policyDisposition: decision.toLowerCase() === "approve" ? "approved_by_human" : "rejected_by_human",
      queuedAtUtc: now,
      status: "queued",
    });
    return;
  }

  const assistantStreamMatch = pathname.match(/^\/api\/v1\/assistant\/conversations\/([^/]+)\/stream$/);
  if (req.method === "GET" && assistantStreamMatch) {
    const conversationId = assistantStreamMatch[1];
    json(res, 200, {
      conversationId,
      runs: getConversationRuns(conversationId),
    });
    return;
  }

  json(res, 404, {
    error: `No mock route registered for ${req.method} ${pathname}`,
  });
});

server.listen(port, "127.0.0.1", () => {
  // eslint-disable-next-line no-console
  console.log(`MM FE-08 mock API listening on http://127.0.0.1:${port}`);
});
