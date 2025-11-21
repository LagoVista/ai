//using System.Collections.Generic;
//using LagoVista.AI.Rag.Chunkers.Models;

//namespace LagoVista.AI.Rag.Chunkers.Interfaces
//{
//    /// <summary>
//    /// Builds an in-memory catalog of domains and models from existing
//    /// chunker outputs (DomainSummaryInfo + ModelMetadataDescription).
//    ///
//    /// Orchestration is responsible for walking source files and calling
//    /// IChunkerServices. This interface is responsible only for organizing
//    /// those results into a reusable catalog.
//    /// </summary>
//    public interface IDomainModelCatalogBuilder
//    {
//        /// <summary>
//        /// Build a catalog of domains and models that can be used later
//        /// when normalizing text for embedding (titles, taglines, etc.).
//        /// </summary>
//        /// <param name="domainSummaries">Domain summaries extracted from domain descriptor files.</param>
//        /// <param name="modelMetadata">Model metadata descriptions extracted from model files.</param>
//        /// <returns>A catalog of domains and models.</returns>
//        DomainModelCatalog BuildCatalog(
//            IReadOnlyList<DomainSummaryInfo> domainSummaries,
//            IReadOnlyList<ModelMetadataDescription> modelMetadata);
//    }
//}
