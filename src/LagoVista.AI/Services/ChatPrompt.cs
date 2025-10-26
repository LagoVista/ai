namespace LagoVista.AI.Services
{
    public class ChatPrompt
    {
        public ChatPrompt(string system, string user, string context)
        {
            System = system;
            User = user;
            Context = context;
        }

        public string System { get;  }
        public string User { get; }
        public string Context { get; }
    }
}
