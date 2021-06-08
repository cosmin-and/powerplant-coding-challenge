using System.Collections.Generic;

namespace Powerplant.Models
{
    public class PowerplantRequest
    {
        public decimal Load { get; set; }
        public Fuels Fuels { get; set; }
        public IEnumerable<Powerplant> Powerplants { get; set; }
    }
}