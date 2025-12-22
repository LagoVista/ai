# TST-002 — Canonical Test Patterns & Examples

**ID:** TST-002  
**Title:** Canonical Test Patterns & Examples  
**Status:** Approved  
**DDR Type:** Referential

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-22

---

## 1. Purpose

TST-002 provides concise, non-normative examples of well-structured tests that comply with TST-001. It exists to offer practical reference patterns for humans and agents without introducing executable rules.

This DDR is informational only.

---

## 2. Scope

This DDR applies to:
- NUnit 4.x test examples
- Moq-based unit and tool tests
- Aptix agent- and tool-oriented test scenarios

It does not define requirements or constraints.

---

## 3. Canonical Test Shapes

### 3.1 Successful Persistence (Confirmed Path)

```csharp
[Test]
public async Task ExecuteAsync_WhenConfirmed_PersistsDdr()
{
    var mgr = new Mock<IDdrManager>(MockBehavior.Strict);
    var ctx = BuildContext();

    mgr.Setup(m => m.GetDdrByTlaIdentiferAsync("AGN-050", ctx.Org, ctx.User, false))
       .ReturnsAsync((DetailedDesignReview)null);

    mgr.Setup(m => m.AddDdrAsync(It.IsAny<DetailedDesignReview>(), ctx.Org, ctx.User))
       .ReturnsAsync(InvokeResult.Success);

    var tool = CreateTool(mgr);

    var args = DdrTestDsl.ConfirmedInstruction(
        "AGN-050",
        "Confirmed DDR",
        "MUST do X.",
        "MUST NOT do Y.");

    var result = await tool.ExecuteAsync(ToArgsJson(args), ctx, CancellationToken.None);

    Assert.That(result.Successful, Is.True, result.ErrorMessage);

    DdrExpect.PersistedOnce(mgr, "AGN-050", "Confirmed DDR", ctx);
}
```

---

### 3.2 Duplicate Block (No Persistence)

```csharp
[Test]
public async Task ExecuteAsync_WhenDdrExists_ReturnsError_AndDoesNotPersist()
{
    var mgr = new Mock<IDdrManager>(MockBehavior.Strict);
    var ctx = BuildContext();

    mgr.Setup(m => m.GetDdrByTlaIdentiferAsync("AGN-777", ctx.Org, ctx.User, false))
       .ReturnsAsync(new DetailedDesignReview { DdrIdentifier = "AGN-777" });

    var tool = CreateTool(mgr);

    var args = DdrTestDsl.ConfirmedGeneration("AGN-777", "Duplicate DDR");

    var result = await tool.ExecuteAsync(ToArgsJson(args), ctx, CancellationToken.None);

    Assert.That(result.Successful, Is.False, result.ErrorMessage);

    DdrExpect.BlockedAsDuplicate(mgr, "AGN-777", ctx);
}
```

---

### 3.3 Dry-Run Preview

```csharp
[Test]
public async Task ExecuteAsync_WhenDryRun_ReturnsPreview_WithoutPersistence()
{
    var mgr = new Mock<IDdrManager>(MockBehavior.Loose);
    var ctx = BuildContext();

    mgr.Setup(m => m.GetDdrByTlaIdentiferAsync("AGN-023", ctx.Org, ctx.User, false))
       .ReturnsAsync((DetailedDesignReview)null);

    var tool = CreateTool(mgr);

    var args = DdrTestDsl.DryRunGeneration("AGN-023", "Preview DDR");

    var result = await tool.ExecuteAsync(ToArgsJson(args), ctx, CancellationToken.None);

    Assert.That(result.Successful, Is.True, result.ErrorMessage);

    DdrExpect.DryRunOnly(mgr, "AGN-023", ctx);
}
```

---

## 4. Common Anti-Patterns (For Awareness)

The following patterns are intentionally avoided in examples:

- Verifying every property on complex objects
- Using StringAssert instead of Assert.That
- Asserting timestamps or DateTime.UtcNow
- Verifying call order when behavior does not require it
- Using VerifyNoOtherCalls() without helper encapsulation

---

## 5. Relationship to TST-001

TST-002 complements TST-001 by providing concrete examples only. All normative rules governing test generation are defined exclusively in TST-001.

---

# End of TST-002
