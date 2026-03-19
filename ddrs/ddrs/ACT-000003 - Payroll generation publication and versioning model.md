# Metadata
ID: ACT-000003
Title: Payroll generation publication and versioning model
Type: Referential
Summary: This DDR captures the payroll generation publication model, including how payroll summaries are published, how regeneration avoids mutating the live payroll in place, and why payments are attached to payroll summary generations rather than only to time periods.
Status: Draft
Creator: Kevin D. Wolf
References: SYS-000010
Creation Date: 2026-03-16T16:00:00.000Z
Last Updated Date: 2026-03-16T16:00:00.000Z
Last Updated By: Kevin D. Wolf

# Body
## 1 - Overview
Summary: Defines the publication and versioning model for generated payroll artifacts so payroll generation, regeneration, and downstream reads are consistent and deterministic.
Purpose

This DDR records the payroll generation model agreed during review of `PayrollGenerationService`. The goal is to make the published payroll for a time period unambiguous, prevent regeneration from corrupting the currently trusted payroll, and establish a durable relationship between generated payments and the payroll generation that produced them.

Scope

This DDR applies to payroll generation and regeneration for a `TimePeriod`, including the roles of `TimePeriod`, `PayrollSummary`, and `Payment`.

## 2 - Problem Statement
Summary: The prior model tied payments too directly to the time period and made regeneration vulnerable to deleting the currently trusted state before a replacement was fully ready.
In the prior implementation, payroll generation could reuse or mutate the existing payroll summary and delete existing payments before the replacement generation had completed successfully. Because payments were tied directly to the time period, regeneration risked damaging the currently trusted payroll state mid-process.

This also made it harder to answer the question, “Which payroll is the published payroll for this time period?” without relying on in-process behavior rather than durable model rules.

## 3 - Decision
Summary: Published payroll is defined by the payroll summary assigned to the time period, while payments belong to a specific payroll generation through `PayrollSummaryId`.
The following decisions are adopted:

- `TimePeriod.PayrollSummaryId` is the one published master reference for payroll for that time period.
- A payroll generation is not considered complete until `TimePeriod.PayrollSummaryId` has been assigned and persisted.
- `PayrollSummary` may exist before publication and may carry lifecycle state such as generating, generated, failed, or similar workflow states.
- `Payment` must belong to the payroll generation that created it via `Payment.PayrollSummaryId`.
- `Payment.TimePeriodId` may remain temporarily as a legacy relationship or query convenience, but it is not the authoritative publication/versioning anchor.
- Regeneration must create a replacement payroll summary generation instead of mutating the currently published payroll in place.

## 4 - Publication Model
Summary: Payroll generation publishes by creating a candidate generation first and only assigning it to the time period after successful completion.
The payroll generation ceremony is defined as:

1. Create a new `PayrollSummary` row in a non-final state so its primary key can be used by generated child records.
2. Generate `Payment` records attached to that `PayrollSummaryId`.
3. Finalize summary totals, deductions, and status for that payroll summary.
4. Persist the final `TimePeriod.PayrollSummaryId` assignment as the publication step.
5. Treat the payroll as published and trusted only after that assignment succeeds.

Under this model, the existence of an unpublished `PayrollSummary` does not imply completed payroll. Publication occurs only when the `TimePeriod` points to that summary.

## 5 - Regeneration Model
Summary: Regeneration builds a replacement payroll generation and publishes it by swapping the time period pointer rather than rewriting the currently published payroll in place.
Regeneration must not delete or rewrite the currently published payroll state before a replacement is ready.

Instead, regeneration should:

- create a new candidate `PayrollSummary`
- generate replacement `Payment` records tied to that new `PayrollSummaryId`
- finalize the new summary
- publish by updating `TimePeriod.PayrollSummaryId` to the replacement summary

This approach isolates failed regeneration attempts from the currently trusted payroll. If regeneration fails before publication, the existing published payroll remains the master for that time period.

## 6 - Invariants
Summary: Defines the key invariants future implementations must preserve regardless of internal refactoring.
The following invariants must hold:

- A `TimePeriod` can have only one published payroll summary at a time.
- The payroll summary referenced by `TimePeriod.PayrollSummaryId` is the only payroll summary that downstream readers should trust as authoritative for that time period.
- Published payroll is immutable in principle; replacement occurs by publishing a new payroll summary generation, not by mutating the published one in place.
- A `Payment` belongs to exactly one payroll generation through `PayrollSummaryId`.
- Multiple active pay rates for the same user in the same payroll period are invalid and payroll generation must throw loudly if encountered.

## 7 - Rationale
Summary: Records why the chosen model was preferred over in-place mutation and delete-then-rebuild behavior.
This model was chosen for the following reasons:

- It makes publication explicit and durable.
- It prevents regeneration from damaging the currently trusted payroll before a replacement exists.
- It supports deterministic rebuilds for locked time periods.
- It reduces stale-state risk by favoring new summary generations over resetting and reusing an existing published summary.
- It gives `Payment` a stable parent generation artifact, which simplifies future auditing, cleanup, and downstream reporting.

## 8 - Consequences
Summary: Captures the expected implementation and data model consequences of the adopted publication model.
Expected consequences include:

- `Payment` requires a `PayrollSummaryId` foreign key.
- Queries that need “payments for a time period” should resolve through the published payroll summary for that time period rather than relying solely on `TimePeriodId`.
- `TimePeriodId` may remain on `Payment` temporarily for legacy compatibility, but new logic should prefer `PayrollSummaryId` as the authoritative generation link.
- Payroll generation code must explicitly persist the `TimePeriod.PayrollSummaryId` assignment as the final publication step.
- Regeneration logic becomes replacement-and-publish rather than delete-and-rebuild-in-place.

## 9 - Deferred Follow-Up
Summary: Notes related decisions discussed during review that were intentionally deferred from the core publication/versioning decision.
The following related items were identified but intentionally deferred from this DDR’s primary scope:

- richer payroll tax segment persistence for 941, FUTA, SUTA, and future state/local obligations
- long-term removal of `Payment.TimePeriodId` once legacy usage has been retired
- final naming and lifecycle-state details on `PayrollSummary.Status`

# Approval