# Metadata
ID: SYS-000012
Title: Date and Time Handling Policy
Type: Policy / Rules / Governance
Summary: Defines the authoritative platform rules for representing, validating, storing, mapping, and querying dates and timestamps across all services and persistence layers.
Status: Approved
Creator: Kevin D. Wolf
References: SYS-001, SYS-000004, SYS-000010
Creation Date: 2026-03-01T00:00:00.000Z
Last Updated Date: 2026-03-01T00:00:00.000Z
Last Updated By: Kevin D. Wolf

# Body

## 1 - Overview
Summary: Establishes a unified, enforceable policy for date and time handling across application, API, and persistence boundaries.

Purpose  
This DDR defines the canonical rules governing date and time representation in the platform. It separates application-level wire formats from relational storage concerns and enforces UTC semantics and inclusive calendar period logic.

Scope  
Applies to:
- All API contracts
- Domain models
- Scheduled jobs
- Importers
- EF-based relational persistence
- Non-relational storage
- Mapping layers

---

## 2 - Canonical Application-Level Types
Summary: Defines the authoritative wire-level representations used throughout the system.

The system defines three canonical wire types:

### 2.1 UtcTimestamp
- Represents a point-in-time.
- Must be ISO 8601 UTC.
- Must end with `Z`.
- Serialized as string.
- Validated on construction.
- Internally normalized to canonical format.

### 2.2 CalendarDate
- Represents a date without time.
- Must be exactly 10 characters.
- Accepts:
  - `yyyy/MM/dd` (legacy)
  - `yyyy-MM-dd` (canonical)
- Emits canonical `yyyy-MM-dd`.
- Serialized as string.
- Validated on construction.

### 2.3 ClockTime
- Represents time-of-day.
- Format: `HH:mm`.
- No seconds.
- Serialized as string.
- Validated on construction.

---

## 3 - Nullability Semantics
Summary: Defines strict interpretation of nullable wire types.

- `UtcTimestamp?`, `CalendarDate?`, `ClockTime?` represent absence of value.
- Non-null instances MUST represent valid canonical values.
- Empty string is NOT a valid value.
- Invalid formats MUST throw `FormatException` at construction.

This replaces prior string-based ambiguity.

---

## 4 - Relational Storage Rules
Summary: Defines how dates and timestamps are stored and materialized via EF.

### 4.1 Storage Types
Relational storage uses:
- `DateTime` / `DateTime?` for UTC timestamps.
- `Date` / `DateOnly` where supported for calendar-only fields.

### 4.2 UTC Enforcement
All relational `DateTime` values represent UTC instants.

EF must:
- Normalize DateTime values to UTC before save.
- Materialize DateTime values with `Kind = Utc`.
- Avoid `Kind = Unspecified` (especially for SQLite).

Implementation:
- Global EF `ValueConverter` applied in `OnModelCreating`.

---

## 5 - Mapping Layer Rules
Summary: Defines how relational types map to wire types.

Required mapping converters:

- `DateTime` ↔ `UtcTimestamp`
- `DateOnly` ↔ `CalendarDate`

Mapping behavior:
- DateTime → UtcTimestamp requires UTC semantics.
- DateOnly → CalendarDate normalizes format.
- Conversions must be explicit and deterministic.
- Failures must be loud and include offending value.

Migration mode:
- Conditional implicit conversion from `string` → wrapper types MAY be enabled via compile symbol.
- Migration mode must not suppress validation or exception detail.

---

## 6 - Period Semantics
Summary: Defines authoritative inclusive business period behavior.

### 6.1 CalendarDate Period
Calendar period membership is inclusive:

```
value >= Start AND value <= End
```

### 6.2 UtcTimestamp Within Calendar Period
Business semantics are inclusive of entire End day.

Implemented as half-open interval:

```
timestamp >= Start@00:00Z
AND
timestamp < (End + 1 day)@00:00Z
```

This ensures:
- Any time on End date is included.
- No precision ambiguity.
- SQL-safe translation.

EF provides `InPeriod` and `InPeriodUtc` helpers to enforce this logic consistently.

---

## 7 - Application Rules
Summary: Defines system-wide behavioral expectations.

