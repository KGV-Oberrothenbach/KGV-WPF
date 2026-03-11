using KGV.Core.Models;

namespace KGV.Wpf.Messages
{
    public sealed class NebenmitgliedSelectedMessage
    {
        public NebenmitgliedContext Context { get; }

        public NebenmitgliedSelectedMessage(NebenmitgliedContext context)
        {
            Context = context;
        }
    }
}
