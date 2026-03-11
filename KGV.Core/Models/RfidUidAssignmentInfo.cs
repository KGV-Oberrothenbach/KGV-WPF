namespace KGV.Core.Models;

public sealed record RfidUidAssignmentInfo(
    int ParzelleId,
    string GartenNr,
    string Anlage,
    short ZaehlerTyp,
    string FeldName);
