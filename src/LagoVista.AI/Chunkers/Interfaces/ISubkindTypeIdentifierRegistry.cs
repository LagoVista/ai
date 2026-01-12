using LagoVista.AI.Indexing.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Chunkers.Interfaces
{
    public interface ISubkindTypeIdentifierRegistry : IProcessorRegistry<DocumentType, ISubkindTypeIdentifier>
    {
    }
}
