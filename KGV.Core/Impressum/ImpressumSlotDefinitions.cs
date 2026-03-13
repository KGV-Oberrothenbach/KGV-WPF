namespace KGV.Core.Impressum;

public enum ImpressumBereich
{
    Vorstand = 1,
    Bauausschuss = 2
}

public sealed record ImpressumSlotDefinition(
    string SlotKey,
    string FunktionLabel,
    int SortOrder,
    ImpressumBereich Bereich);

public static class ImpressumSlotDefinitions
{
    // Feste Reihenfolge laut fachlicher Vorgabe.
    public static IReadOnlyList<ImpressumSlotDefinition> All { get; } = new List<ImpressumSlotDefinition>
    {
        new("vorstandsvorsitzender", "Vorstandsvorsitzender", 1, ImpressumBereich.Vorstand),
        new("vertreter", "Vertreter", 2, ImpressumBereich.Vorstand),
        new("kassenwart", "Kassenwart", 3, ImpressumBereich.Vorstand),
        new("schriftfuehrer", "Schriftführer", 4, ImpressumBereich.Vorstand),
        new("bauausschuss_1", "Bauausschuss", 5, ImpressumBereich.Bauausschuss),
        new("bauausschuss_2", "Bauausschuß", 6, ImpressumBereich.Bauausschuss),
        new("bauausschuss_3", "Bauausschuß", 7, ImpressumBereich.Bauausschuss)
    };
}
