using System;
using System.IO;
using System.Linq;
using System.Text;
using Mafi;

namespace ResourceQuantityEditor {

	public sealed class OptionsStateService {
		private readonly SandboxFeatureService m_sandboxFeatures;
		private readonly LocomotiveEditorService m_locoEditor;
		private readonly string m_saveFilePath;

		public OptionsStateService(SandboxFeatureService sandboxFeatures, LocomotiveEditorService locoEditor) {
			m_sandboxFeatures = sandboxFeatures;
			m_locoEditor = locoEditor;
			string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string modDataPath = Path.Combine(appDataPath, "Captain of Industry", "Mods", "ResourceQuantityEditor");
			if (!Directory.Exists(modDataPath)) {
				Directory.CreateDirectory(modDataPath);
			}
			m_saveFilePath = Path.Combine(modDataPath, "options_state.txt");
		}

		public string SaveCurrentState() {
			try {
				StringBuilder sb = new StringBuilder();
				
				// Economy cheats
				sb.AppendLine("InstantBuildAndConstruction=" + m_sandboxFeatures.InstantBuildAndConstruction);
				sb.AppendLine("AreSourcesAndSinksAllowed=" + m_sandboxFeatures.AreSourcesAndSinksAllowed);
				sb.AppendLine("IgnoreMissingMaintenance=" + m_sandboxFeatures.IgnoreMissingMaintenance);
				sb.AppendLine("NoConstructionCosts=" + m_sandboxFeatures.NoConstructionCosts);
				sb.AppendLine("FreeResearch=" + m_sandboxFeatures.FreeResearch);
				sb.AppendLine("InfiniteFocus=" + m_sandboxFeatures.InfiniteFocus);
				
				// Settlement needs
				sb.AppendLine("IgnoreMissingFood=" + m_sandboxFeatures.IgnoreMissingFood);
				sb.AppendLine("IgnoreMissingWorkers=" + m_sandboxFeatures.IgnoreMissingWorkers);
				sb.AppendLine("IgnoreMissingPower=" + m_sandboxFeatures.IgnoreMissingPower);
				sb.AppendLine("IgnoreMissingComputing=" + m_sandboxFeatures.IgnoreMissingComputing);
				sb.AppendLine("IgnoreMissingUnity=" + m_sandboxFeatures.IgnoreMissingUnity);
				sb.AppendLine("UnlimitedUnity=" + m_sandboxFeatures.UnlimitedUnity);
				sb.AppendLine("NoCleanWaterNeed=" + m_sandboxFeatures.NoCleanWaterNeed);
				sb.AppendLine("NoWastewaterProduction=" + m_sandboxFeatures.NoWastewaterProduction);
				sb.AppendLine("NoDiseaseEffects=" + m_sandboxFeatures.NoDiseaseEffects);
				
				// Environment cheats
				sb.AppendLine("UnlimitedFood=" + m_sandboxFeatures.UnlimitedFood);
				sb.AppendLine("InstantTreeGrowth=" + m_sandboxFeatures.InstantTreeGrowth);
				sb.AppendLine("NoAirPollution=" + m_sandboxFeatures.NoAirPollution);
				sb.AppendLine("NoWaterPollution=" + m_sandboxFeatures.NoWaterPollution);
				sb.AppendLine("NoShipPollution=" + m_sandboxFeatures.NoShipPollution);
				sb.AppendLine("NoTrainPollution=" + m_sandboxFeatures.NoTrainPollution);
				sb.AppendLine("NoVehiclePollution=" + m_sandboxFeatures.NoVehiclePollution);
				sb.AppendLine("NoBioWaste=" + m_sandboxFeatures.NoBioWaste);
				sb.AppendLine("NoLandfillWaste=" + m_sandboxFeatures.NoLandfillWaste);
				sb.AppendLine("NoToxicWaste=" + m_sandboxFeatures.NoToxicWaste);
				sb.AppendLine("NoRadioactiveWaste=" + m_sandboxFeatures.NoRadioactiveWaste);
				
				// Terrain controls
				sb.AppendLine("ProcessTerrainDesignations=" + m_sandboxFeatures.ProcessTerrainDesignations);
				sb.AppendLine("ProcessSurfaceDesignations=" + m_sandboxFeatures.ProcessSurfaceDesignations);
				sb.AppendLine("DisableTerrainPhysics=" + m_sandboxFeatures.DisableTerrainPhysics);
				sb.AppendLine("UnlimitedMiningArea=" + m_sandboxFeatures.UnlimitedMiningArea);
				sb.AppendLine("UnlimitedTowerArea=" + m_sandboxFeatures.UnlimitedTowerArea);
				
				// Logistics
				sb.AppendLine("IgnoreFuelConsumption=" + m_sandboxFeatures.IgnoreFuelConsumption);
				sb.AppendLine("UnlimitedVehicleFuel=" + m_sandboxFeatures.UnlimitedVehicleFuel);
				sb.AppendLine("InstantCargoShips=" + m_sandboxFeatures.InstantCargoShips);

				// Locomotive settings
				foreach (var loco in m_locoEditor.GetAllLocomotives()) {
					string id = loco.Id.ToString();
					sb.AppendLine("LocoSpeed_" + id + "=" + m_locoEditor.GetMaxSpeedKmh(loco).ToString(System.Globalization.CultureInfo.InvariantCulture));
					sb.AppendLine("LocoPower_" + id + "=" + m_locoEditor.GetFieldFloat(loco, "EnginePowerKw").ToString(System.Globalization.CultureInfo.InvariantCulture));
					sb.AppendLine("LocoTractive_" + id + "=" + m_locoEditor.GetFieldFloat(loco, "StartingTractiveEffort").ToString(System.Globalization.CultureInfo.InvariantCulture));
					sb.AppendLine("LocoBraking_" + id + "=" + m_locoEditor.GetFieldFloat(loco, "BrakingForceKn").ToString(System.Globalization.CultureInfo.InvariantCulture));
				}
				
				File.WriteAllText(m_saveFilePath, sb.ToString());
				return "Options state saved to " + m_saveFilePath;
			} catch (Exception ex) {
				return "Failed to save options state: " + ex.Message;
			}
		}

