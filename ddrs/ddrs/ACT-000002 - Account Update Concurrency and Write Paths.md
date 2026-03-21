# ACT-000002 — Account Update Concurrency and Write Paths

## Metadata
ID: ACT-000002  
Title: Account Update Concurrency and Write Paths  
Type: Referential  
Summary: Defines the authoritative concurrency and update strategy for Account, including optimistic concurrency via Version, transactional insertion of account transactions, and restricted write paths.  
Status: Draft  
Creator: Kevin D. Wolf  
Creation Date: 2026-03-04T00:00:00-05:00  
Last Updated Date: 2026-03-04T00:00:00-05:00  
Last Updated By: Kevin D. Wolf  

---

## 1. Purpose

This DDR formalizes the update and concurrency strategy for the Account aggregate.

Account is the only entity in this area of the system that:

- Is updated by automated transaction processing.
- May be edited by clients that can hold stale snapshots.
- Contains encrypted balance data.
- Requires strong correctness guarantees across SQL Server, PostgreSQL, SQLite (tests), and future MySQL support.

The goal is to prevent stale client updates from overwriting transaction-driven balance changes while keeping the implementation portable and deterministic.

---

## 2. Concurrency Model

### 2.1 Version Column

Account includes:

public long Version { get; set; }

Configured as:

modelBuilder.Entity<AccountDto>()
    .Property(a => a.Version)
    .HasDefaultValue(0L)
    .IsConcurrencyToken();

Behavior:

- EF includes Version in the WHERE clause of UPDATE.
- Updates succeed only if Version matches the originally loaded value.
- On mismatch, EF throws DbUpdateConcurrencyException.

This mechanism is portable across SQL Server, PostgreSQL, SQLite, and MySQL.

---

## 3. Transactional Integrity of Account Transactions

### 3.1 Atomic Transaction + Balance Update

All account balance changes occur exclusively through AddAccountTransactionAsync.

This method:

- Executes inside WithContextTransactionAsync.
- Opens a database transaction.
- Loads Account as a tracked entity.
- Applies balance mutation in the domain model.
- Maps encrypted balance back onto the tracked entity.
- Increments Version.
- Inserts AccountTransaction row.
- Calls SaveChangesAsync().
- Commits the transaction.

This guarantees:

- Balance update and transaction insert occur in the same database transaction.
- Either both succeed or both fail.
- Concurrency token enforcement applies to the balance update.

If Version has changed, the UPDATE statement affects zero rows and EF throws DbUpdateConcurrencyException.

Optimistic retry with jitter may be applied at the transaction wrapper level.

---

## 4. Update Rules

### 4.1 Balance Mutations

Balance may ONLY be changed via AddAccountTransactionAsync.

Balance is never directly editable by clients.

If balance must be corrected, it must be done by adding a correcting transaction (journal entry). Direct balance setting is prohibited.

Rationale:

- Prevents stale client overwrites.
- Preserves audit trail.
- Maintains event-driven balance correctness.
- Avoids race conditions with automated transaction imports.

---

### 4.2 Client Metadata Updates

Clients may update only the following fields:

- Name
- RoutingNumber
- Institution
- Description
- IsActive

Implementation requirements:

- Load Account as tracked entity.
- Do NOT use AutoMapper for full entity overwrite.
- Manually set only allowed properties.
- Set audit fields.
- Increment Version.
- SaveChanges with concurrency retry.

No other fields may be modified in this path.

---

### 4.3 External Provider Configuration

External provider setup must use a dedicated method responsible for:

- ExternalProvider
- ExternalProviderId
- ExternalAccountId
- AccessTokenSecretId

This method:

- Uses tracked entity.
- Increments Version.
- Uses concurrency retry.

---

### 4.4 Sync State Updates

Sync-related state must use a dedicated method responsible for:

- TransactionCursor
- SyncStatus
- LastSyncAt
- LastError
- EncryptedOnlineBalance

This path:

- Uses tracked entity.
- Increments Version.
- Uses concurrency retry.
- Must not modify client-editable metadata.

---

## 5. Prohibited Pattern

The following pattern is explicitly disallowed for Account updates:

var dto = await _autoMapper.CreateAsync<Account, AccountDto>(account);
ctx.Account.Update(dto);
await ctx.SaveChangesAsync();

Reasons:

- Breaks EF original-value tracking.
- Risks overwriting unrelated fields.
- Can defeat concurrency detection.
- Unsafe for encrypted aggregates.

All Account updates must mutate the tracked entity directly.

---

## 6. Concurrency Retry Strategy

Account updates that modify Version must use optimistic retry with jitter:

- Catch DbUpdateConcurrencyException
- Clear ChangeTracker
- Retry up to N attempts (default 2–3)
- Add small randomized backoff

Retry logic may be centralized inside WithContextTransactionAsync.

---

## 7. Cross-Provider Considerations

The chosen strategy:

- Avoids database-specific locking hints.
- Avoids provider-specific rowversion types.
- Avoids trigger-based version increments.
- Works uniformly across SQL Server, PostgreSQL, SQLite, and MySQL.

---

## 8. Design Guarantees

This DDR ensures:

- No stale client can overwrite a newer balance.
- Automated transactions remain authoritative.
- All balance changes are journaled.
- Metadata updates remain safe under concurrency.
- Implementation remains provider-agnostic.

---

## 9. Future Considerations

Possible enhancements:

- Unique constraint on (AccountId, OriginalHash) for idempotent transactions.
- Monotonic guard on sync cursor updates.
- Admin-only balance reset operation with explicit audit trail.

---

End of ACT-000002
