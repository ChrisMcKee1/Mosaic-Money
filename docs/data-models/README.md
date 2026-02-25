# Data Models

This folder contains canonical data model references for Mosaic Money.

## Files
- `ledger-core-model.md`: single-entry ledger entities and relationships.
- `identity-access-model.md`: household membership, ownership, and account access policy models.

## Rules
- Keep ledger truth immutable and projection logic separate.
- Keep `UserNote` and `AgentNote` separate fields.
- Route ambiguous classifications to review workflows.
