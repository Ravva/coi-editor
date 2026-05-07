using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Buildings.Cargo.Ships;
using Mafi.Core.Entities;
using Mafi.Core.Population;
using Mafi.Core.World;
using Mafi.Core.World.Contracts;
using Mafi.Localization;

namespace ResourceQuantityEditor
{
	public static class InstantCargoShipsPatch
	{
		public static bool InstantCargoShipsEnabled = false;

		// Мгновенный обмен товарами при попытке отправления
		[HarmonyPatch(typeof(ContractsManager), "CanShipDepartForContract")]
		[HarmonyPrefix]
		public static bool PrefixCanShipDepart(CargoShipV2 ship, ContractProto contract, bool payUnityCost, bool wasDepartureRequested, ref ContractsManager.ShipDepartureCheckResult __result, ContractsManager __instance)
		{
			if (!InstantCargoShipsEnabled)
			{
				return true;
			}

			try
			{
				FieldInfo activeContractsField = typeof(ContractsManager).GetField("m_activePaidContracts", BindingFlags.Instance | BindingFlags.NonPublic);
				if (activeContractsField == null) return true;
				
				Set<ContractProto> activeContracts = activeContractsField.GetValue(__instance) as Set<ContractProto>;
				if (activeContracts == null || !activeContracts.Contains(contract)) return true;

				MethodInfo getShipCargoStatsMethod = typeof(ContractsManager).GetMethod("getShipCargoStats", BindingFlags.Instance | BindingFlags.NonPublic);
				if (getShipCargoStatsMethod == null) return true;

				object[] args = new object[6] { ship, contract, Quantity.Zero, Quantity.Zero, Quantity.Zero, Quantity.Zero };
				getShipCargoStatsMethod.Invoke(__instance, args);

				Quantity exportQty = (Quantity)args[2];
				Quantity importCap = (Quantity)args[5];

				// Если нечего везти или некуда грузить - ждем (стандартное поведение)
				if (exportQty.IsZero || importCap.IsZero)
				{
					__result = ContractsManager.ShipDepartureCheckResult.WaitingForCargo;
					return false;
				}

				// Проверяем единство
				Quantity toBuy = __instance.GetToBuy(contract);
				Quantity quantity = (exportQty.Value * toBuy / contract.QuantityToPayWith.Value).Min(importCap);
				Upoints unityCost = contract.CalculateUpointsForQuantityBought(quantity);

				FieldInfo upointsManagerField = typeof(ContractsManager).GetField("m_upointsManager", BindingFlags.Instance | BindingFlags.NonPublic);
				IUpointsManager upointsManager = upointsManagerField?.GetValue(__instance) as IUpointsManager;

				if (upointsManager != null && !upointsManager.CanConsume(unityCost))
				{
					__result = ContractsManager.ShipDepartureCheckResult.NotEnoughUpoints;
					return false;
				}

				if (payUnityCost && upointsManager != null)
				{
					upointsManager.ConsumeExactly(IdsCore.UpointsCategories.Contract, unityCost, default(Option<IEntity>), (LocStr?)Tr.Contract__ExchangeCost);
				}

				// МГНОВЕННЫЙ ОБМЕН
				__instance.ExchangeContractProducts(ship, contract);
				
				// Возвращаем Ok, чтобы корабль "совершил" рейс (который будет мгновенным благодаря другому патчу)
				__result = ContractsManager.ShipDepartureCheckResult.Ok;
				Log.Info($"ResourceQuantityEditor: Instant exchange for {contract.ProductToBuy.Id} performed.");
				return false;
			}
			catch (Exception ex)
			{
				Log.Error("InstantCargoShipsPatch.Prefix failed: " + ex.Message);
				return true;
			}
		}

		// Мгновенное перемещение по карте мира
		[HarmonyPatch(typeof(BattleShip), "GetTravelTimeFromDistance")]
		[HarmonyPostfix]
		public static void PostfixTravelTime(ref RelGameDate __result)
		{
			if (InstantCargoShipsEnabled)
			{
				// Устанавливаем минимальное время в пути (1 день в игровых расчетах обычно минимум)
				__result = RelGameDate.FromDays(0); 
			}
		}
	}
}
