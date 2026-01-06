using LagoVista.AI.ACP;
using LagoVista.Core.Validation;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IAcpCommandFactory
    {
        InvokeResult<IAcpCommand> GetCommand(string commandId);
    }
}
