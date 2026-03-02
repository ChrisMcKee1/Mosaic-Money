# Identity And Access Model

## Purpose
Define household identity, membership, and account-level access controls required by M7 scope enforcement.

## Core Entities
- `Household`: top-level scope for users and accounts.
- `MosaicUser`: canonical authenticated person (`AuthProvider` + `AuthSubject`).
- `HouseholdUser`: household membership record and lifecycle status.
- `Account`: financial account associated with a household.
- `AccountMemberAccess`: explicit per-account access role and visibility grant.
- `AccountAccessPolicyReviewQueueEntry`: fail-closed queue record when account access policy requires review.

## Rules
- Identity mapping is explicit and auditable via unique `MosaicUser(AuthProvider, AuthSubject)`.
- Membership is status-aware (`Active`, `Invited`, `Removed`) and one active membership per `(HouseholdId, MosaicUserId)` is enforced.
- Account permissions are explicit per `(AccountId, HouseholdUserId)` with role/visibility consistency checks.
- Ambiguous access decisions route to `AccountAccessPolicyReviewQueueEntry` and keep `Account.AccessPolicyNeedsReview` fail-closed until resolved.

```mermaid
erDiagram
    MOSAIC_USER ||--o{ HOUSEHOLD_USER : links
    HOUSEHOLD ||--o{ HOUSEHOLD_USER : has
    HOUSEHOLD ||--o{ ACCOUNT : owns
    ACCOUNT ||--o{ ACCOUNT_MEMBER_ACCESS : granted_to
    HOUSEHOLD_USER ||--o{ ACCOUNT_MEMBER_ACCESS : receives
    ACCOUNT ||--o| ACCOUNT_ACCESS_POLICY_REVIEW_QUEUE_ENTRY : escalates
    HOUSEHOLD ||--o{ ACCOUNT_ACCESS_POLICY_REVIEW_QUEUE_ENTRY : scopes

    MOSAIC_USER {
        uuid Id PK
        string AuthProvider
        string AuthSubject
        string Email
        string DisplayName
        bool IsActive
    }

    HOUSEHOLD_USER {
        uuid Id PK
        uuid HouseholdId FK
        uuid MosaicUserId FK
        string DisplayName
        string ExternalUserKey
        int MembershipStatus
    }

    ACCOUNT {
        uuid Id PK
        uuid HouseholdId FK
        string Name
        string ExternalAccountKey
        bool AccessPolicyNeedsReview
    }

    ACCOUNT_MEMBER_ACCESS {
        uuid AccountId FK
        uuid HouseholdUserId FK
        int AccessRole
        int Visibility
    }

    ACCOUNT_ACCESS_POLICY_REVIEW_QUEUE_ENTRY {
        uuid AccountId PK
        uuid HouseholdId FK
        string ReasonCode
        string Rationale
        datetime ResolvedAtUtc
    }
```
