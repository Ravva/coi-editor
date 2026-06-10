using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Buildings.Storages;
using Mafi.Core.Buildings.Settlements;
using Mafi.Core.Economy;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Entities.Static;
using Mafi.Core.Environment;
using Mafi.Core.Game;
using Mafi.Core.Maintenance;
using Mafi.Core.Population;
using Mafi.Core.Products;
using Mafi.Core.PropertiesDb;
using Mafi.Core.Prototypes;
using Mafi.Core.Research;
using Mafi.Core.SpaceProgram;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Trains;
using Mafi.Core.Utils;
using Mafi.Core.Vehicles;
using Mafi.Core.Vehicles.Trucks;

namespace ResourceQuantityEditor {

	public sealed class SandboxFeatureService {
		private readonly SandboxManager m_sandbox;
		private readonly SourceSinkCheatManager m_sourceSink;
		private readonly InstaBuildManager m_instaBuild;
		private readonly WorkersManager m_workers;
		private readonly SettlementsManager m_settlements;
		private readonly ProtosDb m_protosDb;
		private readonly IEntitiesManager m_entities;
		private readonly UpointsManager m_upointsManager;
		private readonly IAssetTransactionManager m_assets;
		private readonly GameDifficultyConfig m_difficultyConfig;
		private readonly ResearchManager m_researchManager;
		private readonly VehiclesManager m_vehiclesManager;
		private readonly AirPollutionManager m_airPollutionManager;
		private readonly WaterPollutionManager m_waterPollutionManager;
		private readonly MaintenanceManager m_maintenanceManager;
		private readonly TerrainManager m_terrainManager;
		private readonly TerrainDesignationsManager m_terrainDesignationsManager;
		private readonly SurfaceDesignationsManager m_surfaceDesignationsManager;
		private readonly AsteroidsManager m_asteroidsManager;
		private readonly VisualTweaksService m_visualTweaks;
		private readonly PopsHealthManager m_popsHealthManager;
		private readonly FieldInfo m_landfillPartialField;
		private readonly FieldInfo m_landfillReportedField;
		private readonly FieldInfo m_bioWasteField;
		private readonly FieldInfo m_foodConsumptionField;
		private readonly FieldInfo m_fuelConsumptionDisabledField;
		private readonly FieldInfo m_locomotiveFuelConsumptionField;
		private readonly FieldInfo m_vehiclesNoFuelField;
		private readonly FieldInfo m_trainsNoFuelField;
		private readonly System.Collections.Generic.Dictionary<FoodProto, Fix32> m_originalFoodConsumption;
		private readonly System.Collections.Generic.Dictionary<IProperty<bool>, bool> m_originalFuelConsumptionDisabled;
		private readonly System.Collections.Generic.Dictionary<IProperty<Percent>, Percent> m_originalPercentProperties;
		private GameDifficultyConfig.VehiclesNoFuelSetting m_originalVehiclesNoFuel;
		private bool m_hasOriginalVehiclesNoFuel;
		private GameDifficultyConfig.TrainsNoFuelSetting m_originalTrainsNoFuel;
		private bool m_hasOriginalTrainsNoFuel;
		private Percent m_originalConstructionCostsDiff;
		private bool m_hasOriginalConstructionCostsDiff;
		private Percent m_originalResearchCostDiff;
		private bool m_hasOriginalResearchCostDiff;
		private Percent m_originalQuickActionsCostDiff;
		private bool m_hasOriginalQuickActionsCostDiff;
		private Percent m_originalPollutionDiff;
		private bool m_hasOriginalPollutionDiff;
		private Percent m_originalTreesGrowthDiff;
		private bool m_hasOriginalTreesGrowthDiff;

