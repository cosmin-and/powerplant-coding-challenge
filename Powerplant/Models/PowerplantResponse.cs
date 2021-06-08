using Newtonsoft.Json;
using System.Collections.Generic;

namespace Powerplant.Models
{
    public class PowerplantResponse
    {
        public bool HasErrors { get; set; }
        public IEnumerable<PowerplantResponseInfo> PowerplantResponseList { get; set; }
        public string ErrorDescription { get; set; }
    }

    public class PowerplantResponseInfo
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "p")]
        public decimal PowerToDeliver { get; set; }
    }
}