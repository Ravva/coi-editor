using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core.Trains;

namespace ResourceQuantityEditor {

	public static class TrainReservationPatches {

		/// <summary>
		/// Limits how many waypoints a train reserves ahead.
		/// At 200 km/h the game reserves hundreds of waypoints, causing deadlocks
		/// where trains block each other across the map. Capping at 120 prevents this
		/// while still allowing normal navigation through junctions.
		/// When stopped at station/depot, reservation is zeroed.
		/// </summary>
		[HarmonyPatch(typeof(Train), "getOptimalReservedWaypointsCount")]
		public static class TrainGetOptimalReservedWaypointsCountPatch {
			public static void Postfix(Train __instance, ref int __result) {
				if ((__instance.CurrentStation.HasValue || __instance.Depot.HasValue) && __instance.TargetSpeed.IsZero) {
					__result = 0;
					return;
				}

				if (__result > 120) {
					__result = 120;
				}
			}
		}
	}
}
