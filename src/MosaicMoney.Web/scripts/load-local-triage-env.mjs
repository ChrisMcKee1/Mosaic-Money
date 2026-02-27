import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const defaultEnvPath = path.resolve(scriptDir, "..", "triage-partners.env.local");

function unquote(value) {
  if (!value) {
    return "";
  }

  if (
    (value.startsWith('"') && value.endsWith('"')) ||
    (value.startsWith("'") && value.endsWith("'"))
  ) {
    return value.slice(1, -1);
  }

  return value;
}

export function loadLocalTriageEnv() {
  const overridePath = process.env.MM_TRIAGE_ENV_FILE;
  const envPath = overridePath
    ? path.resolve(process.cwd(), overridePath)
    : defaultEnvPath;

  if (!fs.existsSync(envPath)) {
    return;
  }

  const fileText = fs.readFileSync(envPath, "utf8");
  const lines = fileText.split(/\r?\n/);

  for (const rawLine of lines) {
    const line = rawLine.trim();

    if (!line || line.startsWith("#")) {
      continue;
    }

    const separatorIndex = line.indexOf("=");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = line.slice(0, separatorIndex).trim();
    const value = unquote(line.slice(separatorIndex + 1).trim());

    if (!key || process.env[key] !== undefined) {
      continue;
    }

    process.env[key] = value;
  }
}
