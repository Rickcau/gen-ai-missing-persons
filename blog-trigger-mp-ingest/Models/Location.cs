using System.Text.Json.Serialization;  

namespace blog_trigger_mp_ingest.Models;
  
public class Location  
{  
    [JsonPropertyName("latitude")]  
    public double? Latitude { get; set; }  
  
    [JsonPropertyName("longitude")]  
    public double? Longitude { get; set; }  
}  
