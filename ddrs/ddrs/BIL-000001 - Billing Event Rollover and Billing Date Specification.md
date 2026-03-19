# Metadata
ID: BIL-000001
Title: Billing Event Rollover and Billing Date Specification
Type: Referential
Summary: This DDR defines the authoritative billing event slicing, rollover, timezone, and billing date rules used to finalize invoice-eligible billing events. It standardizes how open events are created, rolled over, closed, and split when billing timezone context changes.
Status: Draft
Creator: Kevin D. Wolf
References: SYS-000001, SYS-000004, SYS-000010
Creation Date: 2026-03-11T00:00:00.000Z
Last Updated Date: 2026-03-11T00:00:00.000Z
Last Updated By: Kevin D. Wolf

# Body

## 1 - Overview
Summary: Defines the purpose, scope, and architectural intent of the billing event rollover design. This chapter establishes the core model that billing events are stored in UTC while invoice grouping is determined by a billing timezone captured on each event slice.

Purpose
This DDR defines the billing event model used to convert open operational usage into finalized, invoice-eligible billing slices. The design must be financially correct, operationally deterministic, portable across supported relational databases, and easy to explain during reconciliation or support investigations.

Scope
This DDR applies to billing events that represent time-based usage, token/session-based usage, and future quantity-based point-in-time billing facts. It governs event creation, open-event rollover, close/finalization, billing date derivation, timezone capture, and timezone change handling for open billing events.

Architectural intent
The design separates operational timestamps from invoice bucketing semantics:
- Operational timestamps are always stored in UTC.
- Invoice bucketing is based on the billing timezone captured on the billing event slice.
- Finalized billing slices are immutable for billing semantics.

## 2 - Definitions
Summary: Defines the key terms and concepts used throughout this DDR so the implementation and review process use precise and consistent language. These definitions are normative for all chapters that follow.

- Billing Event: A persisted usage fact representing an open or finalized slice of billable activity.
- Open Billing Event: A billing event with no end timestamp and no billing date, indicating the activity is still in progress and not yet invoice-eligible.
- Finalized Billing Slice: A billing event with an end timestamp and billing date, indicating the usage slice is closed and eligible for invoice assembly.
- Billing Date: The date-only value representing the billing day to which a finalized slice belongs.
- Billing Timezone: The timezone used to interpret billing-day boundaries for a billing scope.
- Billing Timezone Id: The persisted identifier representing the billing timezone captured for a specific billing event slice.
- Billing Scope: The billing owner/context used to resolve billing configuration, typically customer first with organization fallback.
- RolloverAtUtc: The UTC instant at which the current open billing slice must be split because the next billing-day boundary has been reached.
- Effective Timestamp: The UTC instant at which a business change, such as a timezone change, becomes active.
- Billing Rule Context: The set of billing semantics governing a slice, including at minimum the billing timezone used to interpret billing-day boundaries.

## 3 - Core Data Model Rules
Summary: Defines the required fields and their billing semantics for billing events. This chapter establishes which values are stored in UTC, which values are derived, and which fields are required to make open and finalized slices self-describing.

Required billing event fields
A compliant billing event model MUST support at minimum the following fields:
- `StartTimestampUtc`
- `EndTimestampUtc` (nullable while open)
- `BillingDate` (nullable while open)
- `BillingTimeZoneId`
- `RolloverAtUtc` (required for rollover-based event types while open)

Field semantics
- `StartTimestampUtc` MUST be stored in UTC.
- `EndTimestampUtc` MUST be stored in UTC when populated.
- `BillingDate` MUST be a date-only value.
- `BillingTimeZoneId` MUST identify the billing timezone rules captured for the slice.
- `RolloverAtUtc` MUST represent the UTC instant of the next billing-day boundary for the open slice.

Nullability rules
- Open billing events MUST have `EndTimestampUtc = null`.
- Open billing events MUST have `BillingDate = null`.
- Finalized billing slices MUST have `EndTimestampUtc != null`.
- Finalized billing slices MUST have `BillingDate != null`.

Timezone representation rules
- Billing timezone capture MUST use a stable timezone identifier, not a naked UTC offset alone.
- A fixed offset MAY be stored as supplemental diagnostic data, but MUST NOT be the sole source of billing-day rules when DST-aware behavior is required.

## 4 - Billing Timezone Resolution Rules
Summary: Defines how the billing timezone is resolved and captured when a billing event slice is created. This chapter makes billing interpretation deterministic by requiring timezone selection to occur once per slice rather than being re-derived during invoice assembly.

Resolution precedence
The billing timezone for a new billing event slice MUST be resolved using the applicable billing scope in this precedence order:
1. Customer billing timezone, when present.
2. Organization billing timezone, when customer timezone is absent.
3. System default only if explicitly defined by implementation policy.