		public string LoadSavedState() {
			try {
				if (!File.Exists(m_saveFilePath)) {
					return "No saved options state found.";
				}

				string[] lines = File.ReadAllLines(m_saveFilePath);
				int appliedCount = 0;

				foreach (string line in lines) {
					if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) {
						continue;
					}

					string[] parts = line.Split('=');
					if (parts.Length != 2) {
						continue;
					}

					string key = parts[0].Trim();
					string valueStr = parts[1].Trim();

					bool boolValue;
					if (bool.TryParse(valueStr, out boolValue)) {
						ApplyOption(key, boolValue);
						appliedCount++;
					} else {
						float floatValue;
						if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out floatValue)) {
							ApplyFloatOption(key, floatValue);
							appliedCount++;
						}
					}
				}

				return "Loaded " + appliedCount + " options from saved state.";
			} catch (Exception ex) {
				return "Failed to load options state: " + ex.Message;
			}
		}

		public string ClearSavedState() {
			try {
				if (File.Exists(m_saveFilePath)) {
					File.Delete(m_saveFilePath);
					return "Saved options state cleared.";
				}
				return "No saved options state to clear.";
			} catch (Exception ex) {
				return "Failed to clear saved state: " + ex.Message;
			}
		}

		public bool HasSavedState() {
			return File.Exists(m_saveFilePath);
		}

		private void ApplyOption(string key, bool value) {
			try {
				switch (key) {
					// Economy cheats
					case "InstantBuildAndConstruction":
						m_sandboxFeatures.SetInstantBuildAndConstruction(value);
						break;
					case "AreSourcesAndSinksAllowed":
						m_sandboxFeatures.SetSourceSinks(value);
						break;
					case "IgnoreMissingMaintenance":
						m_sandboxFeatures.SetIgnoreMissingMaintenance(value);
						break;
					case "NoConstructionCosts":
						m_sandboxFeatures.SetNoConstructionCosts(value);
						break;
					case "FreeResearch":
						m_sandboxFeatures.SetFreeResearch(value);
						break;
					case "InfiniteFocus":
						m_sandboxFeatures.SetInfiniteFocus(value);
						break;
					
					// Settlement needs
					case "IgnoreMissingFood":
						m_sandboxFeatures.SetIgnoreMissingFood(value);
						break;
					case "IgnoreMissingWorkers":
						m_sandboxFeatures.SetIgnoreMissingWorkers(value);
						break;
					case "IgnoreMissingPower":
						m_sandboxFeatures.SetIgnoreMissingPower(value);
						break;
					case "IgnoreMissingComputing":
						m_sandboxFeatures.SetIgnoreMissingComputing(value);
						break;
					case "IgnoreMissingUnity":
						m_sandboxFeatures.SetIgnoreMissingUnity(value);
						break;
					case "UnlimitedUnity":
						m_sandboxFeatures.SetUnlimitedUnity(value);
						break;
					case "NoCleanWaterNeed":
						m_sandboxFeatures.SetNoCleanWaterNeed(value);
						break;
					case "NoWastewaterProduction":
						m_sandboxFeatures.SetNoWastewaterProduction(value);
						break;
					case "NoDiseaseEffects":
						m_sandboxFeatures.SetNoDiseaseEffects(value);
						break;
					
					// Environment cheats
					case "UnlimitedFood":
						m_sandboxFeatures.SetUnlimitedFood(value);
						break;
					case "InstantTreeGrowth":
						m_sandboxFeatures.SetInstantTreeGrowth(value);
						break;
					case "NoAirPollution":
						m_sandboxFeatures.SetNoAirPollution(value);
						break;
					case "NoWaterPollution":
						m_sandboxFeatures.SetNoWaterPollution(value);
						break;
					case "NoShipPollution":
						m_sandboxFeatures.SetNoShipPollution(value);
						break;
					case "NoTrainPollution":
						m_sandboxFeatures.SetNoTrainPollution(value);
						break;
					case "NoVehiclePollution":
						m_sandboxFeatures.SetNoVehiclePollution(value);
						break;
					case "NoBioWaste":
						m_sandboxFeatures.SetNoBioWaste(value);
						break;
					case "NoLandfillWaste":
						m_sandboxFeatures.SetNoLandfillWaste(value);
						break;
					case "NoToxicWaste":
						m_sandboxFeatures.SetNoToxicWaste(value);
						break;
					case "NoRadioactiveWaste":
						m_sandboxFeatures.SetNoRadioactiveWaste(value);
						break;
					
					// Terrain controls
					case "ProcessTerrainDesignations":
						m_sandboxFeatures.SetProcessTerrainDesignations(value);
						break;
					case "ProcessSurfaceDesignations":
						m_sandboxFeatures.SetProcessSurfaceDesignations(value);
						break;
					case "DisableTerrainPhysics":
						m_sandboxFeatures.SetDisableTerrainPhysics(value);
						break;
					case "UnlimitedMiningArea":
						m_sandboxFeatures.SetUnlimitedMiningArea(value);
						break;
					case "UnlimitedTowerArea":
						m_sandboxFeatures.SetUnlimitedTowerArea(value);
						break;
					
					// Logistics
					case "IgnoreFuelConsumption":
						m_sandboxFeatures.SetIgnoreFuelConsumption(value);
						break;
					case "UnlimitedVehicleFuel":
						m_sandboxFeatures.SetUnlimitedVehicleFuel(value);
						break;
					case "InstantCargoShips":
						m_sandboxFeatures.SetInstantCargoShips(value);
						break;
				}
			} catch (Exception ex) {
				Log.Warning("Failed to apply option " + key + ": " + ex.Message);
			}
		}

		private void ApplyFloatOption(string key, float value) {
			try {
				if (key.StartsWith("LocoSpeed_")) {
					string id = key.Substring("LocoSpeed_".Length);
					var loco = m_locoEditor.GetAllLocomotives().FirstOrDefault(l => l.Id.ToString() == id);
					if (loco != null) {
						m_locoEditor.SetMaxSpeedKmh(loco, value);
					}
				} else if (key.StartsWith("LocoPower_")) {
					string id = key.Substring("LocoPower_".Length);
					var loco = m_locoEditor.GetAllLocomotives().FirstOrDefault(l => l.Id.ToString() == id);
					if (loco != null) {
						m_locoEditor.SetFieldFloat(loco, "EnginePowerKw", value);
					}
				} else if (key.StartsWith("LocoTractive_")) {
					string id = key.Substring("LocoTractive_".Length);
					var loco = m_locoEditor.GetAllLocomotives().FirstOrDefault(l => l.Id.ToString() == id);
					if (loco != null) {
						m_locoEditor.SetFieldFloat(loco, "StartingTractiveEffort", value);
					}
				} else if (key.StartsWith("LocoBraking_")) {
					string id = key.Substring("LocoBraking_".Length);
					var loco = m_locoEditor.GetAllLocomotives().FirstOrDefault(l => l.Id.ToString() == id);
					if (loco != null) {
						m_locoEditor.SetFieldFloat(loco, "BrakingForceKn", value);
					}
				}
			} catch (Exception ex) {
				Log.Warning("Failed to apply float option " + key + ": " + ex.Message);
			}
		}
	}
}