		public SandboxFeatureService(
			SandboxManager sandbox,
			SourceSinkCheatManager sourceSink,
			InstaBuildManager instaBuild,
			WorkersManager workers,
			SettlementsManager settlements,
			ProtosDb protosDb,
			IEntitiesManager entities,
			UpointsManager upointsManager,
			IAssetTransactionManager assets,
			GameDifficultyConfig difficultyConfig,
			ResearchManager researchManager,
			VehiclesManager vehiclesManager,
			AirPollutionManager airPollutionManager,
			WaterPollutionManager waterPollutionManager,
			MaintenanceManager maintenanceManager,
			TerrainManager terrainManager,
			TerrainDesignationsManager terrainDesignationsManager,
			SurfaceDesignationsManager surfaceDesignationsManager,
			AsteroidsManager asteroidsManager,
			VisualTweaksService visualTweaks,
			PopsHealthManager popsHealth) {
			m_sandbox = sandbox;
			m_sourceSink = sourceSink;
			m_instaBuild = instaBuild;
			m_workers = workers;
			m_settlements = settlements;
			m_protosDb = protosDb;
			m_entities = entities;
			m_upointsManager = upointsManager;
			m_assets = assets;
			m_difficultyConfig = difficultyConfig;
			m_researchManager = researchManager;
			m_vehiclesManager = vehiclesManager;
			m_airPollutionManager = airPollutionManager;
			m_waterPollutionManager = waterPollutionManager;
			m_maintenanceManager = maintenanceManager;
			m_terrainManager = terrainManager;
			m_terrainDesignationsManager = terrainDesignationsManager;
			m_surfaceDesignationsManager = surfaceDesignationsManager;
			m_asteroidsManager = asteroidsManager;
			m_visualTweaks = visualTweaks;
			m_popsHealthManager = popsHealth;
			m_landfillPartialField = typeof(Settlement).GetField("m_landfillInSettlementPartial", BindingFlags.Instance | BindingFlags.NonPublic);
			m_landfillReportedField = typeof(Settlement).GetField("m_landfillInSettlementReported", BindingFlags.Instance | BindingFlags.NonPublic);
			m_bioWasteField = typeof(Settlement).GetField("m_bioWasteInSettlement", BindingFlags.Instance | BindingFlags.NonPublic);
			m_foodConsumptionField = typeof(FoodProto).GetField("m_consumedPerHundredPopsPerMonth", BindingFlags.Instance | BindingFlags.NonPublic);
			m_fuelConsumptionDisabledField = typeof(FuelTank).GetField("m_fuelConsumptionDisabled", BindingFlags.Instance | BindingFlags.NonPublic);
			m_locomotiveFuelConsumptionField = typeof(Locomotive).GetField("<FuelConsumption>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			m_vehiclesNoFuelField = typeof(GameDifficultyConfig).GetField("<VehiclesNoFuel>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			m_trainsNoFuelField = typeof(GameDifficultyConfig).GetField("<TrainsNoFuel>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			m_originalFoodConsumption = new System.Collections.Generic.Dictionary<FoodProto, Fix32>();
			m_originalFuelConsumptionDisabled = new System.Collections.Generic.Dictionary<IProperty<bool>, bool>();
			m_originalPercentProperties = new System.Collections.Generic.Dictionary<IProperty<Percent>, Percent>();
		}

		public bool UnlimitedFood { get; private set; }
		public bool NoConstructionCosts { get; private set; }
		public bool FreeResearch { get; private set; }
		public bool InfiniteFocus { get; private set; }
		public bool NoAirPollution { get; private set; }
		public bool NoShipPollution { get; private set; }
		public bool NoTrainPollution { get; private set; }
		public bool NoVehiclePollution { get; private set; }
		public bool NoWaterPollution { get; private set; }
		public bool NoBioWaste { get; private set; }
		public bool NoLandfillWaste { get; private set; }
		public bool NoToxicWaste { get; private set; }
		public bool NoRadioactiveWaste { get; private set; }
		public bool UnlimitedUnity { get; private set; }
		public bool UnlimitedVehicleFuel { get; private set; }
		public bool InstantTreeGrowth { get; private set; }
		public bool ProcessTerrainDesignations { get; private set; }
		public bool ProcessSurfaceDesignations { get; private set; }
		public bool DisableTerrainPhysics { get; private set; }
		public bool InstantCargoShips { get; private set; }
		public bool UnlimitedMiningArea { get; private set; }
		public bool UnlimitedTowerArea { get; private set; }
		public bool NoCleanWaterNeed { get; private set; }
		public bool NoWastewaterProduction { get; private set; }
		public bool NoDiseaseEffects { get; private set; }
		public bool CloudsEnabled => m_visualTweaks.CloudsEnabled;
		public bool FogEnabled => m_visualTweaks.FogEnabled;
		public bool WeatherEffectsEnabled => m_visualTweaks.WeatherEffectsEnabled;

		public bool CanCheat { get { return m_sandbox.CanCheat; } }
		public bool IsInstaBuildEnabled { get { return m_sandbox.IsInstaBuildEnabled; } }
		public bool AreSourcesAndSinksAllowed { get { return m_sourceSink.AreSourcesAndSinksAllowed; } }
		public bool AreSourceSinksShownInToolbar { get { return m_sourceSink.AreSourceSinksShownInToolbar; } }
		public int TotalPopulation { get { return m_settlements.GetTotalPopulation(); } }
		public int FreeWorkersOrMissing { get { return m_workers.AmountOfFreeWorkersOrMissing; } }
		public int NumberOfWorkersWithheld { get { return m_workers.NumberOfWorkersWithheld; } }
		public bool IgnoreMissingWorkers { get { return m_workers.IgnoreMissingWorkers; } }
		public bool IgnoreMissingMaintenance { get { return m_sandbox.IgnoreMissingMaintenance; } }
		public bool EnableInstantConstruction { get { return m_sandbox.EnableInstantConstruction; } }
		public bool IgnoreFuelConsumption { get { return m_sandbox.IgnoreFuelConsumption; } }
		public bool IgnoreMissingPower { get { return m_sandbox.IgnoreMissingPower; } }
		public bool IgnoreMissingComputing { get { return m_sandbox.IgnoreMissingComputing; } }
		public bool IgnoreMissingUnity { get { return m_sandbox.IgnoreMissingUnity; } }
		public bool IgnoreMissingFood { get { return m_sandbox.IgnoreMissingFood; } }
		public bool IsAllResearchUnlocked { get { return m_sandbox.IsAllResearchUnlocked; } }
		public bool InstantBuildAndConstruction { get { return IsInstaBuildEnabled && EnableInstantConstruction; } }
		public string CurrentWeatherName {
			get {
				if (!m_sandbox.CurrentWeather.HasValue) {
					return "Dynamic";
				}
				return m_sandbox.CurrentWeather.ValueOrThrow("Weather was empty.").Strings.Name.ToString();
			}
		}
		public string CurrentWeatherId {
			get {
				if (!m_sandbox.CurrentWeather.HasValue) {
					return "";
				}
				return m_sandbox.CurrentWeather.ValueOrThrow("Weather was empty.").Id.ToString();
			}
		}
		public bool IsSunnyWeatherEnabled {
			get {
				if (!m_sandbox.CurrentWeather.HasValue) {
					return false;
				}
				return IsSunnyWeather(m_sandbox.CurrentWeather.ValueOrThrow("Weather was empty."));
			}
		}

		public string EnableSourceSinks() {
			return SetSourceSinks(true);
		}

		public string SetSourceSinks(bool enabled) {
			EnsureSandboxEnabled(enabled);
			m_sourceSink.SetAreSourcesAndSinksAllowed(enabled);
			if (!enabled) {
				return "Sandbox source/sink buildings disabled.";
			}
			m_sourceSink.SetAreSourcesAndSinksAllowed(true);
			InvokeNonPublic(m_sourceSink, "ShowSourcesSinksInToolbar");
			return "Sandbox source/sink buildings enabled and requested in toolbar.";
		}

		public string SetInstaBuild(bool enabled) {
			EnsureSandboxEnabled(enabled);
			InvokeNonPublic(m_instaBuild, "SetInstaBuild", enabled);
			return "Instant build " + (enabled ? "enabled." : "disabled.");
		}

		public string SetIgnoreMissingMaintenance(bool ignore) {
			EnsureSandboxEnabled(ignore);
			m_sandbox.IgnoreMissingMaintenance = ignore;
			return "Missing maintenance " + (ignore ? "ignored." : "required.");
		}

		public string SetEnableInstantConstruction(bool enabled) {
			EnsureSandboxEnabled(enabled);
			m_sandbox.EnableInstantConstruction = enabled;
			return "Instant construction " + (enabled ? "enabled." : "disabled.");
		}

		public string SetInstantBuildAndConstruction(bool enabled) {
			SetInstaBuild(enabled);
			SetEnableInstantConstruction(enabled);
			return "Instant build and construction " + (enabled ? "enabled." : "disabled.");
		}

		public string SetIgnoreFuelConsumption(bool ignore) {
			EnsureSandboxEnabled(ignore);
			m_sandbox.IgnoreFuelConsumption = ignore;
			UpdateFuelConsumptionDisabled();
			return "Fuel consumption " + (ignore ? "ignored." : "required.");
		}

		public string SetIgnoreMissingPower(bool ignore) {
			EnsureSandboxEnabled(ignore);
			m_sandbox.IgnoreMissingPower = ignore;
			return "Missing power " + (ignore ? "ignored." : "required.");
		}

		public string SetIgnoreMissingComputing(bool ignore) {
			EnsureSandboxEnabled(ignore);
			m_sandbox.IgnoreMissingComputing = ignore;
			return "Missing computing " + (ignore ? "ignored." : "required.");
		}

		public string SetIgnoreMissingUnity(bool ignore) {
			EnsureSandboxEnabled(ignore);
			m_sandbox.IgnoreMissingUnity = ignore;
			return "Missing unity " + (ignore ? "ignored." : "required.");
		}

		public string SetIgnoreMissingFood(bool ignore) {
			EnsureSandboxEnabled(ignore);
			m_sandbox.IgnoreMissingFood = ignore;
			return "Missing food " + (ignore ? "ignored." : "required.");
		}

		public string SetUnlimitedFood(bool enabled) {
			UnlimitedFood = enabled;
			if (enabled) {
				ApplyZeroFoodConsumption();
			} else {
				RestoreFoodConsumption();
			}
			return "Food consumption " + (enabled ? "disabled." : "restored.");
		}

		public string SetNoConstructionCosts(bool enabled) {
			NoConstructionCosts = enabled;
			if (enabled) {
				StoreOriginal(ref m_originalConstructionCostsDiff, ref m_hasOriginalConstructionCostsDiff, m_difficultyConfig.ConstructionCostsDiff);
				SetDifficultyPercent("<ConstructionCostsDiff>k__BackingField", Percent.Zero);
			} else if (m_hasOriginalConstructionCostsDiff) {
				SetDifficultyPercent("<ConstructionCostsDiff>k__BackingField", m_originalConstructionCostsDiff);
				m_hasOriginalConstructionCostsDiff = false;
			}
			return "Construction costs " + (enabled ? "disabled." : "restored.");
		}

		public string SetFreeResearch(bool enabled) {
			FreeResearch = enabled;
			if (enabled) {
				StoreOriginal(ref m_originalResearchCostDiff, ref m_hasOriginalResearchCostDiff, m_difficultyConfig.ResearchCostDiff);
				SetDifficultyPercent("<ResearchCostDiff>k__BackingField", Percent.Zero);
				InvokeNonPublic(m_researchManager, "updateNodesResearchCosts");
			} else if (m_hasOriginalResearchCostDiff) {
				SetDifficultyPercent("<ResearchCostDiff>k__BackingField", m_originalResearchCostDiff);
				m_hasOriginalResearchCostDiff = false;
				InvokeNonPublic(m_researchManager, "updateNodesResearchCosts");
			}
			return "Research costs " + (enabled ? "disabled." : "restored.");
		}

		public string FinishCurrentResearch() {
			EnsureSandboxEnabled(true);
			InvokeNonPublic(m_researchManager, "Cheat_FinishCurrent");
			return "Current research finished.";
		}

		public string FinishRepeatableResearch() {
			return FinishCurrentResearch();
		}

		public string SetInfiniteFocus(bool enabled) {
			InfiniteFocus = enabled;
			if (enabled) {
				StoreOriginal(ref m_originalQuickActionsCostDiff, ref m_hasOriginalQuickActionsCostDiff, m_difficultyConfig.QuickActionsCostDiff);
				SetDifficultyPercent("<QuickActionsCostDiff>k__BackingField", Percent.Zero);
				AddUnityReserve();
			} else if (m_hasOriginalQuickActionsCostDiff) {
				SetDifficultyPercent("<QuickActionsCostDiff>k__BackingField", m_originalQuickActionsCostDiff);
				m_hasOriginalQuickActionsCostDiff = false;
			}
			return "Quick action/focus costs " + (enabled ? "disabled." : "restored.");
		}

		public string SetNoAirPollution(bool enabled) {
			NoAirPollution = enabled;
			HealthCheatsPatch.NoAirPollution = enabled;
			SetPollutionDifficultyOverride(enabled);
			SetPercentPropertyOverride(GetFieldValue(m_airPollutionManager, "m_airPollutionMultiplier") as IProperty<Percent>, enabled, Percent.Zero);
			if (enabled) {
				ClearAirPollution();
			}
			return "Air pollution " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoShipPollution(bool enabled) {
			NoShipPollution = enabled;
			HealthCheatsPatch.NoShipPollution = enabled;
			SetPercentPropertyOverride(GetFieldValue(m_airPollutionManager, "m_shipsPollutionMultiplier") as IProperty<Percent>, enabled, Percent.Zero);
			if (enabled) {
				ClearNamedBuffer(m_airPollutionManager, "m_shipsPollutionBuffer", Quantity.Zero);
				ClearNamedBuffer(m_airPollutionManager, "m_shipsPollutionBufferPartial", PartialQuantity.Zero);
			}
			return "Ship pollution " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoTrainPollution(bool enabled) {
			NoTrainPollution = enabled;
			HealthCheatsPatch.NoTrainPollution = enabled;
			SetPercentPropertyOverride(GetFieldValue(m_airPollutionManager, "m_trainsPollutionMultiplier") as IProperty<Percent>, enabled, Percent.Zero);
			if (enabled) {
				ClearNamedBuffer(m_airPollutionManager, "m_trainsPollutionBuffer", Quantity.Zero);
				ClearNamedBuffer(m_airPollutionManager, "m_trainsPollutionBufferPartial", PartialQuantity.Zero);
			}
			return "Train pollution " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoVehiclePollution(bool enabled) {
			NoVehiclePollution = enabled;
			HealthCheatsPatch.NoVehiclePollution = enabled;
			SetPercentPropertyOverride(GetFieldValue(m_airPollutionManager, "m_vehiclesPollutionMultiplier") as IProperty<Percent>, enabled, Percent.Zero);
			if (enabled) {
				ClearNamedBuffer(m_airPollutionManager, "m_vehiclesPollutionBuffer", Quantity.Zero);
				ClearNamedBuffer(m_airPollutionManager, "m_vehiclesPollutionBufferPartial", PartialQuantity.Zero);
			}
			return "Vehicle pollution " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoWaterPollution(bool enabled) {
			NoWaterPollution = enabled;
			HealthCheatsPatch.NoWaterPollution = enabled;
			SetPercentPropertyOverride(GetFieldValue(m_waterPollutionManager, "m_waterPollutionMultiplier") as IProperty<Percent>, enabled, Percent.Zero);
			if (enabled) {
				ClearProductBufferField(m_waterPollutionManager, "m_pollutedWaterBuffer");
			}
			return "Water pollution " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoBioWaste(bool enabled) {
			NoBioWaste = enabled;
			if (enabled) {
				ClearBioWaste();
			}
			return "Bio waste generation " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoLandfillWaste(bool enabled) {
			NoLandfillWaste = enabled;
			if (enabled) {
				ClearLandfillWaste();
			}
			return "Landfill waste generation " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoToxicWaste(bool enabled) {
			NoToxicWaste = enabled;
			if (enabled) {
				ClearToxicWaste();
			}
			return "Toxic slurry waste " + (enabled ? "suppressed." : "restored.");
		}

		public string SetNoRadioactiveWaste(bool enabled) {
			NoRadioactiveWaste = enabled;
			if (enabled) {
				ClearRadioactiveWaste();
			}
			return "Depleted uranium waste " + (enabled ? "suppressed." : "restored.");
		}

		public string SetUnlimitedUnity(bool enabled) {
			UnlimitedUnity = enabled;
			EnsureSandboxEnabled(enabled);
			m_sandbox.IgnoreMissingUnity = enabled || m_sandbox.IgnoreMissingUnity;
			if (enabled) {
				AddUnityReserve();
			}
			return "Unlimited unity " + (enabled ? "enabled." : "disabled.");
		}

		public string SetUnlimitedVehicleFuel(bool enabled) {
			UnlimitedVehicleFuel = enabled;
			if (enabled) {
				ApplyVehiclesNoFuel();
				ApplyTrainsNoFuel();
			} else {
				RestoreVehiclesNoFuel();
				RestoreTrainsNoFuel();
			}
			UpdateFuelConsumptionDisabled();
			return "Unlimited vehicle fuel " + (enabled ? "enabled." : "disabled.");
		}

		public string SetInstantTreeGrowth(bool enabled) {
			InstantTreeGrowth = enabled;
			if (enabled) {
				StoreOriginal(ref m_originalTreesGrowthDiff, ref m_hasOriginalTreesGrowthDiff, m_difficultyConfig.TreesGrowthDiff);
				SetDifficultyPercent("<TreesGrowthDiff>k__BackingField", Percent.MaxValue);
			} else if (m_hasOriginalTreesGrowthDiff) {
				SetDifficultyPercent("<TreesGrowthDiff>k__BackingField", m_originalTreesGrowthDiff);
				m_hasOriginalTreesGrowthDiff = false;
			}
			return "Tree growth " + (enabled ? "boosted." : "restored.");
		}

		public string SetProcessTerrainDesignations(bool enabled) {
			ProcessTerrainDesignations = enabled;
			InvokeNonPublic(m_terrainDesignationsManager, "SetFulfillAllDesignations_TestOnly", enabled);
			return "Terrain designations quick fulfillment " + (enabled ? "enabled." : "disabled.");
		}

		public string SetProcessSurfaceDesignations(bool enabled) {
			ProcessSurfaceDesignations = enabled;
			InvokeNonPublic(m_surfaceDesignationsManager, "SetFulfillAllDesignations_TestOnly", enabled);
			return "Surface designations quick fulfillment " + (enabled ? "enabled." : "disabled.");
		}

		public string ClearAllTerrainDesignations() {
			m_terrainDesignationsManager.ClearAllDesignations();
			return "All terrain designations cleared.";
		}

		public string SetDisableTerrainPhysics(bool enabled) {
			DisableTerrainPhysics = enabled;
			if (enabled) {
				InvokeNonPublic(m_terrainManager, "ClearTerrainPhysicsSimulation");
			}
			return "Terrain physics " + (enabled ? "cleared and suppressed." : "restored.");
		}

		public string AddCargoShips(int count) {
			if (count < 0) {
				throw new ArgumentOutOfRangeException("count", count, "Cargo ship count must be non-negative.");
			}
			object cargoDepotManager = GetFieldValue(m_sandbox, "m_cargoDepotManager");
			InvokeNonPublic(cargoDepotManager, "ReportNewCargoShipFound", count);
			InvokeNonPublic(cargoDepotManager, "ReportNewCargoShipRepaired", count);
			return "Added " + count + " repaired cargo ship(s).";
		}

		public string IncreaseVehicleLimit(int amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException("amount", amount, "Vehicle limit increase must be non-negative.");
			}
			m_vehiclesManager.IncreaseVehicleLimit(amount);
			return "Vehicle limit increased by " + amount + ".";
		}

		public string RevealAllLocations() {
			object worldMapManager = GetFieldValue(m_sandbox, "m_worldMapManager");
			InvokeNonPublic(worldMapManager, "Cheat_RevealAndResolveAllEntities");
			return "All world map entities revealed and resolved.";
		}

		public string ScanAllLocations() {
			int count = ForEachWorldLocation(delegate(object location) {
				SetPropertyIfPresent(location, "IsScannedByRadar", true);
				SetPropertyIfPresent(location, "IsEnemyKnown", true);
			});
			return "Scanned " + count + " location(s).";
		}

		public string VisitAllLocations() {
			object worldMapManager = GetFieldValue(m_sandbox, "m_worldMapManager");
			object map = GetPropertyValue(worldMapManager, "Map");
			int count = 0;
			foreach (object location in (IEnumerable)GetPropertyValue(map, "Locations")) {
				InvokeNonPublic(map, "Visit", location, 99, 99);
				count++;
			}
			return "Visited " + count + " location(s).";
		}

		public string DefeatAllEnemies() {
			int count = ForEachWorldLocation(delegate(object location) {
				InvokeNonPublic(location, "MarkEnemyAsDefeated");
			});
			return "Defeated enemies at " + count + " location(s).";
		}

		public string ResetAllMaintenance() {
			InvokeByNameIfPresent(m_maintenanceManager, "Cheat_ResetAllMaintenance");
			return "Maintenance reset requested.";
		}

		public string EnableCommonCheats() {
			EnsureSandboxEnabled(true);
			m_sandbox.IgnoreMissingMaintenance = true;
			m_sandbox.IgnoreFuelConsumption = true;
			m_sandbox.IgnoreMissingWorkers = true;
			m_sandbox.IgnoreMissingPower = true;
			m_sandbox.IgnoreMissingComputing = true;
			m_sandbox.IgnoreMissingUnity = true;
			m_sandbox.IgnoreMissingFood = true;
			SetInstantBuildAndConstruction(true);
			EnableSourceSinks();
			UpdateFuelConsumptionDisabled();
			return "Common sandbox cheats enabled.";
		}

		public string DisableCommonCheats() {
			m_sandbox.IgnoreMissingMaintenance = false;
			m_sandbox.IgnoreFuelConsumption = false;
			m_sandbox.IgnoreMissingWorkers = false;
			m_sandbox.IgnoreMissingPower = false;
			m_sandbox.IgnoreMissingComputing = false;
			m_sandbox.IgnoreMissingUnity = false;
			m_sandbox.IgnoreMissingFood = false;
			SetInstantBuildAndConstruction(false);
			UpdateFuelConsumptionDisabled();
			return "Common sandbox cheats disabled.";
		}

		public string UnlockAllResearch() {
			EnsureSandboxEnabled(true);
			m_sandbox.UnlockAllResearch();
			InvokeNonPublic(m_researchManager, "Cheat_UnlockAllResearch");
			return "All research unlocked. Researched nodes=" + m_researchManager.ResearchedNodes.Count + "/" + m_researchManager.AllNodes.Length + ".";
		}

		public string SetPopulation(int targetPopulation) {
			if (targetPopulation < 0) {
				throw new ArgumentOutOfRangeException("targetPopulation", targetPopulation, "Population must be non-negative.");
			}

			EnsureSandboxEnabled(true);
			m_sandbox.SetPopulation(targetPopulation);
			m_settlements.RecalculateValues();

			int currentPopulation = TotalPopulation;
			if (currentPopulation > targetPopulation) {
				m_settlements.RemovePopsAsMuchAs(currentPopulation - targetPopulation);
				m_settlements.RecalculateValues();
			} else if (currentPopulation < targetPopulation) {
				m_settlements.AddPops(targetPopulation - currentPopulation, PopsAdditionReason.Other);
				m_settlements.RecalculateValues();
			}

			if (TotalPopulation != targetPopulation) {
				return GetWorkersStatus("Population requested " + targetPopulation + ", actual " + TotalPopulation + ".");
			}
			return GetWorkersStatus("Population set to " + targetPopulation + ".");
		}

		public string AddWorkers(int workers) {
			if (workers < 0) {
				throw new ArgumentOutOfRangeException("workers", workers, "Workers must be non-negative.");
			}

			m_workers.Cheat_addWorkers(workers);
			m_settlements.RecalculateValues();
			return GetWorkersStatus("Added " + workers + " workers.");
		}

		public string RemovePopulation(int population) {
			if (population < 0) {
				throw new ArgumentOutOfRangeException("population", population, "Population must be non-negative.");
			}

			int removed = m_settlements.RemovePopsAsMuchAs(population);
			m_settlements.RecalculateValues();
			return GetWorkersStatus("Removed " + removed + " population.");
		}

		public string SetIgnoreMissingWorkers(bool ignore) {
			EnsureSandboxEnabled(ignore);
			m_sandbox.IgnoreMissingWorkers = ignore;
			return GetWorkersStatus("Ignore missing workers " + (ignore ? "enabled." : "disabled."));
		}

		public string RemoveSelectedTrees() {
			EnsureSandboxEnabled(true);
			m_sandbox.RemoveSelectedTrees();
			return "Selected trees removed.";
		}

		public string AddRepairedCargoShip() {
			EnsureSandboxEnabled(true);
			m_sandbox.AddRepairedCargoShip();
			return "Added repaired cargo ship.";
		}

		public string SetWeather(string weatherId) {
			if (string.IsNullOrEmpty((weatherId ?? "").Trim())) {
				return ClearWeather();
			}
			WeatherProto weather = GetWeather(weatherId.Trim());
			EnsureSandboxEnabled(true);
			m_sandbox.SetWeatherId(new Proto.ID(weather.Id.ToString()));
			return "Weather locked to " + weather.Strings.Name + ".";
		}

		public string SetCurrentWeatherIntensity(int sunPercent, int rainPercent) {
			if (sunPercent < 0 || rainPercent < 0) {
				throw new ArgumentOutOfRangeException("sunPercent", sunPercent, "Weather intensity must be non-negative.");
			}
			WeatherProto weather = GetCurrentConcreteWeather();
			SetFieldIfPresent(typeof(WeatherProto).GetField("SunIntensity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), weather, Percent.FromPercentVal(sunPercent));
			SetFieldIfPresent(typeof(WeatherProto).GetField("RainIntensity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), weather, Percent.FromPercentVal(rainPercent));
			return "Weather intensity set: sun=" + sunPercent + "%, rain=" + rainPercent + "%.";
		}

		public string ClearWeather() {
			Proto.ID? weatherId = null;
			EnsureSandboxEnabled(true);
			m_sandbox.SetWeatherId(weatherId);
			return "Weather override cleared.";
		}

		public string ToggleSunnyWeather() {
			if (IsSunnyWeatherEnabled) {
				return ClearWeather();
			}
			WeatherProto weather = GetSunnyWeather();
			EnsureSandboxEnabled(true);
			m_sandbox.SetWeatherId(new Proto.ID(weather.Id.ToString()));
			return "Sunny weather enabled.";
		}

		public WeatherRow[] GetWeatherRows(string filter) {
			string normalizedFilter = (filter ?? string.Empty).Trim();
			return m_protosDb.All<WeatherProto>()
				.Where(x => MatchesWeatherFilter(x, normalizedFilter))
				.OrderBy(x => x.Id.ToString())
				.Select(x => new WeatherRow(x.Id.ToString(), x.Strings.Name.ToString()))
				.ToArray();
		}

		public AsteroidMaterialRow[] GetAsteroidMaterialRows(string filter) {
			string normalizedFilter = (filter ?? string.Empty).Trim();
			return m_protosDb.All<TerrainMaterialProto>()
				.Where(x => IsAsteroidMaterial(x))
				.Where(x => MatchesAsteroidMaterialFilter(x, normalizedFilter))
				.OrderByDescending(x => x.AsteroidSpawnWeight)
				.ThenBy(x => x.Id.ToString())
				.Select(x => new AsteroidMaterialRow(
					x.MinedProduct,
					x.Id.ToString(),
					x.MinedProduct.Strings.Name.ToString(),
					x.AsteroidSpawnWeight,
					x.IsAsteroidFillerMaterial))
				.ToArray();
		}

		public TerrainMaterialProto[] GetAllAsteroidMaterials() {
			return m_protosDb.All<TerrainMaterialProto>()
				.Where(x => IsAsteroidMaterial(x))
				.OrderByDescending(x => x.AsteroidSpawnWeight)
				.ThenBy(x => x.Id.ToString())
				.ToArray();
		}

		public AsteroidRow[] GetAsteroidRows() {
			return m_asteroidsManager.AsteroidsActive.AsEnumerable()
				.OrderBy(x => x.Id.Value)
				.Select(x => new AsteroidRow(
					x.Id.Value,
					GetAsteroidState(x),
					x.Radius.Value,
					x.TotalQuantity.ToString(),
					GetAsteroidMaterialsSummary(x)))
				.ToArray();
		}

		public string SpawnAsteroidToOrbit(string material1Id, string material2Id, int radius, int materialRatio) {
			if (radius <= 0) {
				throw new ArgumentOutOfRangeException("radius", radius, "Asteroid radius must be positive.");
			}
			if (materialRatio <= 0) {
				throw new ArgumentOutOfRangeException("materialRatio", materialRatio, "Material ratio must be positive.");
			}

			TerrainMaterialProto material1 = GetAsteroidMaterial(material1Id);
			Option<TerrainMaterialProto> material2 = string.IsNullOrEmpty((material2Id ?? "").Trim())
				? Option<TerrainMaterialProto>.None
				: Option<TerrainMaterialProto>.Some(GetAsteroidMaterial(material2Id));

			EnsureSandboxEnabled(true);
			Asteroid asteroid = m_asteroidsManager.CheatNewAsteroidToOrbit(
				new RelTile1i(radius),
				Option<TerrainMaterialProto>.Some(material1),
				material2,
				materialRatio);
			return "Asteroid #" + asteroid.Id.Value + " created in orbit: " + GetAsteroidMaterialsSummary(asteroid) + ".";
		}

		public string CaptureAsteroidToOrbit(int asteroidId) {
			Asteroid asteroid = GetActiveAsteroid(asteroidId);
			if (asteroid.ReachedOrbit) {
				return "Asteroid #" + asteroidId + " is already in orbit.";
			}

			EnsureSandboxEnabled(true);
			InvokeNonPublicBySuffix(asteroid, "ForcePutToOrbit");
			return "Asteroid #" + asteroidId + " captured to orbit.";
		}

		public string DropAsteroidAt(int asteroidId, int x, int y) {
			Asteroid asteroid = GetActiveAsteroid(asteroidId);
			if (!asteroid.ReachedOrbit) {
				InvokeNonPublicBySuffix(asteroid, "ForcePutToOrbit");
			}

			EnsureSandboxEnabled(true);
			InvokeNonPublic(m_asteroidsManager, "startAsteroidProcessing", asteroid, new Tile2i(x, y));
			return "Asteroid #" + asteroidId + " landing started at " + x + ", " + y + ".";
		}

		public string GetStatus() {
			return string.Format(
				"canCheat={0}, source/sink allowed={1}, toolbar={2}, instaBuild={3}, noWorkers={4}, noPower={5}, noComputing={6}, noUnity={7}, noFood={8}, noFuel={9}, noMaintenance={10}, instantConstruction={11}, allResearch={12}",
				m_sandbox.CanCheat,
				m_sourceSink.AreSourcesAndSinksAllowed,
				m_sourceSink.AreSourceSinksShownInToolbar,
				m_sandbox.IsInstaBuildEnabled,
				m_sandbox.IgnoreMissingWorkers,
				m_sandbox.IgnoreMissingPower,
				m_sandbox.IgnoreMissingComputing,
				m_sandbox.IgnoreMissingUnity,
				m_sandbox.IgnoreMissingFood,
				m_sandbox.IgnoreFuelConsumption,
				m_sandbox.IgnoreMissingMaintenance,
				m_sandbox.EnableInstantConstruction,
				m_sandbox.IsAllResearchUnlocked);
		}

		public string GetWorkersStatus(string prefix) {
			return string.Format(
				"{0} population={1}, freeOrMissingWorkers={2}, withheldWorkers={3}, ignoreMissingWorkers={4}",
				prefix,
				TotalPopulation,
				FreeWorkersOrMissing,
				NumberOfWorkersWithheld,
				IgnoreMissingWorkers);
		}

		public void MaintainUnlimitedOptions() {
			if (UnlimitedFood) {
				ApplyZeroFoodConsumption();
			}
			if (NoBioWaste) {
				ClearBioWaste();
			}
			if (NoLandfillWaste) {
				ClearLandfillWaste();
			}
			if (NoToxicWaste) {
				ClearToxicWaste();
			}
			if (NoRadioactiveWaste) {
				ClearRadioactiveWaste();
			}
			if (NoAirPollution) {
				ClearAirPollution();
			}
			if (NoWaterPollution) {
				ClearProductBufferField(m_waterPollutionManager, "m_pollutedWaterBuffer");
			}
			if (UnlimitedUnity) {
				EnsureSandboxEnabled(true);
				m_sandbox.IgnoreMissingUnity = true;
				AddUnityReserve();
			}
			if (UnlimitedVehicleFuel) {
				ApplyVehiclesNoFuel();
				ApplyTrainsNoFuel();
			}
			if (UnlimitedVehicleFuel || m_sandbox.IgnoreFuelConsumption) {
				ApplyFuelConsumptionDisabled();
			}
			if (DisableTerrainPhysics) {
				InvokeNonPublic(m_terrainManager, "ClearTerrainPhysicsSimulation");
			}
		}

		private static void StoreOriginal(ref Percent original, ref bool hasOriginal, Percent value) {
			if (!hasOriginal) {
				original = value;
				hasOriginal = true;
			}
		}

		private static object InvokeNonPublic(object target, string methodName, params object[] args) {
			MethodInfo method = target.GetType().GetMethod(
				methodName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null) {
				throw new MissingMethodException(target.GetType().FullName, methodName);
			}

			return method.Invoke(target, args);
		}

		private static object InvokeNonPublicBySuffix(object target, string methodNameSuffix, params object[] args) {
			MethodInfo method = target.GetType()
				.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.FirstOrDefault(x => x.Name == methodNameSuffix || x.Name.EndsWith("." + methodNameSuffix, StringComparison.Ordinal));
			if (method == null) {
				throw new MissingMethodException(target.GetType().FullName, methodNameSuffix);
			}

			return method.Invoke(target, args);
		}

		private void EnsureSandboxEnabled(bool enableRequested) {
			if (enableRequested && !m_sandbox.CanCheat) {
				InvokeNonPublic(m_sandbox, "enableSandbox");
			}
		}

		private void SetPollutionDifficultyOverride(bool enabled) {
			if (enabled) {
				StoreOriginal(ref m_originalPollutionDiff, ref m_hasOriginalPollutionDiff, m_difficultyConfig.PollutionDiff);
				SetDifficultyPercent("<PollutionDiff>k__BackingField", Percent.Zero);
			} else if (!NoAirPollution && m_hasOriginalPollutionDiff) {
				SetDifficultyPercent("<PollutionDiff>k__BackingField", m_originalPollutionDiff);
				m_hasOriginalPollutionDiff = false;
			}
		}

		private void SetDifficultyPercent(string fieldName, Percent value) {
			FieldInfo field = typeof(GameDifficultyConfig).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null) {
				throw new MissingFieldException(typeof(GameDifficultyConfig).FullName, fieldName);
			}
			field.SetValue(m_difficultyConfig, value);
		}

		private void SetPercentPropertyOverride(IProperty<Percent> property, bool enabled, Percent value) {
			if (property == null) {
				return;
			}
			if (enabled) {
				if (!m_originalPercentProperties.ContainsKey(property)) {
					m_originalPercentProperties.Add(property, property.Value);
				}
				property.OverrideValue(value);
			} else if (m_originalPercentProperties.ContainsKey(property)) {
				property.OverrideValue(m_originalPercentProperties[property]);
				m_originalPercentProperties.Remove(property);
			}
		}

		private WeatherProto GetWeather(string weatherId) {
			WeatherProto weather;
			if (!m_protosDb.TryGetProto(new Proto.ID(weatherId), out weather)) {
				throw new ArgumentException("Weather '" + weatherId + "' was not found.", "weatherId");
			}
			return weather;
		}

		private TerrainMaterialProto GetAsteroidMaterial(string materialId) {
			string normalizedId = (materialId ?? "").Trim();
			if (string.IsNullOrEmpty(normalizedId)) {
				throw new ArgumentException("Select asteroid material first.", "materialId");
			}

			TerrainMaterialProto material;
			if (!m_protosDb.TryGetProto(new Proto.ID(normalizedId), out material) || !IsAsteroidMaterial(material)) {
				throw new ArgumentException("Asteroid material '" + normalizedId + "' was not found.", "materialId");
			}
			return material;
		}

		private Asteroid GetActiveAsteroid(int asteroidId) {
			foreach (Asteroid asteroid in m_asteroidsManager.AsteroidsActive.AsEnumerable()) {
				if (asteroid.Id.Value == asteroidId) {
					return asteroid;
				}
			}
			throw new ArgumentException("Active asteroid #" + asteroidId + " was not found.", "asteroidId");
		}

		private static bool IsAsteroidMaterial(TerrainMaterialProto material) {
			// Включаем материалы с весом спавна > 0, филлеры, и кварцевый песок
			if (material.AsteroidSpawnWeight > 0 || material.IsAsteroidFillerMaterial) {
				return true;
			}
			// Добавляем кварцевый песок (Quartz) вручную
			string materialId = material.Id.ToString();
			if (materialId.IndexOf("Quartz", StringComparison.OrdinalIgnoreCase) >= 0 ||
			    materialId.IndexOf("Sand", StringComparison.OrdinalIgnoreCase) >= 0) {
				return true;
			}
			return false;
		}

		private static bool MatchesAsteroidMaterialFilter(TerrainMaterialProto material, string filter) {
			return string.IsNullOrEmpty(filter)
				|| material.Id.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
				|| material.MinedProduct.Strings.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static string GetAsteroidState(Asteroid asteroid) {
			if (asteroid.IsDropped) {
				return "Dropped";
			}
			if (asteroid.ReachedOrbit) {
				return "Orbit";
			}
			if (asteroid.IsTravellingToOrbit) {
				return "Travelling";
			}
			if (asteroid.IsDiscovered) {
				return "Discovered";
			}
			if (asteroid.IsBeingDiscovered) {
				return "Scanning";
			}
			return "Active";
		}

		private static string GetAsteroidMaterialsSummary(Asteroid asteroid) {
			return string.Join(", ", asteroid.Materials.Select(x => x.First.MinedProduct.Strings.Name + " x" + x.Second).ToArray());
		}

		private WeatherProto GetSunnyWeather() {
			WeatherProto weather;
			if (m_protosDb.TryGetProto(new Proto.ID("Sunny"), out weather)) {
				return weather;
			}
			weather = m_protosDb.All<WeatherProto>()
				.FirstOrDefault(IsSunnyWeather);
			if (weather == null) {
				throw new InvalidOperationException("Sunny weather was not found.");
			}
			return weather;
		}

		private WeatherProto GetCurrentConcreteWeather() {
			if (m_sandbox.CurrentWeather.HasValue) {
				return m_sandbox.CurrentWeather.ValueOrThrow("Weather was empty.");
			}
			return GetSunnyWeather();
		}

		private static bool IsSunnyWeather(WeatherProto weather) {
			return weather.Id.ToString().IndexOf("Sunny", StringComparison.OrdinalIgnoreCase) >= 0
				|| weather.Strings.Name.ToString().IndexOf("Sunny", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool MatchesWeatherFilter(WeatherProto weather, string filter) {
			if (string.IsNullOrEmpty(filter)) {
				return true;
			}
			return weather.Id.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
				|| weather.Strings.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private void ApplyZeroFoodConsumption() {
			if (m_foodConsumptionField == null) {
				throw new MissingFieldException(typeof(FoodProto).FullName, "m_consumedPerHundredPopsPerMonth");
			}
			foreach (FoodProto food in m_protosDb.All<FoodProto>()) {
				if (!m_originalFoodConsumption.ContainsKey(food)) {
					m_originalFoodConsumption.Add(food, (Fix32)m_foodConsumptionField.GetValue(food));
				}
				m_foodConsumptionField.SetValue(food, default(Fix32));
			}
		}

		private void RestoreFoodConsumption() {
			if (m_foodConsumptionField == null) {
				return;
			}
			foreach (System.Collections.Generic.KeyValuePair<FoodProto, Fix32> pair in m_originalFoodConsumption) {
				m_foodConsumptionField.SetValue(pair.Key, pair.Value);
			}
			m_originalFoodConsumption.Clear();
		}

		private void ClearBioWaste() {
			foreach (Settlement settlement in m_settlements.Settlements) {
				SetFieldIfPresent(m_bioWasteField, settlement, PartialQuantityLarge.Zero);
				ClearWasteModules(settlement.BioWasteModules.AsEnumerable());
			}
		}

		private void ClearLandfillWaste() {
			foreach (Settlement settlement in m_settlements.Settlements) {
				SetFieldIfPresent(m_landfillPartialField, settlement, PartialQuantity.Zero);
				SetFieldIfPresent(m_landfillReportedField, settlement, Quantity.Zero);
				ClearWasteModules(settlement.AllLandfillModules.AsEnumerable());
			}
		}

		private void ClearToxicWaste() {
			ProductProto toxicWaste = GetToxicWasteProduct();
			ClearProductFromGlobalAndStorages(toxicWaste);
		}

		private void ClearRadioactiveWaste() {
			ProductProto radioactiveWaste = GetRadioactiveWasteProduct();
			ClearProductFromGlobalAndStorages(radioactiveWaste);
		}

		private void ClearAirPollution() {
			ClearProductBufferField(m_airPollutionManager, "m_pollutedAirBuffer");
			ClearNamedBuffer(m_airPollutionManager, "m_shipsPollutionBuffer", Quantity.Zero);
			ClearNamedBuffer(m_airPollutionManager, "m_shipsPollutionBufferPartial", PartialQuantity.Zero);
			ClearNamedBuffer(m_airPollutionManager, "m_trainsPollutionBuffer", Quantity.Zero);
			ClearNamedBuffer(m_airPollutionManager, "m_trainsPollutionBufferPartial", PartialQuantity.Zero);
			ClearNamedBuffer(m_airPollutionManager, "m_vehiclesPollutionBuffer", Quantity.Zero);
			ClearNamedBuffer(m_airPollutionManager, "m_vehiclesPollutionBufferPartial", PartialQuantity.Zero);
		}

		private static void ClearProductBufferField(object target, string fieldName) {
			object buffer = GetFieldValue(target, fieldName);
			ProductBuffer productBuffer = buffer as ProductBuffer;
			if (productBuffer != null) {
				productBuffer.Clear();
			}
		}

		private static void ClearNamedBuffer(object target, string fieldName, object value) {
			FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null) {
				field.SetValue(target, value);
			}
		}

		private void ClearProductFromGlobalAndStorages(ProductProto product) {
			int globalAmount = m_assets.GetAvailableQuantityForRemoval(product).Value;
			if (globalAmount > 0) {
				m_assets.RemoveAsMuchAs(new ProductQuantity(product, new Quantity(globalAmount)), DestroyReason.Cheated);
			}
			foreach (Storage storage in m_entities.GetAllEntitiesOfType<Storage>()) {
				if (storage.StoredProduct.HasValue && storage.StoredProduct.ValueOrThrow("Storage product was empty.").Equals(product)) {
					storage.RemoveAsMuchAs(storage.CurrentQuantity);
				}
			}
			foreach (Truck truck in m_entities.GetAllEntitiesOfType<Truck>()) {
				Quantity cargoQuantity = truck.Cargo.GetQuantityOf(product);
				if (cargoQuantity.IsPositive) {
					TryCancelTruckJobs(truck);
					TryRemoveVehicleCargo(truck.Cargo, product, cargoQuantity);
				}
			}
		}

		private static void TryCancelTruckJobs(Truck truck) {
			if (TryInvokeNonPublic(truck, "CancelAllJobsAndResetState")) {
				return;
			}
			PropertyInfo jobs = truck.GetType().GetProperty("JobsOld", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (jobs == null) {
				return;
			}
			object jobsValue = jobs.GetValue(truck, null);
			if (jobsValue != null) {
				TryInvokeNonPublic(jobsValue, "CancelAll");
			}
		}

		private static void TryRemoveVehicleCargo(object cargo, ProductProto product, Quantity quantity) {
			if (cargo == null) {
				return;
			}
			if (TryInvokeNonPublic(cargo, "TryRemoveAsMuchAs", product, quantity)) {
				return;
			}
			TryInvokeNonPublic(cargo, "TryRemoveAsMuchAs", new ProductQuantity(product, quantity));
		}

		private static void ClearWasteModules(IEnumerable modules) {
			foreach (object module in modules) {
				ClearNamedProductBuffers(module, field => true);
			}
		}

		private static void ClearNamedProductBuffers(object target, Func<FieldInfo, bool> shouldFollowField) {
			ClearNamedProductBuffers(target, shouldFollowField, 0);
		}

		private static void ClearNamedProductBuffers(object target, Func<FieldInfo, bool> shouldFollowField, int depth) {
			if (target == null) {
				return;
			}
			if (depth > 5) {
				return;
			}
			ProductBuffer directBuffer = target as ProductBuffer;
			if (directBuffer != null) {
				directBuffer.Clear();
				return;
			}
			Type targetType = target.GetType();
			if (targetType == typeof(string) || targetType.IsPrimitive || targetType.IsEnum) {
				return;
			}
			IEnumerable enumerable = target as IEnumerable;
			if (enumerable != null) {
				foreach (object item in enumerable) {
					ClearNamedProductBuffers(item, shouldFollowField, depth + 1);
				}
				return;
			}
			FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < fields.Length; i++) {
				if (!shouldFollowField(fields[i])) {
					continue;
				}
				object value = fields[i].GetValue(target);
				ClearNamedProductBuffers(value, shouldFollowField, depth + 1);
			}
		}

		private ProductProto GetToxicWasteProduct() {
			ProductProto product;
			if (m_protosDb.TryGetProto(new ProductProto.ID("ToxicSlurry"), out product)) {
				return product;
			}
			product = m_protosDb.All<ProductProto>()
				.FirstOrDefault(x => x.Id.ToString().IndexOf("ToxicSlurry", StringComparison.OrdinalIgnoreCase) >= 0
					|| x.Strings.Name.ToString().IndexOf("toxic", StringComparison.OrdinalIgnoreCase) >= 0);
			if (product == null) {
				throw new InvalidOperationException("Toxic slurry product was not found.");
			}
			return product;
		}

		private ProductProto GetRadioactiveWasteProduct() {
			ProductProto product;
			if (m_protosDb.TryGetProto(new ProductProto.ID("UraniumDepleted"), out product)) {
				return product;
			}
			if (m_protosDb.TryGetProto(new ProductProto.ID("DepletedUranium"), out product)) {
				return product;
			}
			product = m_protosDb.All<ProductProto>()
				.FirstOrDefault(x => x.Id.ToString().IndexOf("UraniumDepleted", StringComparison.OrdinalIgnoreCase) >= 0
					|| x.Id.ToString().IndexOf("DepletedUranium", StringComparison.OrdinalIgnoreCase) >= 0
					|| x.Strings.Name.ToString().IndexOf("Depleted uranium", StringComparison.OrdinalIgnoreCase) >= 0);
			if (product == null) {
				throw new InvalidOperationException("Depleted uranium product was not found.");
			}
			return product;
		}

		private void AddUnityReserve() {
			InvokeNonPublic(m_upointsManager, "Cheat_addUnityOnce", new Upoints(100000));
		}

		private void UpdateFuelConsumptionDisabled() {
			if (UnlimitedVehicleFuel || m_sandbox.IgnoreFuelConsumption) {
				ApplyFuelConsumptionDisabled();
			} else {
				RestoreFuelConsumptionDisabled();
			}
		}

		private void ApplyFuelConsumptionDisabled() {
			foreach (Vehicle vehicle in m_entities.GetAllEntitiesOfType<Vehicle>()) {
				if (vehicle.FuelTank.HasValue) {
					SetFuelTankConsumptionDisabled(vehicle.FuelTank.ValueOrThrow("Vehicle fuel tank was empty."), true);
				}
			}
			foreach (Locomotive locomotive in m_entities.GetAllEntitiesOfType<Locomotive>()) {
				SetFieldIfPresent(m_locomotiveFuelConsumptionField, locomotive, Percent.Zero);
				if (locomotive.FuelTank.HasValue) {
					SetFuelTankConsumptionDisabled(locomotive.FuelTank.ValueOrThrow("Locomotive fuel tank was empty."), true);
				}
			}
		}

		private void RestoreFuelConsumptionDisabled() {
			foreach (System.Collections.Generic.KeyValuePair<IProperty<bool>, bool> pair in m_originalFuelConsumptionDisabled) {
				pair.Key.OverrideValue(pair.Value);
			}
			m_originalFuelConsumptionDisabled.Clear();
		}

		private void SetFuelTankConsumptionDisabled(IFuelTankReadonly tankReadonly, bool disabled) {
			if (m_fuelConsumptionDisabledField == null) {
				throw new MissingFieldException(typeof(FuelTank).FullName, "m_fuelConsumptionDisabled");
			}
			FuelTank tank = tankReadonly as FuelTank;
			if (tank == null) {
				return;
			}
			IProperty<bool> property = (IProperty<bool>)m_fuelConsumptionDisabledField.GetValue(tank);
			if (!m_originalFuelConsumptionDisabled.ContainsKey(property)) {
				m_originalFuelConsumptionDisabled.Add(property, property.Value);
			}
			property.OverrideValue(disabled);
		}

		private void FillVehicleFuelTanks() {
			foreach (Vehicle vehicle in m_entities.GetAllEntitiesOfType<Vehicle>()) {
				if (!vehicle.FuelTank.HasValue) {
					continue;
				}
				IFuelTankReadonly tank = vehicle.FuelTank.ValueOrThrow("Vehicle fuel tank was empty.");
				Quantity freeCapacity = tank.GetFreeCapacity();
				if (freeCapacity.IsPositive) {
					vehicle.AddFuelAsMuchAs(new ProductQuantity(tank.Proto.Product, freeCapacity));
				}
			}
		}

		private void ApplyVehiclesNoFuel() {
			if (m_vehiclesNoFuelField == null) {
				throw new MissingFieldException(typeof(GameDifficultyConfig).FullName, "<VehiclesNoFuel>k__BackingField");
			}
			if (!m_hasOriginalVehiclesNoFuel) {
				m_originalVehiclesNoFuel = m_difficultyConfig.VehiclesNoFuel;
				m_hasOriginalVehiclesNoFuel = true;
			}
			m_vehiclesNoFuelField.SetValue(m_difficultyConfig, GameDifficultyConfig.VehiclesNoFuelSetting.SlowDown);
		}

		private void RestoreVehiclesNoFuel() {
			if (m_hasOriginalVehiclesNoFuel) {
				SetFieldIfPresent(m_vehiclesNoFuelField, m_difficultyConfig, m_originalVehiclesNoFuel);
				m_hasOriginalVehiclesNoFuel = false;
			}
		}

		private void ApplyTrainsNoFuel() {
			if (m_trainsNoFuelField == null) {
				throw new MissingFieldException(typeof(GameDifficultyConfig).FullName, "<TrainsNoFuel>k__BackingField");
			}
			if (!m_hasOriginalTrainsNoFuel) {
				m_originalTrainsNoFuel = m_difficultyConfig.TrainsNoFuel;
				m_hasOriginalTrainsNoFuel = true;
			}
			m_trainsNoFuelField.SetValue(m_difficultyConfig, GameDifficultyConfig.TrainsNoFuelSetting.SlowDown);
		}

		private void RestoreTrainsNoFuel() {
			if (m_hasOriginalTrainsNoFuel) {
				SetFieldIfPresent(m_trainsNoFuelField, m_difficultyConfig, m_originalTrainsNoFuel);
				m_hasOriginalTrainsNoFuel = false;
			}
		}

		private static void SetFieldIfPresent(FieldInfo field, object target, object value) {
			if (field != null) {
				field.SetValue(target, value);
			}
		}

		private object GetManagerFromField(string fieldName) {
			object manager = GetFieldValue(m_sandbox, fieldName);
			if (manager == null) {
				throw new MissingFieldException(typeof(SandboxManager).FullName, fieldName);
			}
			return manager;
		}

		private static object GetFieldValue(object target, string fieldName) {
			if (target == null) {
				return null;
			}
			FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return field == null ? null : field.GetValue(target);
		}

		private static object GetPropertyValue(object target, string propertyName) {
			if (target == null) {
				return null;
			}
			PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return property == null ? null : property.GetValue(target, null);
		}

		private static void SetPropertyIfPresent(object target, string propertyName, object value) {
			if (target == null) {
				return;
			}
			PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanWrite) {
				property.SetValue(target, value, null);
			}
		}

		private int ForEachWorldLocation(Action<object> action) {
			object worldMapManager = GetFieldValue(m_sandbox, "m_worldMapManager");
			object map = GetPropertyValue(worldMapManager, "Map");
			int count = 0;
			foreach (object location in (IEnumerable)GetPropertyValue(map, "Locations")) {
				action(location);
				count++;
			}
			return count;
		}

		private static bool InvokeByNameIfPresent(object target, string methodName, params object[] args) {
			if (target == null) {
				return false;
			}
			MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null) {
				return false;
			}
			method.Invoke(target, args);
			return true;
		}

		private static bool TryInvokeNonPublic(object target, string methodName, params object[] args) {
			MethodInfo method = target.GetType().GetMethod(
				methodName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null) {
				return false;
			}
			method.Invoke(target, args);
			return true;
		}

		public string SetInstantCargoShips(bool enabled) {
			InstantCargoShips = enabled;
			InstantCargoShipsPatch.InstantCargoShipsEnabled = enabled;
			return enabled ? "Instant cargo ships enabled." : "Instant cargo ships disabled.";
		}

		public string SetUnlimitedMiningArea(bool enabled) {
			UnlimitedMiningArea = enabled;
			UnlimitedDesignationsPatch.SetUnlimitedMining(enabled);
			return enabled ? "Unlimited mining/surface designations enabled." : "Unlimited mining/surface designations disabled.";
		}

		public string SetUnlimitedTowerArea(bool enabled) {
			UnlimitedTowerArea = enabled;
			UnlimitedDesignationsPatch.SetUnlimitedTowerArea(enabled);
			return enabled ? "Unlimited tower area enabled." : "Unlimited tower area disabled.";
		}

		public string SetCloudsEnabled(bool enabled) {
			m_visualTweaks.SetCloudsEnabled(enabled);
			return enabled ? "Clouds enabled." : "Clouds disabled.";
		}

		public string SetFogEnabled(bool enabled) {
			m_visualTweaks.SetFogRendering(enabled);
			return enabled ? "Fog rendering enabled." : "Fog rendering disabled.";
		}

		public string SetWeatherEffectsEnabled(bool enabled) {
			m_visualTweaks.SetWeatherEffectsVisible(enabled);
			return enabled ? "Weather effects enabled." : "Weather effects disabled.";
		}

		public string SetNoCleanWaterNeed(bool enabled) {
			NoCleanWaterNeed = enabled;
			SettlementCheatsPatch.NoCleanWaterNeed = enabled;
			return enabled ? "No clean water need enabled." : "No clean water need disabled.";
		}

		public string SetNoWastewaterProduction(bool enabled) {
			NoWastewaterProduction = enabled;
			SettlementCheatsPatch.NoWastewaterProduction = enabled;
			return enabled ? "No wastewater production enabled." : "No wastewater production disabled.";
		}

		public string SetNoDiseaseEffects(bool enabled) {
			NoDiseaseEffects = enabled;
			// Используем встроенный метод игры через Reflection
			TryInvokeNonPublic(m_popsHealthManager, "SetDisableDiseases", enabled);
			return enabled ? "Diseases disabled." : "Diseases enabled.";
		}
	}

	public struct WeatherRow {
		public readonly string Id;
		public readonly string Name;

		public WeatherRow(string id, string name) {
			Id = id;
			Name = name;
		}
	}

	public struct AsteroidMaterialRow {
		public readonly ProductProto Product;
		public readonly string Id;
		public readonly string Name;
		public readonly int SpawnWeight;
		public readonly bool IsFiller;

		public AsteroidMaterialRow(ProductProto product, string id, string name, int spawnWeight, bool isFiller) {
			Product = product;
			Id = id;
			Name = name;
			SpawnWeight = spawnWeight;
			IsFiller = isFiller;
		}
	}

	public struct AsteroidRow {
		public readonly int Id;
		public readonly string State;
		public readonly int Radius;
		public readonly string Quantity;
		public readonly string Materials;

		public AsteroidRow(int id, string state, int radius, string quantity, string materials) {
			Id = id;
			State = state;
			Radius = radius;
			Quantity = quantity;
			Materials = materials;
		}
	}
}
