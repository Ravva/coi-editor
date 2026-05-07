using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Core.Population;
using Mafi.Core.Prototypes;

namespace ResourceQuantityEditor
{
	[HarmonyPatch(typeof(PopsHealthManager), "AddHealthDecrease")]
	public static class HealthCheatsPatch
	{
		public static bool NoAirPollution = false;
		public static bool NoWaterPollution = false;
		public static bool NoShipPollution = false;
		public static bool NoVehiclePollution = false;
		public static bool NoTrainPollution = false;

		[HarmonyPrefix]
		public static bool Prefix(Proto.ID categoryId)
		{
			if (categoryId == IdsCore.HealthPointsCategories.AirPollution && NoAirPollution) return false;
			if (categoryId == IdsCore.HealthPointsCategories.AirPollutionShips && NoShipPollution) return false;
			if (categoryId == IdsCore.HealthPointsCategories.AirPollutionVehicles && NoVehiclePollution) return false;
			if (categoryId == IdsCore.HealthPointsCategories.AirPollutionTrains && NoTrainPollution) return false;
			if (categoryId == IdsCore.HealthPointsCategories.WaterPollution && NoWaterPollution) return false;
			
			return true;
		}
	}
}