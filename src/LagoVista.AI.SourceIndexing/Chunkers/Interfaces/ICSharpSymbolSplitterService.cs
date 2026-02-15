using LagoVista.AI.Chunkers.Services;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Chunkers.Interfaces
{
    public interface ISymbolSplitterService
    {
        InvokeResult<IReadOnlyList<SplitSymbolResult>> Split(string sourceText);
    }

    public interface ICSharpSymbolSplitterService : ISymbolSplitterService
    {

    }
}
