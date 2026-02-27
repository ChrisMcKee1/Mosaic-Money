import { requestJson } from "../../../shared/services/mobileApiClient";

interface HouseholdDto {
  id: string;
}

interface NetWorthHistoryPointDto {
  date: string;
  investmentBalance: number;
}

export interface InvestmentHistoryChartPoint {
  [key: string]: number | string;
  day: number;
  value: number;
  date: string;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function parseHouseholdList(value: unknown): HouseholdDto[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .filter((entry): entry is Record<string, unknown> => isRecord(entry) && typeof entry.id === "string")
    .map((entry) => ({ id: entry.id as string }));
}

function parseNetWorthHistoryList(value: unknown): NetWorthHistoryPointDto[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .filter(
      (entry): entry is Record<string, unknown> =>
        isRecord(entry) &&
        typeof entry.date === "string" &&
        typeof entry.investmentBalance === "number" &&
        Number.isFinite(entry.investmentBalance),
    )
    .map((entry) => ({
      date: entry.date as string,
      investmentBalance: entry.investmentBalance as number,
    }));
}

function isValidIsoDate(value: string): boolean {
  return !Number.isNaN(Date.parse(value));
}

export function mapNetWorthHistoryToInvestmentChartPoints(
  history: NetWorthHistoryPointDto[],
): InvestmentHistoryChartPoint[] {
  return [...history]
    .filter((point) => isValidIsoDate(point.date))
    .sort((left, right) => Date.parse(left.date) - Date.parse(right.date))
    .map((point, index) => ({
      day: index + 1,
      value: point.investmentBalance,
      date: point.date,
    }));
}

export async function fetchInvestmentHistoryChartPoints(
  signal?: AbortSignal,
): Promise<InvestmentHistoryChartPoint[]> {
  const households = await requestJson<HouseholdDto[]>("/api/v1/households", {
    signal,
    parse: parseHouseholdList,
  });

  if (households.length === 0) {
    return [];
  }

  const primaryHouseholdId = households[0].id;
  const history = await requestJson<NetWorthHistoryPointDto[]>(
    `/api/v1/net-worth/history?householdId=${encodeURIComponent(primaryHouseholdId)}&months=6`,
    {
      signal,
      parse: parseNetWorthHistoryList,
    },
  );

  return mapNetWorthHistoryToInvestmentChartPoints(history);
}
