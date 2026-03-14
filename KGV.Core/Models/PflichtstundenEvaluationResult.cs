namespace KGV.Core.Models;

public enum PflichtstundenBefreiungsQuelle
{
    None = 0,
    Wartungsvertrag = 1,
    LegacyRole = 2
}

public sealed class PflichtstundenEvaluationResult
{
    public int HauptmitgliedId { get; init; }
    public int SaisonId { get; init; }
    public int Jahr { get; init; }

    public decimal Sollstunden { get; init; }
    public decimal Geleistet { get; init; }
    public decimal OffeneStunden { get; init; }
    public decimal Fehlbetrag { get; init; }

    public decimal EuroProFehlstunde { get; init; }

    public bool IstBefreit { get; init; }
    public PflichtstundenBefreiungsQuelle BefreiungsQuelle { get; init; }
    public string Grund { get; init; } = string.Empty;

    public long? BefreienderWartungsvertragId { get; init; }
    public string? BefreienderWartungsvertragTitel { get; init; }
    public string? BefreienderWartungsvertragBereich { get; init; }

    public string? LegacyRole { get; init; }
}
