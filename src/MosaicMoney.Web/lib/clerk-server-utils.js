export function isClerkConfiguredCorrectly() {
  const pk = process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY;
  const sk = process.env.CLERK_SECRET_KEY;

  if (!pk || !sk) {
    return false;
  }

  const isLivePk = pk.startsWith("pk_live_");
  const isLiveSk = sk.startsWith("sk_live_");
  const isTestPk = pk.startsWith("pk_test_");
  const isTestSk = sk.startsWith("sk_test_");

  if ((isLivePk && isTestSk) || (isTestPk && isLiveSk)) {
    console.warn("Mosaic Clerk Configuration Error: Mixed instance keys detected. This will cause redirect loops and auth failures. Clerk is disabled.");
    return false;
  }

  return true;
}
