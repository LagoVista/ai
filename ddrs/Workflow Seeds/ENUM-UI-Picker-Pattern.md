# Enum → UI Picker Pattern (Canonical)

Use this pattern whenever you add a user-selectable enum that appears in forms and must support localization, stable string values, and optional CSS hooks.

## Generate these artifacts

### 1) Enum definition (with EnumLabel)

Each enum value MUST have an EnumLabel that points to:
- a stable string constant on the owning entity
- a localized resource entry

Example:

public enum ExampleEnum
{
    [EnumLabel(ExampleEntity.ExampleEnum_ValueA, ExampleResources.Names.ExampleEnum_ValueA, typeof(ExampleResources))]
    ValueA,

    [EnumLabel(ExampleEntity.ExampleEnum_ValueB, ExampleResources.Names.ExampleEnum_ValueB, typeof(ExampleResources))]
    ValueB
}

### 2) String constants on the owning entity

These are the wire / CSS / persistence values. They MUST be lowercase and stable.

public const string ExampleEnum_ValueA = "value-a";
public const string ExampleEnum_ValueB = "value-b";

### 3) Resource name constants

Add matching resource keys to *.Resources.Names.

public const string ExampleEnum_ValueA = "ExampleEnum_ValueA";
public const string ExampleEnum_ValueB = "ExampleEnum_ValueB";

### 4) XML resource entries (.resx)

These control what the user sees. Text may change later without breaking code.

<data name="ExampleEnum_ValueA" xml:space="preserve">
  <value>Value A</value>
</data>

<data name="ExampleEnum_ValueB" xml:space="preserve">
  <value>Value B</value>
</data>

### 5) (Optional) Form field declaration

If the enum is user-configurable, expose it via a picker.

[FormField(
    LabelResource: ExampleResources.Names.ExampleEnum,
    FieldType: FieldTypes.Picker,
    ResourceType: typeof(ExampleResources),
    IsRequired: true,
    IsUserEditable: true)]
public ExampleEnum ExampleSetting { get; set; }

## Naming rules

- Enum name: PascalCase
- EnumLabel key: EntityName.EnumName_Value
- Entity constants: lowercase (kebab-case recommended)
- Resource keys: match enum label names exactly
- Display text: human-friendly and may be edited later

## Reusable LLM prompt

Create a user-selectable enum following our LagoVista pattern, including: (1) enum with EnumLabel attributes, (2) owning entity string constants for stable values, (3) resource name constants, and (4) .resx XML nodes for all values.
