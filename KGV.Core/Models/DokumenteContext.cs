namespace KGV.Core.Models
{
    public sealed class DokumenteContext
    {
        public MemberDTO Member { get; }
        public ParzellenBelegungDTO? Belegung { get; }

        public DokumenteContext(MemberDTO member, ParzellenBelegungDTO? belegung)
        {
            Member = member;
            Belegung = belegung;
        }
    }
}
