// File: Core/Models/NebenmitgliedContext.cs
namespace KGV.Core.Models
{
    public sealed class NebenmitgliedContext
    {
        public MemberDTO Hauptmitglied { get; }
        public MemberDTO Nebenmitglied { get; }

        public NebenmitgliedContext(MemberDTO hauptmitglied, MemberDTO nebenmitglied)
        {
            Hauptmitglied = hauptmitglied;
            Nebenmitglied = nebenmitglied;
        }
    }
}