Capture rule
When a new billing event slice is created, the resolved billing timezone MUST be captured on the event as `BillingTimeZoneId`.

Immutability rule
Once a slice has been created, its `BillingTimeZoneId` governs that slice for its full lifetime. Finalized slices MUST NOT be reinterpreted under a different billing timezone.

## 5 - Billing Date Rules
Summary: Defines the meaning and derivation of BillingDate and clarifies when it is populated. This chapter ensures invoice grouping remains simple while keeping open events separate from finalized invoice-eligible slices.

Billing date meaning
`BillingDate` represents the billing day to which a finalized slice belongs, as interpreted in the slice's captured billing timezone.

Population rule
`BillingDate` MUST remain null while the billing event is open.

Finalization rule
When a billing slice is finalized, `BillingDate` MUST be set to the local date of the slice's `StartTimestampUtc`, converted using the slice's captured `BillingTimeZoneId`.

Query rule
Invoice assembly MUST group and filter finalized billing slices by stored `BillingDate`. Invoice assembly MUST NOT re-derive billing day boundaries from UTC timestamps on the fly as the primary mechanism.

## 6 - Rollover Boundary Rules
Summary: Defines how rollover boundaries are calculated and explains why rollover must be based on the billing timezone rather than UTC midnight. This chapter ensures each finalized slice maps to exactly one billing day in its captured billing context.

Boundary interpretation
Billing-day rollover MUST be based on the next midnight in the captured billing timezone for the open slice.

Boundary calculation
For an open slice, `RolloverAtUtc` MUST be calculated by:
1. Converting `StartTimestampUtc` to local time using `BillingTimeZoneId`.
2. Finding the first local midnight strictly after the local start time.
3. Converting that local midnight back to UTC.

Zero-length guardrail
If a slice begins exactly at local midnight, the next rollover boundary MUST be the following local midnight. The implementation MUST NOT create zero-length or negative-length slices.

DST rule
The implementation MUST NOT assume every billing day is 24 hours. Rollover calculation MUST respect the captured billing timezone rules, including DST transitions and other timezone-specific boundary behavior.

## 7 - Event Creation Rules
Summary: Defines the required behavior when a new billing event starts. This chapter turns timezone reasoning into a one-time creation step so open-event processing can remain efficient and deterministic.

On event start, the implementation MUST:
1. Resolve the billing timezone using the billing scope precedence rules.
2. Persist `StartTimestampUtc` in UTC.
3. Persist `BillingTimeZoneId` on the event.
4. Compute and persist `RolloverAtUtc` for the open slice.
5. Leave `EndTimestampUtc` null.
6. Leave `BillingDate` null.

Short-first-slice rule
If an event begins close to the next billing boundary, the first slice MAY be very short. This is valid and MUST NOT be treated as an error condition.

## 8 - Normal Close Rules
Summary: Defines how open billing events are finalized when the underlying activity stops before the next rollover boundary. This chapter also establishes the rule that close logic must be consistent with rollover logic.

If an event closes before `RolloverAtUtc`, the implementation MUST:
1. Set `EndTimestampUtc` to the actual UTC close instant.
2. Set `BillingDate` using the local date of `StartTimestampUtc` in the slice's captured billing timezone.
3. Mark the slice as finalized and invoice-eligible according to implementation policy.

Consistency rule
Close logic and rollover logic MUST produce the same slice semantics. A billing slice finalized by explicit close MUST follow the same billing date and timezone rules as a billing slice finalized by rollover.

## 9 - Rollover Job Rules
Summary: Defines how open billing events are rolled over after they cross a billing-day boundary. This chapter is designed so rollover processing can be implemented efficiently in code using indexed database queries and deterministic slice generation.

Eligibility rule
An open billing event is eligible for rollover processing when `RolloverAtUtc <= nowUtc`.

On rollover, the implementation MUST:
1. Close the current slice at `RolloverAtUtc`.
2. Set `BillingDate` for the finalized slice.
3. Insert a successor open slice with `StartTimestampUtc = previous RolloverAtUtc`.
4. Carry forward the same `BillingTimeZoneId` unless a separate billing rule context change applies.
5. Compute the successor slice's `RolloverAtUtc`.

Execution model
Rollover processing SHOULD be implemented in application code rather than stored procedures so the billing logic remains portable, testable, and consistent across supported databases.

Batching model
Rollover processing SHOULD batch by billing scope, such as customer, so the billing timezone context can be resolved once and reused efficiently during processing.

## 10 - Late Close and Missed Rollover Rules
Summary: Defines the required behavior when an event closes after one or more rollover boundaries but before the rollover job processes it. This chapter protects the invariant that finalized slices may not span more than one billing day.

Invariant
No finalized billing slice may span more than one billing day in its captured billing timezone.

Required behavior
If an event closes after `RolloverAtUtc`, the implementation MUST still split the event at each missed billing boundary before applying the final close.

