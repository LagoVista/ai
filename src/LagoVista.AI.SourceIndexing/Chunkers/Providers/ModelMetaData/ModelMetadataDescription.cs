// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: TBD
// IndexVersion: 1
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// IDX-0038: Model Metadata & UI Reason
    /// Detailed UI, validation, picker, layout, labeling, and metadata for model entities.
    /// </summary>
    public sealed partial class ModelMetadataDescription : SummaryFacts
    {
        // ---------- Identity / Domain ----------
        public string ModelName { get; set; }
        public string Domain { get; set; }

        /// <summary>
        /// Underlying .resx resource library type name, e.g. "AIResources".
        /// </summary>
        public string ResourceLibrary { get; set; }

        // ---------- UX Strings ----------
        public string Title { get; set; }
        public string Help { get; set; }
        public string Description { get; set; }

        // ---------- Capabilities ----------
        public bool Cloneable { get; set; }
        public bool CanImport { get; set; }
        public bool CanExport { get; set; }

        // ---------- UI / API Affordances ----------
        public string PreviewUIUrl { get; set; }
        public string ListUIUrl { get; set; }
        public string EditUIUrl { get; set; }
        public string CreateUIUrl { get; set; }
        public string HelpUrl { get; set; }

        public string InsertUrl { get; set; }
        public string SaveUrl { get; set; }
        public string UpdateUrl { get; set; }
        public string FactoryUrl { get; set; }
        public string GetUrl { get; set; }
        public string GetListUrl { get; set; }
        public string DeleteUrl { get; set; }

        // ---------- Fields ----------
        public List<ModelFieldMetadataDescription> Fields { get; set; } =
            new List<ModelFieldMetadataDescription>();

        // ---------- Layouts (IDX-0038 Layouts block) ----------
        /// <summary>
        /// Form and view layouts derived from IFormDescriptor* interfaces and additional actions.
        /// </summary>
        public ModelFormLayouts Layouts { get; set; } = new ModelFormLayouts();

        public override string Subtype => "Model";
        public override string SubtypeFlavor => "ModelMetaData"; 
   }

    /// <summary>
    /// Field-level metadata for IDX-0038.
    /// (We’re starting with identity only; richer UI/validation metadata comes later.)
    /// </summary>
    public sealed class ModelFieldMetadataDescription
    {
        /// <summary>
        /// C# property name on the model.
        /// </summary>
        public string PropertyName { get; set; }
        public string Label { get; set; }
        public string Help { get; set; }
        public string Watermark { get; set; }
        public string FieldType { get; set; }
        public string DataType { get; set; }
        public bool IsRequired { get; set; }
        // Future: resource keys, field kind, validation, picker metadata, etc.
        // e.g. LabelKey, HelpKey, FieldType, IsRequired, EnumType, PickerFor, etc.
    }

    /// <summary>
    /// High-level layout description for forms and views.
    /// Mirrors the IDX-0038 "Layouts" shape:
    ///   - Form (Col1/Col2/Bottom/Tab fields)
    ///   - Advanced (Col1/Col2)
    ///   - Inline, Mobile, Simple, QuickCreate
    ///   - Additional actions
    /// </summary>
    public sealed class ModelFormLayouts
    {
        /// <summary>
        /// Main form layout: standard + tabbed sections.
        /// </summary>
        public ModelFormLayoutColumns Form { get; set; } = new ModelFormLayoutColumns();

        /// <summary>
        /// Advanced form layout (often separate section).
        /// </summary>
        public ModelFormLayoutColumns Advanced { get; set; } = new ModelFormLayoutColumns();

        /// <summary>
        /// Fields rendered inline (e.g., inline editors).
        /// </summary>
        public List<string> InlineFields { get; set; } = new List<string>();

        /// <summary>
        /// Fields prioritized for mobile layouts.
        /// </summary>
        public List<string> MobileFields { get; set; } = new List<string>();

        /// <summary>
        /// Minimal/simple edit layout.
        /// </summary>
        public List<string> SimpleFields { get; set; } = new List<string>();

        /// <summary>
        /// Quick-create layout for fast data entry.
        /// </summary>
        public List<string> QuickCreateFields { get; set; } = new List<string>();

        /// <summary>
        /// Additional actions attached to the form (buttons/commands).
        /// </summary>
        public List<ModelFormAdditionalActionDescription> AdditionalActions { get; set; } =
            new List<ModelFormAdditionalActionDescription>();
    }

    /// <summary>
    /// Columnar layout for a form section.
    /// Matches the IDX-0038 description:
    ///   Form:
    ///     - Col1Fields
    ///     - Col2Fields
    ///     - BottomFields
    ///     - TabFields (grouped by tab)
    ///   Advanced:
    ///     - Col1Fields
    ///     - Col2Fields
    /// </summary>
    public sealed class ModelFormLayoutColumns
    {
        public List<string> Col1Fields { get; set; } = new List<string>();
        public List<string> Col2Fields { get; set; } = new List<string>();

        /// <summary>
        /// Fields rendered in a bottom row/section (used primarily on the main form).
        /// </summary>
        public List<string> BottomFields { get; set; } = new List<string>();

        /// <summary>
        /// Tab-based layout: key is tab identifier (or title),
        /// value is list of property names on that tab.
        /// </summary>
        public Dictionary<string, List<string>> TabFields { get; set; } =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reason of an additional action on the form.
    /// Derived from IFormAdditionalActions.GetAdditionalActions().
    /// </summary>
    public sealed class ModelFormAdditionalActionDescription
    {
        public string Title { get; set; }
        public string Icon { get; set; }
        public string Help { get; set; }
        public string Key { get; set; }

        /// <summary>
        /// True if this action is shown in create mode.
        /// </summary>
        public bool ForCreate { get; set; }

        /// <summary>
        /// True if this action is shown in edit mode.
        /// </summary>
        public bool ForEdit { get; set; }
    }
}
