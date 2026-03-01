import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const workspaceRoot = path.join(__dirname, "..", "..");

const nextConfig = {
  reactStrictMode: true,
  allowedDevOrigins: ["127.0.0.1", "localhost"],
  outputFileTracingRoot: workspaceRoot,
  turbopack: {
    // Keep Turbopack rooted at the web project so Next package resolution is stable in Aspire runs.
    root: __dirname,
  },
};

export default nextConfig;
