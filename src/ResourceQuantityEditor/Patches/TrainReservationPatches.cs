using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core.Trains;

namespace ResourceQuantityEditor {

	public static class TrainReservationPatches {

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

		[HarmonyPatch(typeof(Train), "get_AttemptedReservationDistance")]
		public static class TrainAttemptedReservationDistancePatch {
			private static bool s_loggedFieldNames = false;

			public static void Postfix(ref RelTile1f __result) {
				try {
					Type relType = typeof(RelTile1f);

					if (!s_loggedFieldNames) {
						FieldInfo[] allFields = relType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						foreach (var f in allFields) {
							Log.Info("TrainReservationPatches: RelTile1f field: " + f.FieldType.Name + " " + f.Name);
						}
						s_loggedFieldNames = true;
					}

					FieldInfo rawField = null;
					string[] fieldNames = { "m_rawValue", "RawValue", "m_value", "Value", "m_data", "Data" };
					foreach (string name in fieldNames) {
						rawField = relType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (rawField != null) break;
					}

					if (rawField != null) {
						int currentRaw = (int)rawField.GetValue(__result);
						int maxRaw = (int)Math.Round(80.0 * 1024.0);
						if (currentRaw > maxRaw) {
							rawField.SetValue(__result, maxRaw);
						}
					}
				} catch { }
			}
		}
	}
}
