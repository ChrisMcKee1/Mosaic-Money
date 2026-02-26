import { SignUp } from "@clerk/nextjs";

export const metadata = {
  title: "Sign Up | Mosaic Money",
};

export default function SignUpPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-[var(--color-background)] p-4">
      <SignUp path="/sign-up" routing="path" signInUrl="/sign-in" />
    </div>
  );
}