- `DateTime.Now` MUST NOT be used.
- `DateTime.UtcNow` MUST be used for timestamp generation.
- Importers receiving third-party timestamps must normalize explicitly.
- Business logic must use strongly typed wrappers rather than raw strings.

---

## 8 - Enforcement Strategy
Summary: Explains how this policy is enforced in practice.

Enforcement mechanisms:
- Strongly-typed wire wrappers.
- Constructor validation.
- Canonical output normalization.
- EF global UTC converter.
- Explicit mapping converters.
- Period extension helpers.

Violations will surface as:
- `FormatException`
- `InvalidOperationException`
- Mapping failure during startup verification.

Fail-fast behavior is intentional.

---

## 9 - Artifact Location
Summary: Defines authoritative storage path.

Source-of-truth:
`ddrs/SYS-000011 - Date and Time Handling Policy.md`

No secondary JSONL artifact required at this time.

## 10 - Current Relational Date Inventory (Billing Module)

**Summary:** Documents the current date and timestamp surface area within the Billing domain to clarify scope and migration impact.

This section reflects the DTO inventory reviewed during implementation of SYS-000011.

---

### 10.1 Entities Containing DateTime (UTC Instants)

These fields represent point-in-time values and MUST be treated as UTC instants:

- **AccountDto**
  - `LastSyncAt`

- **AccountTransactionDto**
  - `CreationDate`
  - `LastUpdateDate`

- **BillingEventDTO**
  - `StartTimeStamp`
  - `EndTimeStamp`

- **ExpenseDTO**
  - `Date` *(obsolete)*
  - `ApprovedDate`

- **InvoiceDTO**
  - `CreationTimeStamp`
  - `StatusDate`

- **InvoiceLogsDTO**
  - `DateStamp`

- **LicenseUsageDTO**
  - `TimeStamp`

- **TimePeriodDTO**
  - `LockedTimeStamp`

- **TransactionStagingDto**
  - `AuthorizationDate`

All of the above MUST:

- Represent UTC instants.
- Be normalized to `Kind = Utc` when materialized from EF.
- Map to/from `UtcTimestamp` at application boundaries.

---

### 10.2 Entities Containing DateOnly (Calendar Semantics)

These fields represent calendar-only dates (no time component):

- **AccountTransactionDto**
  - `TransactionDate`

- **AgreementDTO**
  - `Start`
  - `End`
  - `LastInvoicedDate`
  - `NextInvoiceDate`

- **ExpenseDTO**
  - `ExpenseDate`

- **AgreementLineItemDTO**
  - `Start` 
  - `End` 
  - `NextBillingDate`
  - `LastBilledDate`

- **InvoiceDTO**
  - `BillingStart`
  - `BillingEnd`
  - `DueDate`
  - `PaidDate`
  - `InvoiceDate`

- **LicenseDTO**
  - `ActiveDate`
  - `RenewalDate`

- **TimeEntryDTO**
  - `Date`

- **PaymentDTO**
  - `PeriodStart`
  - `PeriodEnd`
  - `SubmittedDate`
  - `ExpectedDeliveryDate`

- **PayRateDTO**
  - `Start`
  - `End`

- **TimePeriodDTO**
  - `Start`
  - `End`

These fields:

- Represent calendar semantics only.
- Must map to/from `CalendarDate` at API boundaries.
- Must use inclusive period semantics.

---

### 10.3 Decomposed Calendar Fields

These represent calendar periods via numeric components:

- **BudgetItemDTO**
  - `Year`
  - `Month`

These remain structural period identifiers and are not migrated to `CalendarDate`.

---

### 10.4 No Date Fields

The following DTOs contain no date fields:

- InvoiceLineItemDTO

---

### 10.5 Observations

- The system contains significant temporal surface area.
- The majority of billing-period logic is calendar-based.
- All instant-based timestamps are audit, lifecycle, or event driven.
- Period semantics are business-critical and must remain inclusive.

This inventory confirms that SYS-000011 affects a foundational but well-understood subset of the domain model.

# Approval
Approver: Kevin D. Wolf
Approval Timestamp: 2026-03-01T00:00:00.000Z
