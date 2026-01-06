using LagoVista.AI.ACP;
using System;
using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Interfaces
{
    public interface IAcpCommandRegistry
    {
        void RegisterCommand<T>() where T : IAcpCommand;

        bool HasCommand(string commandId);
        Type GetCommandType(string commandId);

        IReadOnlyDictionary<string, Type> GetRegisteredCommands();
        IEnumerable<AcpCommandSummary> GetAllCommands();
        AcpCommandDescriptor GetDescriptor(string commandId);
    }

}
