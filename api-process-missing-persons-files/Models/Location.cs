using System.Text.Json.Serialization;

namespace api_process_missing_persons_files.Models;

public class Location  
{  
    [JsonPropertyName("latitude")]  
    public double? Latitude { get; set; }  
  
    [JsonPropertyName("longitude")]  
    public double? Longitude { get; set; }  
}  
