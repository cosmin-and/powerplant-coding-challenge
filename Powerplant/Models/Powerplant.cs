using Newtonsoft.Json;

namespace Powerplant.Models
{
    public class Powerplant
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public decimal Efficiency { get; set; }
        public int PMin { get; set; }
        public int PMax { get; set; }

        [JsonIgnore]
        public decimal MinGenElectricalEnergy { get; set; }
        [JsonIgnore]
        public decimal MaxGenElectricalEnergy { get; set; }
        [JsonIgnore]
        public decimal FuelCost { get; set; }
        [JsonIgnore]
        public decimal ElectricityUnitCost { get; internal set; }
        [JsonIgnore]
        public int Index { get; set; }
        [JsonIgnore]
        public bool IsLastInGroup { get; set; }
    }
}