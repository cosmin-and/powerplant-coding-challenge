using Powerplant.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Powerplant.Controllers
{
    public class PowerplantController : ApiController
    {
        [HttpPost]
        [Route("productionplan")]
        public HttpResponseMessage GenerateProductionPlan([FromBody] PowerplantRequest powerplantRequest)
        {
            if (powerplantRequest != null)
            {
                var estimatedProductionPlan = GenerateProductionPlanResponse(powerplantRequest);

                if(estimatedProductionPlan.HasErrors)
                {
                    var errors = new HttpError(estimatedProductionPlan.ErrorDescription);
                    return Request.CreateResponse(HttpStatusCode.BadRequest, errors);
                }

                return Request.CreateResponse(HttpStatusCode.OK, estimatedProductionPlan.PowerplantResponseList);
            }

            return Request.CreateResponse(HttpStatusCode.BadRequest, new HttpError("Invalid input"));
        }

        /// <summary>
        ///     Parses the payload and generates a response based on the input data
        /// </summary>
        /// <param name="powerplantRequest">the object corresponding to the payload</param>
        /// <returns>
        ///     An object specifying if there were any errors and a list describing how much power each powerplant 
        ///     should deliver
        /// </returns>
        private PowerplantResponse GenerateProductionPlanResponse(PowerplantRequest powerplantRequest)
        {
            try
            {
                // Update the request object with calculated values
                powerplantRequest = CalculateCostsAndGeneratedEnergy(powerplantRequest);

                // Establish merits based on the costs needed to generate one unit of electricity and the maximum
                // energy that can be generated. Promote the powerplants having the costs as low as possible and that
                // can generate more energy
                var powerplants = (IEnumerable<Models.Powerplant>)powerplantRequest.Powerplants
                                            .OrderBy(p => p.ElectricityUnitCost)
                                            .ThenByDescending(p => p.MaxGenElectricalEnergy);

                // If the powerplant is the last one in the group and its power plus the one generated so far would
                // be higher than the load, I need to adjust the generated power for the current powerplant and,
                // eventually, for the previous one. So I group the powerplants to know which one is the last in the
                // group of similar powerplants and see if I should adjust the generated power as a valid difference to 
                // the load.
                var powerplantsGroups = from powerplant in powerplants
                                        group powerplant by powerplant.Type into powerplantsGroup
                                        select new { powerplantsGroup.Key, List = powerplantsGroup.ToList() };

                int index;
                foreach (var powerplant in powerplantsGroups)
                {
                    index = 0;
                    foreach (var p in powerplant.List)
                    {
                        p.Index = index++;
                        p.IsLastInGroup = index == powerplant.List.Count;
                    }
                }

                return new PowerplantResponse
                {
                    HasErrors = false,
                    ErrorDescription = "",
                    PowerplantResponseList = GenerateSuggestions(powerplants, powerplantRequest.Load) 
                };
            }
            catch (Exception ex)
            {
                return new PowerplantResponse
                {
                    HasErrors = true,
                    ErrorDescription = ex.Message,
                    PowerplantResponseList = null
                };
            }
        }

        /// <summary>
        /// Calculate for each power plant the electricity costs and the min/max electrical energy that can be generated
        /// based on the efficiency and the corresponding cost for each type of fuel
        /// </summary>
        /// <param name="powerplantRequest">the request object</param>
        /// <returns>the request object updated with the calculated values</returns>
        private PowerplantRequest CalculateCostsAndGeneratedEnergy(PowerplantRequest powerplantRequest)
        {
            try
            {
                var useFactor = Convert.ToBoolean(ConfigurationManager.AppSettings["UseFactor"]);
                var factor = Convert.ToDecimal(ConfigurationManager.AppSettings["Factor"]);

                if(useFactor)
                {                
                    if (factor <= 0 || factor > 1)
                        throw new Exception($"The factor needs to be between 0 and 1. Current value: '{factor}'.");
                }

                foreach (var powerplantInfo in powerplantRequest.Powerplants)
                {
                    switch(powerplantInfo.Type)
                    {
                        case "windturbine":
                            powerplantInfo.MinGenElectricalEnergy = powerplantInfo.PMin * powerplantRequest.Fuels.WindPercentage / 100;
                            powerplantInfo.MaxGenElectricalEnergy = powerplantInfo.PMax *
                                                                powerplantRequest.Fuels.WindPercentage / 100;
                            powerplantInfo.FuelCost = 0;
                            powerplantInfo.ElectricityUnitCost = 0;
                        break;

                        case "gasfired":
                            powerplantInfo.MinGenElectricalEnergy = powerplantInfo.PMin * powerplantInfo.Efficiency;
                            powerplantInfo.MaxGenElectricalEnergy = powerplantInfo.PMax * powerplantInfo.Efficiency;
                            powerplantInfo.FuelCost = powerplantRequest.Fuels.GasPrice;
                            powerplantInfo.ElectricityUnitCost = powerplantRequest.Fuels.GasPrice / 
                                                                            powerplantInfo.Efficiency;
                        break;

                        case "turbojet":
                            powerplantInfo.MinGenElectricalEnergy = powerplantInfo.PMin * powerplantInfo.Efficiency;
                            powerplantInfo.MaxGenElectricalEnergy = powerplantInfo.PMax * powerplantInfo.Efficiency;
                            powerplantInfo.ElectricityUnitCost = powerplantRequest.Fuels.KerosenePrice / 
                                                                            powerplantInfo.Efficiency;
                            powerplantInfo.FuelCost = powerplantRequest.Fuels.KerosenePrice;
                            break;

                        default:
                            throw new Exception($"Invalid powerplant type: '{powerplantInfo.Type}'.");
                    }
                    
                    // we can choose or not to generate electricity by using the maximum power (more explanations in
                    // the Readme file)
                    if(useFactor)
                    {
                        if (powerplantInfo.MaxGenElectricalEnergy * factor <= powerplantInfo.MinGenElectricalEnergy)
                            powerplantInfo.MaxGenElectricalEnergy = powerplantInfo.MinGenElectricalEnergy;
                        else
                            powerplantInfo.MaxGenElectricalEnergy *= factor;                            
                    }

                    // keep only the first decimal
                    powerplantInfo.MinGenElectricalEnergy = Math.Round(powerplantInfo.MinGenElectricalEnergy, 1);
                    powerplantInfo.MaxGenElectricalEnergy = Math.Round(powerplantInfo.MaxGenElectricalEnergy, 1);
                }
            }
            catch(Exception) {
                throw;
            }            

            return powerplantRequest;
        }

        /// <summary>
        /// Implements the algorithm that calculates how much power each powerplant should deliver
        /// </summary>
        /// <param name="powerplants">the list of power plants to analyze</param>
        /// <param name="load">the load that needs to be compared against</param>
        /// <returns>an IEnumerable specifying how much power each powerplant should deliver</returns>
        private IEnumerable<PowerplantResponseInfo> GenerateSuggestions(IEnumerable<Models.Powerplant> powerplants, decimal load)
        {
            var response = new List<PowerplantResponseInfo>();
            var loadReached = false;
            decimal prevMinVal = 0, prevAddedVal = 0, totalGeneratedPower = 0;
            int lastIndex = 0;

            foreach (var powerplant in powerplants)
            {
                // if the load has been already reached, turn off the remaining powerplants
                if (loadReached)
                {
                    response.Add(new PowerplantResponseInfo { Name = powerplant.Name, PowerToDeliver = 0 });
                }
                else
                {
                    // if the maximum electrical energy generated by the current powerplant plus the total electric
                    // energy generated so far exceeds the load, check if we can generate some power from this
                    // powerplant to match the load (if the minimum electrical energy generated by the current
                    // powerplant plus the total electric energy generated so far does not exceed the load). If yes,
                    // I add it to the response list as the difference between the load and the energy generated so far.
                    // If not, I consider the minimum generated power for the previous powerplant. If the current
                    // powerplant is the last in the group and either its minimum or maximum power added to the one
                    // generated so far will exceed the load, consider the minimum power that it can generate and
                    // adjust the power generated by the previous powerplant to match the difference to the load.
                    if (powerplant.MaxGenElectricalEnergy + totalGeneratedPower >= load)
                    {
                        if (powerplant.MinGenElectricalEnergy + totalGeneratedPower > load)
                        {
                            totalGeneratedPower -= prevAddedVal;

                            if (powerplant.IsLastInGroup)
                            {
                                response.Add(new PowerplantResponseInfo
                                {
                                    Name = powerplant.Name, PowerToDeliver = powerplant.MinGenElectricalEnergy
                                });
                                response.ElementAt(lastIndex - 1).
                                    PowerToDeliver = load - (totalGeneratedPower + powerplant.MinGenElectricalEnergy);
                                loadReached = true;
                            }
                            else
                            {
                                // consider the minimum value for the previous powerplant
                                response.ElementAt(lastIndex - 1).PowerToDeliver = prevMinVal;
                                totalGeneratedPower += prevMinVal;

                                // check if we can use the max value for the current powerplant in the new conditions
                                if (totalGeneratedPower + powerplant.MaxGenElectricalEnergy <= load)
                                {
                                    response.Add(new PowerplantResponseInfo
                                    {
                                        Name = powerplant.Name,
                                        PowerToDeliver = powerplant.MaxGenElectricalEnergy
                                    });
                                    totalGeneratedPower += powerplant.MaxGenElectricalEnergy;
                                }
                                else
                                {
                                    // use the minimum value
                                    response.Add(new PowerplantResponseInfo
                                    {
                                        Name = powerplant.Name,
                                        PowerToDeliver = powerplant.MinGenElectricalEnergy
                                    });
                                    totalGeneratedPower += powerplant.MinGenElectricalEnergy;
                                }

                                if (totalGeneratedPower == load)
                                    loadReached = true;

                                lastIndex++;
                            }
                        }
                        else
                        {
                            // the powerplant can generate power to match the load, but not the maximum one
                            response.Add(new PowerplantResponseInfo
                            {
                                Name = powerplant.Name,
                                PowerToDeliver = load - totalGeneratedPower
                            });
                            loadReached = true;
                        }

                        prevMinVal = powerplant.MinGenElectricalEnergy;
                        prevAddedVal = powerplant.IsLastInGroup ? load - totalGeneratedPower 
                                                                : powerplant.MinGenElectricalEnergy;
                    }
                    else
                    {
                        response.Add(new PowerplantResponseInfo
                        {
                            Name = powerplant.Name,
                            PowerToDeliver = powerplant.MaxGenElectricalEnergy
                        });

                        prevMinVal = powerplant.MinGenElectricalEnergy;
                        prevAddedVal = powerplant.MaxGenElectricalEnergy;
                        totalGeneratedPower += powerplant.MaxGenElectricalEnergy;
                        lastIndex++;
                    }
                }
            }

            return response;
        }
    }
}
