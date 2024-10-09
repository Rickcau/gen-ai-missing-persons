using System.Text.Json.Serialization;  

namespace api_process_missing_persons_files.Models;


public class EnrichedAddress
{
    public double MatchConfidence { get; set; }
    public string? StreetNumber { get; set; }
    public string? StreetName { get; set; }
    public string? Municipality { get; set; }
    public string? Neighbourhood { get; set; }
    public string? CountrySecondarySubdivision { get; set; }
    public string? CountrySubdivisionName { get; set; }
    public string? PostalCode { get; set; }
    public string? ExtendedPostalCode { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}