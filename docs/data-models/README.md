# Data Models

This folder contains canonical data model references for Mosaic Money.

## Files
- `ledger-core-model.md`: single-entry ledger, classification, ingestion lineage, and runtime provenance entities.
- `identity-access-model.md`: household membership, identity mapping, and account access control models.

## Rules
- Keep ledger truth immutable and projection logic separate.
- Keep `UserNote` and `AgentNote` separate fields.
- Route ambiguous classifications to review workflows.
