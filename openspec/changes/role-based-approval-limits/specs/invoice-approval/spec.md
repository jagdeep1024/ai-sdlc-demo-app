# Invoice Approval Specification

## Purpose

Enforce invoice approval authority by amount and role while retaining distinct-user approval evidence and preserving every invoice field after failed attempts.

## ADDED Requirements

### Requirement: Single-approver limits
The system SHALL allow a Manager or Director to approve pending invoices up to 50,000 inclusive, and only a Director above 50,000 through 500,000 inclusive.

#### Scenario: Inclusive Manager boundary
- **WHEN** a Manager approves a pending invoice for 49,999.99 or 50,000
- **THEN** the invoice becomes Approved with that user's decision identity and timestamp

#### Scenario: Manager exceeds limit
- **WHEN** a Manager attempts approval of a pending invoice for 50,000.01 or 500,000
- **THEN** the attempt fails with a message identifying the Manager limit

#### Scenario: Inclusive Director boundary
- **WHEN** a Director approves a pending invoice for 50,000.01, 499,999.99 or 500,000
- **THEN** the invoice becomes Approved

### Requirement: High-value approval sequence
An invoice above 500,000 SHALL require two approvals. A Manager or Director SHALL be eligible for the first approval; only a different Director SHALL be eligible for the second.

#### Scenario: First approval remains pending
- **WHEN** a Manager or Director first approves a pending invoice for 500,000.01
- **THEN** one approval is recorded, status remains PendingApproval, and final decision fields remain unset

#### Scenario: Different Director completes approval
- **WHEN** a distinct Director approves an invoice with one recorded approval
- **THEN** status becomes Approved, two approvals remain recorded, and final decision fields identify the second Director

#### Scenario: Manager cannot give second approval
- **WHEN** a Manager attempts the second approval
- **THEN** the attempt fails with a reason requiring a Director

### Requirement: Distinct approval identities
The system SHALL never count the same authenticated user twice on one invoice.

#### Scenario: Director repeats approval
- **WHEN** the first Director attempts the second approval using the same identity
- **THEN** the attempt fails with a reason requiring a different approver

### Requirement: Rejected attempts preserve state
Every failed approval attempt SHALL return a clear reason and preserve all invoice fields, including approval history and decision metadata, without a repository update.

#### Scenario: Full-state invariant
- **WHEN** an attempt fails for role, limit, duplicate identity, or invoice state
- **THEN** a subsequent read equals the complete pre-attempt invoice snapshot

#### Scenario: Invalid lifecycle state
- **WHEN** an approval targets a Draft, Approved or Rejected invoice
- **THEN** the attempt fails because only pending invoices can be approved

#### Scenario: Missing invoice
- **WHEN** an approval targets an unknown invoice ID
- **THEN** a not-found response is returned without changing stored invoices

### Requirement: Session authority
Approval identity and role SHALL come from the authenticated server session. Admin and unknown roles SHALL have no approval authority.

#### Scenario: Forged client role
- **WHEN** a signed-in Manager sends approval data claiming a Director identity or role
- **THEN** approval still uses the Manager's server-session identity and limit

#### Scenario: Unauthorized account
- **WHEN** an Admin or unknown role attempts approval
- **THEN** the request is denied

#### Scenario: Anonymous attempt
- **WHEN** a request has no valid session
- **THEN** the request is unauthorized

### Requirement: Concurrent approval integrity
Concurrent approval attempts SHALL observe a serialized invoice transition and SHALL preserve distinct identities and the two-approval maximum.

#### Scenario: Concurrent duplicate requests
- **WHEN** the same Director sends concurrent approval requests for a high-value invoice
- **THEN** only one approval is recorded and the invoice remains pending

### Requirement: Visible progress and failure
The web UI SHALL display approval progress and recorded approvers, and SHALL show the server's clear failure reason.

#### Scenario: Rejected over-limit approval through UI
- **WHEN** a Manager creates and submits an invoice for 50,000.01 and clicks Approve
- **THEN** the UI shows the limit error and the invoice remains pending without approvals after reload

#### Scenario: High-value progress
- **WHEN** one approval is recorded on a high-value invoice
- **THEN** the UI identifies the first approver and the need for a different Director
