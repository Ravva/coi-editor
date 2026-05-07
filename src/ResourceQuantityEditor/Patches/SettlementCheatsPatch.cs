using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Core.Buildings.Settlements;
using Mafi.Core.Entities.Static;
using Mafi.Core.Products;

namespace ResourceQuantityEditor
{
	[HarmonyPatch(typeof(SettlementServiceModule), "TrySatisfyNeedOnNewDay")]
	public static class SettlementCheatsPatch
	{
		public static bool NoCleanWaterNeed = false;
		public static bool NoWastewaterProduction = false;

		[HarmonyPrefix]
		public static bool Prefix(SettlementServiceModule __instance, int popsToSatisfy, ref int __result)
		{
			if (!NoCleanWaterNeed && !NoWastewaterProduction)
			{
				return true;
			}

			try
			{
				FieldInfo m_didSatisfyField = typeof(SettlementServiceModule).GetField("m_didSatisfyPopsInLastUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
				FieldInfo m_unconsumedInputField = typeof(SettlementServiceModule).GetField("m_unconsumedInput", BindingFlags.Instance | BindingFlags.NonPublic);
				FieldInfo m_unconsumedOutputField = typeof(SettlementServiceModule).GetField("m_unconsumedOutput", BindingFlags.Instance | BindingFlags.NonPublic);
				FieldInfo m_totalInputThisMonthField = typeof(SettlementServiceModule).GetField("TotalInputThisMonth", BindingFlags.Instance | BindingFlags.NonPublic);
				FieldInfo m_totalOutputThisMonthField = typeof(SettlementServiceModule).GetField("TotalOutputThisMonth", BindingFlags.Instance | BindingFlags.NonPublic);

				if (NoCleanWaterNeed && __instance.Prototype.InputProduct != null)
				{
					m_didSatisfyField?.SetValue(__instance, true);
					m_unconsumedInputField?.SetValue(__instance, PartialQuantity.Zero);
					m_totalInputThisMonthField?.SetValue(__instance, Quantity.Zero);
					__result = 0; // 0 pops NOT satisfied
					
					// Если мы не отключаем сточные воды, нам все равно нужно вызвать оригинальную логику или симулировать ее для вывода
					if (!NoWastewaterProduction) return true; 
					
					return false;
				}

				if (NoWastewaterProduction && __instance.Prototype.OutputProduct.HasValue)
				{
					m_unconsumedOutputField?.SetValue(__instance, PartialQuantity.Zero);
					m_totalOutputThisMonthField?.SetValue(__instance, Quantity.Zero);
					if (NoCleanWaterNeed) return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				Log.Error("SettlementCheatsPatch failed: " + ex.Message);
				return true;
			}
		}
	}
}