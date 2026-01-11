namespace LagoVista.AI.Indexing.Models
{
    /// <summary>
    /// Canonical facet value stored with a document or chunk in the index.
    /// Used for metadata registry and filtering (IDX-033+).
    /// </summary>
    public sealed class FacetValue
    {
        /// <summary>
        /// Kind of the facet type (e.g. "Kind", "SubKind", "Repo").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Value for the facet (e.g. "SourceCode", "Model", "Infrastructure").
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Optional parent type, used for multi-level classifications
        /// (e.g. Kind = SourceCode, SubKind = Model).
        /// </summary>
        public string ParentType { get; set; }

        /// <summary>
        /// Optional parent value, if the facet belongs to a hierarchy.
        /// </summary>
        public string ParentValue { get; set; }
    }
}
