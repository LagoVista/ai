// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 13fd6a7ee515b4668acf4c5cbd9134bb72152644dbfc7f167f6f11aa9041cfd0
// IndexVersion: 2
// --- END CODE INDEX META ---
namespace LagoVista.AI.Models
{
    public class ChatPrompt
    {
        public ChatPrompt(string system, string user, string context)
        {
            System = system;
            User = user;
            Context = context;
        }

        public string System { get; }
        public string User { get; }
        public string Context { get; }
    }
}
