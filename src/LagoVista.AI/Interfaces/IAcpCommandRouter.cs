using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IAcpCommandRouter
    {
        AcpExecutionRoute Route(string inputText);
    }
}
