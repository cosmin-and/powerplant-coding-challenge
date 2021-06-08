using Newtonsoft.Json;

namespace Powerplant.Models
{
    public class Fuels
    {
        [JsonProperty(PropertyName = "gas(euro/MWh)")]
        public decimal GasPrice { get; set; }
        [JsonProperty(PropertyName = "kerosine(euro/MWh)")]
        public decimal KerosenePrice { get; set; }
        [JsonProperty(PropertyName = "co2(euro/ton)")]
        public decimal CO2Price { get; set; }
        [JsonProperty(PropertyName = "wind(%)")]
        public decimal WindPercentage { get; set; }
    }
}