using System;

namespace LagoVista.AI.Rag.Chunkers.Models
{
    /// <summary>
    /// Logical subkind classification for server-side C# artifacts.
    /// Extracted from the original SubKindDetector for reuse by SourceKindAnalyzer.
    /// </summary>
    public enum CodeSubKind
    {
        DomainDescription,
        Model,
        SummaryListModel,
        Manager,
        Repository,
        Controller,
        Service,
        Test,
        Interface,
        Startup,
        Program,
        Client,
        Configuration,
        Handler,
        ResourceFile,
        CodeAttribute,
        Exception,
        ExtensionMethods,
        Request,
        Result,
        Response,
        ProxyServices,
        Message,
        Ddr,
        MarkDown,
        Other
    }
}
