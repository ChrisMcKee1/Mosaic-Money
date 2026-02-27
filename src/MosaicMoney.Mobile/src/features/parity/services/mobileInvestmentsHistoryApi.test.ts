import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  fetchInvestmentHistoryChartPoints,
  mapNetWorthHistoryToInvestmentChartPoints,
} from "./mobileInvestmentsHistoryApi";

const requestJsonMock = vi.fn();

vi.mock("../../../shared/services/mobileApiClient", () => ({
  requestJson: (...args: unknown[]) => requestJsonMock(...args),
}));

describe("mobileInvestmentsHistoryApi", () => {
  beforeEach(() => {
    requestJsonMock.mockReset();
  });

  it("maps net-worth investment history in chronological order", () => {
    const chartPoints = mapNetWorthHistoryToInvestmentChartPoints([
      { date: "2026-02-03T00:00:00Z", investmentBalance: 300 },
      { date: "2026-02-01T00:00:00Z", investmentBalance: 100 },
      { date: "2026-02-02T00:00:00Z", investmentBalance: 200 },
    ]);

    expect(chartPoints).toEqual([
      { day: 1, value: 100, date: "2026-02-01T00:00:00Z" },
      { day: 2, value: 200, date: "2026-02-02T00:00:00Z" },
      { day: 3, value: 300, date: "2026-02-03T00:00:00Z" },
    ]);
  });

  it("returns empty chart data when no household exists", async () => {
    requestJsonMock.mockResolvedValueOnce([]);

    const chartPoints = await fetchInvestmentHistoryChartPoints();

    expect(chartPoints).toEqual([]);
    expect(requestJsonMock).toHaveBeenCalledTimes(1);
    expect(requestJsonMock).toHaveBeenCalledWith("/api/v1/households", expect.any(Object));
  });

  it("throws when history fetch fails after household resolution", async () => {
    requestJsonMock.mockResolvedValueOnce([{ id: "household-1" }]);
    requestJsonMock.mockRejectedValueOnce(new Error("net worth unavailable"));

    await expect(fetchInvestmentHistoryChartPoints()).rejects.toThrow("net worth unavailable");
    expect(requestJsonMock).toHaveBeenCalledTimes(2);
  });
});
