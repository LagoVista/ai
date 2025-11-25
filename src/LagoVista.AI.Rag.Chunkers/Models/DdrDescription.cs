using System;
using System.Collections.Generic;
using System.Dynamic;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Represents a parsed view of a single DDR markdown document.
    /// </summary>
    public partial class DdrDescription : SummaryFacts
    {
        public override string Subtype => "Ddr";


        public string DdrType { get; set; }
        public int DdrNumber { get; set; }

        /// <summary>
        /// Human friendly title of the DDR, usually taken from the first H1 heading.
        /// </summary>
        public string DdrTitle { get; set; }

        public string Status { get; set; }

        public string IndexDate { get => DateTime.Today.ToShortDateString(); }

        /// <summary>
        /// 
        /// </summary>
        public string HeaderBlock { get; set; }

        /// <summary>
        /// Raw sections parsed from the DDR markdown, including preamble and all "##" sections.
        /// Each section is further split into overlapping parts suitable for embedding.
        /// </summary>
        public IList<DdrSectionDescription> Sections { get; } = new List<DdrSectionDescription>();
    }

    /// <summary>
    /// Raw section from a DDR markdown document, plus its embedding-ready parts.
    /// </summary>
    public class DdrSectionDescription
    {
        /// <summary>
        /// Slug/key for this section, derived from the heading when available.
        /// </summary>
        public string SectionKey { get; set; }

        /// <summary>
        /// Human heading text for the section (for example, "Chunking Strategy").
        /// </summary>
        public string Heading { get; set; }

        /// <summary>
        /// The full markdown content for this section.
        /// </summary>
        public string RawMarkdown { get; set; }
    }
}
