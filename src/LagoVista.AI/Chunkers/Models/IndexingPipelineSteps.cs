namespace LagoVista.AI.Indexing.Models
{
    public enum IndexingPipelineSteps
    {
        Unknown = 0,
        PersistSourceFile = 10,
        ExtractSymbols = 20,
        CategorizeContent = 30,
        SegmentContent = 40,
        BuildDescription = 506,
        UploadContent = 60,
        Embed = 70,
        StoreUpsertPoint = 80
    }
}
