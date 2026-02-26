import { SignIn } from "@clerk/nextjs";

export const metadata = {
  title: "Sign In | Mosaic Money",
};

export default function SignInPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-[var(--color-background)] p-4">
      <SignIn path="/sign-in" routing="path" signUpUrl="/sign-up" />
    </div>
  );
}
