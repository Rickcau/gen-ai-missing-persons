
using System.Text.Json.Serialization;

namespace api_process_missing_persons_files.Models;
public class MissingPerson
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("race")]
    public string? Race { get; set; }

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("sex")]
    public string? Sex { get; set; }

    [JsonPropertyName("height")]
    public string? Height { get; set; }

    [JsonPropertyName("weight")]
    public string? Weight { get; set; }

    [JsonPropertyName("eye_color")]
    public string? EyeColor { get; set; }

    [JsonPropertyName("hair")]
    public string? Hair { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("tattoos")]
    public string? Tattoos { get; set; }

    [JsonPropertyName("last_seen")]
    public string? LastSeen { get; set; }

    [JsonPropertyName("date_reported")]
    public string? DateReported { get; set; }

    [JsonPropertyName("missing_from")]
    public string? MissingFrom { get; set; }

    [JsonPropertyName("conditions_of_disappearance")]
    public string? ConditionsOfDisappearance { get; set; }

    [JsonPropertyName("officer_info")]
    public string? OfficerInfo { get; set; }

    [JsonPropertyName("phone_number1")]
    public string? PhoneNumber1 { get; set; }

    [JsonPropertyName("phone_number2")]
    public string? PhoneNumber2 { get; set; }

    [JsonPropertyName("latitude")]  
    public double? Latitude { get; set; }  
  
    [JsonPropertyName("longitude")]  
    public double? Longitude { get; set; }  
}

