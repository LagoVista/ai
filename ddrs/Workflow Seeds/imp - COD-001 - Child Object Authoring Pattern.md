# Metadata
ID: COD-000001
Title: Child Object Authoring Pattern
Type: Referential
Status: Approved
Creator: Kevin D. Wolf
Creation Date: 2026-01-01T20:03:30.805Z
Last Updated Date: 2026-01-01T20:03:30.805Z
Last Updated By: Kevin D. Wolf

# Approval
Approver: Kevin D. Wolf
Approval Timestamp: 2026-01-01T20:03:30.805Z


## Summary

Use this pattern whenever defining reusable embedded configuration models. Doing so guarantees consistency across UI rendering, agent tooling, and future extensibility.


## Body

## Purpose

This document defines the standard pattern for creating **child / embedded objects** within LagoVista domain models. Child objects are not persisted or managed independently and exist only as part of their parent entity.

This pattern ensures:
- Consistent UI metadata behavior
- Predictable form rendering
- Clean embedding inside parent classes
- Minimal boilerplate

---

## Definition: Child Object

A child object:
- Does NOT have its own repository
- Does NOT have its own namespace block
- Does NOT include using statements
- Is declared inline with its parent entity
- Is edited only through the parent UI

Examples include:
- Inline styles
- Layout blocks
- Section definitions
- Embedded configuration records

---

## Required Characteristics

### 1. EntityDescription Attribute

Child objects MUST include an EntityDescription attribute with:
- Domain
- Title resource
- Help resource
- Description resource
- EntityTypes.SimpleModel
- ResourceType
- Icon (optional)
- FactoryUrl (required for tooling and agent creation)

All EntityDescription arguments MUST appear on a **single line**.

---

### 2. FormField Attributes

Each editable property:
- MUST be decorated with a FormField attribute
- MUST specify LabelResource and ResourceType
- SHOULD specify FieldType when not implicit

Attributes MUST appear on a **single line**.

---

### 3. IFormDescriptor Implementation

Child objects MUST implement IFormDescriptor and define GetFormFields():

- Fields MUST be returned in display order
- Only include properties intended for editing

---

### 4. Class Placement Rules

Child objects:
- MUST NOT include a namespace declaration
- MUST NOT include using statements
- MUST be declared in the same file as the parent entity
- MAY be declared above or below the parent class

---

## Canonical Example

```csharp
[EntityDescription(
    Domains.BillingDomainName,
    BillingResources.Names.InlineStyle_Title,
    BillingResources.Names.InlineStyle_Help,
    BillingResources.Names.InlineStyle_Description,
    EntityDescriptionAttribute.EntityTypes.SimpleModel,
    typeof(BillingResources),
    Icon: "icon-ae-editing",
    FactoryUrl: "/api/landingpage/layout/inline-style/factory"
)]
public class InlineStyle : IFormDescriptor
{
    [FormField(LabelResource: BillingResources.Names.InlineStyle_StyleName, ResourceType: typeof(BillingResources), IsRequired: true)]
    public string StyleName { get; set; }

    [FormField(LabelResource: BillingResources.Names.InlineStyle_StyleValue, ResourceType: typeof(BillingResources), FieldType: FieldTypes.MultiLineText)]
    public string StyleValue { get; set; }

    public List<string> GetFormFields()
    {
        return new List<string>
        {
            nameof(StyleName),
            nameof(StyleValue)
        };
    }
}
```

---

## Resource Requirements

For each child object, resource entries MUST exist for:
- Title
- Help
- Description
- Each FormField label

Resources MUST follow existing naming conventions and live in the parent domain resource file.

---

## Agent Tool Compatibility

This pattern is designed to be:
- Agent-creatable via factory endpoints
- Safe for partial mutation via tools
- Compatible with layout and content authoring modes

Agents SHOULD treat child objects as atomic units and avoid modifying parent entities unless explicitly requested.

---