Example behavior
If a close occurs after one missed rollover boundary, the implementation MUST produce:
- one finalized slice ending at the stored `RolloverAtUtc`, and
- one successor slice starting at that same instant and ending at the actual close time.

If multiple rollover boundaries were missed, the implementation MUST repeat this process until the actual close time has been fully sliced.

Prohibited shortcut
The implementation MUST NOT allow a prior billing slice to absorb time past its billing-day boundary merely because the rollover job had not yet executed.

## 11 - Timezone Change Rules for Open Events
Summary: Defines the required handling when the billing timezone for a billing scope changes while events are still open. This chapter intentionally chooses correctness over convenience because the affected data directly impacts invoiceable money calculations.

Invariant
No billing event slice may span more than one billing rule context.

Required behavior
If the billing timezone for a billing scope changes at an explicit effective UTC timestamp, then for every open billing event in scope the implementation MUST:
1. Close the current open slice at the effective UTC timestamp.
2. Finalize that slice using its existing `BillingTimeZoneId`.
3. Insert a successor open slice beginning at the effective UTC timestamp.
4. Assign the successor slice the new `BillingTimeZoneId`.
5. Compute the successor slice's new `RolloverAtUtc`.

Prohibited behavior
The implementation MUST NOT update `BillingTimeZoneId` in place on an already-open slice in a way that causes a single slice to cover time governed by multiple billing timezone contexts.

Historical stability rule
Finalized slices created before the timezone change MUST remain unchanged.

## 12 - Supported Billing Event Types
Summary: Clarifies how the same billing slice model applies to multiple billing event categories. This chapter ensures the core invariants are preserved even when event types differ in how quantity or duration is measured.

Time-based usage
Time-based resource usage events MUST use the standard open, rollover, and close rules defined in this DDR.

Token/session usage
Token/session usage events MUST use the same slicing rules when the session remains open across billing-day boundaries. Token quantity or other usage totals MAY be written when the slice closes or when session inactivity finalizes the slice.

Point-in-time quantity billing
Point-in-time quantity events MAY omit duration semantics when start/end do not apply operationally. If represented in the same table, they MUST still produce a deterministic `BillingDate` and MUST NOT weaken the billing date and invoice eligibility rules for finalized records.

## 13 - Processing Architecture and Portability
Summary: Records the decision to place billing rollover logic in application code rather than stored procedures. This chapter explains the portability, testability, and long-term maintainability rationale behind that decision.

Decision
Billing rollover, billing date derivation, and timezone-aware slicing logic MUST be implemented in application code.

Rationale
This logic includes evolving business rules, timezone-aware calculations, customer-versus-organization precedence, and cross-database portability requirements. Implementing this logic in application code keeps behavior testable and consistent across SQL Server and PostgreSQL.

Database responsibilities
The database SHOULD be responsible for:
- efficient storage of billing events,
- indexed queries for open and rollover-eligible events,
- transactional persistence of updates and inserted successor slices, and
- invoice assembly queries over finalized slices.

Application responsibilities
Application code SHOULD be responsible for:
- billing timezone resolution,
- rollover boundary calculation,
- event slicing,
- billing date derivation,
- timezone change splitting, and
- retry-safe batch processing.

## 14 - Required Invariants and Compliance Rules
Summary: Collects the mandatory invariants that make the design financially safe, easy to reconcile, and implementation-testable. These rules are normative and MUST be preserved by any implementation.

The implementation MUST preserve all of the following invariants:
- All stored operational timestamps are in UTC.
- Open billing events have null `EndTimestampUtc` and null `BillingDate`.
- Finalized billing slices have non-null `EndTimestampUtc` and non-null `BillingDate`.
- `BillingDate` is derived from the localized start date of the finalized slice using the slice's captured billing timezone.
- No finalized billing slice spans more than one billing day in its captured billing timezone.
- No billing event slice spans more than one billing rule context.
- Finalized billing slices are immutable for billing semantics.
- Missed rollover processing MUST still produce correctly split slices.
- Timezone changes for open events MUST be handled by forced split, not in-place reinterpretation.

## 15 - Testing Guidance
Summary: Defines the minimum categories of test coverage required to validate a compliant implementation. This chapter exists because correctness in billing and timezone logic depends on edge-case validation, not just nominal path testing.

A compliant implementation SHOULD include automated tests for at minimum:
- event start and rollover boundary calculation,
- start exactly at local midnight,
- start immediately before local midnight,
- close before rollover,
- close after one missed rollover,
- close after multiple missed rollovers,
- DST spring-forward day,
- DST fall-back day,
- customer timezone overriding organization timezone,
- organization fallback when customer timezone is absent,
- timezone change while events are open, and
- invoice assembly filtering by stored `BillingDate`.

# Approval
