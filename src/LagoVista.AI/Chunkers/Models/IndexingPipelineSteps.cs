namespace LagoVista.AI.Indexing.Models
{
    public enum IndexingPipelineSteps
    {
        Unknown = 0,
        PersistSourceFile = 1,
        ExtractSymbols = 2,
        CategorizeContent = 3,
        SegmentContent = 4,
        BuildDescription = 5,
        UploadContent = 6,
        Embed = 7,
        StoreUpsertPoint = 8
    }
}
