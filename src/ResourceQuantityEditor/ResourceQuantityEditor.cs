using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Game;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Core.Trains;
using Mafi.Unity.Ui;

namespace ResourceQuantityEditor {

	public sealed class ResourceQuantityEditor : IMod {

		private Harmony _harmony;
		private ResolveEventHandler _assemblyResolver;

		public ModManifest Manifest { get; private set; }
		public bool IsUiOnly { get { return false; } }
		public Option<IConfig> ModConfig { get; private set; }
		public ModJsonConfig JsonConfig { get; private set; }

		public ResourceQuantityEditor(ModManifest manifest) {
			Manifest = manifest;
			ModConfig = default(Option<IConfig>);
			JsonConfig = new ModJsonConfig(this);
			
			// Принудительно загружаем 0Harmony.dll из папки мода
			string modDir = manifest.RootDirectoryPath;
			string harmonyPath = Path.Combine(modDir, "0Harmony.dll");
			if (File.Exists(harmonyPath)) {
				try {
					Assembly.LoadFrom(harmonyPath);
					Log.Info("ResourceQuantityEditor: 0Harmony.dll loaded manually from " + harmonyPath);
				} catch (Exception ex) {
					Log.Error("ResourceQuantityEditor: Failed to load 0Harmony.dll manually: " + ex.Message);
				}
			}

			// Резолвер на случай, если автоматическая загрузка не сработает
			_assemblyResolver = (sender, args) => {
				string name = new AssemblyName(args.Name).Name;
				if (name == "0Harmony" || name.Contains("Harmony")) {
					if (File.Exists(harmonyPath)) {
						return Assembly.LoadFrom(harmonyPath);
					}
				}
				return null;
			};
			AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolver;
			
			Log.Info("ResourceQuantityEditor: constructed");
		}

		public void RegisterPrototypes(ProtoRegistrator registrator) {
		}

		public void RegisterDependencies(
			DependencyResolverBuilder depBuilder,
			ProtosDb protosDb,
			bool gameWasLoaded) {
			depBuilder.RegisterInstance(this, disposeOnResolverTermination: false).AsSelf();
			depBuilder.RegisterDependency<GlobalResourceEditorService>().AsSelf();
			depBuilder.RegisterDependency<SandboxFeatureService>().AsSelf();
			depBuilder.RegisterDependency<TreeRangeRemovalService>().AsSelf();
			depBuilder.RegisterDependency<VisualTweaksService>().AsSelf();
			depBuilder.RegisterDependency<OptionsStateService>().AsSelf();
		}

		public void EarlyInit(DependencyResolver resolver) {
			try {
				_harmony = new Harmony("ResourceQuantityEditor");
				_harmony.PatchAll(Assembly.GetExecutingAssembly());
				
				UnlimitedDesignationsPatch.Initialize();
				
				Log.Info("ResourceQuantityEditor: All patches initialized in EarlyInit");
			} catch (Exception ex) {
				Log.Error("ResourceQuantityEditor: Failed to initialize patches: " + ex.Message);
			}
		}



		public void Initialize(DependencyResolver resolver, bool gameWasLoaded) {
			SandboxFeatureService sandboxFeatures = resolver.Resolve<SandboxFeatureService>();
			OptionsStateService optionsState = resolver.Resolve<OptionsStateService>();
			
			ResourceQuantityEditorUi.Install(
				resolver.Resolve<GlobalResourceEditorService>(),
				sandboxFeatures,
				resolver.Resolve<TreeRangeRemovalService>(),
				resolver.Resolve<UiContext>(),
				optionsState,
				resolver);
			
			// Автоматически загружаем сохраненные опции при инициализации мода
			// Загружаем всегда, когда есть сохраненный файл (и при новой игре, и при загрузке)
			if (optionsState.HasSavedState()) {
				try {
					string result = optionsState.LoadSavedState();
					Log.Info("ResourceQuantityEditor: Auto-loading options - " + result);
				} catch (Exception ex) {
					Log.Warning("ResourceQuantityEditor: Failed to auto-load options: " + ex.Message);
				}
			} else {
				Log.Info("ResourceQuantityEditor: No saved options state found, using defaults.");
			}

			// Применяем 10-кратное увеличение мощности поездов в соответствии с сохраненными настройками
			TrainPowerBooster.UpdatePowerBoost(resolver, optionsState.IsTrainsPowerBoosted10x);
		}

		public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues) {
		}

		public void Dispose() {
			ResourceQuantityEditorUi.Uninstall();
			try {
				_harmony?.UnpatchAll("ResourceQuantityEditor");
			} catch { }
			try {
				UnlimitedDesignationsPatch.Unpatch();
			} catch { }
			if (_assemblyResolver != null) {
				AppDomain.CurrentDomain.AssemblyResolve -= _assemblyResolver;
			}
		}
	}

	public static class TrainPowerBooster {
		private static readonly System.Collections.Generic.Dictionary<string, (MechPower power, Fix32 tractive)> s_originalLocoValues =
			new System.Collections.Generic.Dictionary<string, (MechPower, Fix32)>();

		public static void UpdatePowerBoost(DependencyResolver resolver, bool enable) {
			try {
				ProtosDb protosDb = resolver.Resolve<ProtosDb>();
				
				// 1. Заполняем кэш оригинальных характеристик при первом запуске
				FieldInfo powerField = typeof(LocomotiveProto).GetField("EnginePowerKw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo tractiveField = typeof(LocomotiveProto).GetField("StartingTractiveEffort", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				foreach (LocomotiveProto loco in protosDb.Filter<LocomotiveProto>(_ => true)) {
					string id = loco.Id.ToString();
					if (!s_originalLocoValues.ContainsKey(id)) {
						s_originalLocoValues[id] = (loco.EnginePowerKw, loco.StartingTractiveEffort);
					}
				}

				// 2. Применяем настройки к прототипам в БД
				int protoCount = 0;
				foreach (LocomotiveProto loco in protosDb.Filter<LocomotiveProto>(_ => true)) {
					string id = loco.Id.ToString();
					if (s_originalLocoValues.TryGetValue(id, out var orig)) {
						if (powerField != null) {
							int origVal = GetMechPowerValue(orig.power);
							int targetPower = enable ? origVal * 10 : origVal;
							powerField.SetValue(loco, new MechPower(targetPower));
						}
						if (tractiveField != null) {
							Fix32 targetTractive = enable ? orig.tractive * 10 : orig.tractive;
							tractiveField.SetValue(loco, targetTractive);
						}
						protoCount++;
					}
				}
				Log.Info("TrainPowerBooster: Scaled " + protoCount + " locomotive prototypes. Boosted: " + enable);

				// 3. Масштабируем поезда на карте (если они уже созданы)
				TrainsManager trainsManager = null;
				try {
					trainsManager = resolver.Resolve<TrainsManager>();
				} catch {
					// Игнорируем на стартовом экране
				}

				if (trainsManager != null) {
					int activeCount = 0;
					FieldInfo tsdTotalPowerField = typeof(TrainStaticData).GetField("TotalPower", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					FieldInfo tsdTotalPowerFwdField = typeof(TrainStaticData).GetField("TotalPowerFwd", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					FieldInfo tsdTotalPowerBwdField = typeof(TrainStaticData).GetField("TotalPowerBwd", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					FieldInfo tsdStartingTractiveField = typeof(TrainStaticData).GetField("StartingTractiveEffort", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					MethodInfo recomputeSpeedsMethod = typeof(TrainStaticData).GetMethod("recomputeSpeeds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

					foreach (var train in trainsManager.Trains) {
						if (train == null || train.Data == null) continue;

						int origTotalPower = 0;
						Fix32 origTotalTractive = Fix32.Zero;

						if (train.Data.TrainCars != null) {
							foreach (var car in train.Data.TrainCars) {
								if (car is LocomotiveProto loco) {
									if (s_originalLocoValues.TryGetValue(loco.Id.ToString(), out var origLoco)) {
										origTotalPower += GetMechPowerValue(origLoco.power);
										origTotalTractive += origLoco.tractive;
									} else {
										origTotalPower += GetMechPowerValue(loco.EnginePowerKw);
										origTotalTractive += loco.StartingTractiveEffort;
									}
								}
							}
						}

						int targetPower = enable ? origTotalPower * 10 : origTotalPower;
						Fix32 targetTractive = enable ? origTotalTractive * 10 : origTotalTractive;

						if (tsdTotalPowerField != null) {
							tsdTotalPowerField.SetValue(train.Data, new MechPower(targetPower));
						}
						if (tsdTotalPowerFwdField != null) {
							tsdTotalPowerFwdField.SetValue(train.Data, new MechPower(targetPower));
						}
						if (tsdTotalPowerBwdField != null) {
							tsdTotalPowerBwdField.SetValue(train.Data, new MechPower(targetPower));
						}
						if (tsdStartingTractiveField != null) {
							tsdStartingTractiveField.SetValue(train.Data, targetTractive);
						}

						recomputeSpeedsMethod?.Invoke(train.Data, null);
						activeCount++;
					}
					Log.Info("TrainPowerBooster: Updated " + activeCount + " active trains on map. Boosted: " + enable);
				}
			} catch (Exception ex) {
				Log.Error("TrainPowerBooster: Failed to update power boost: " + ex);
			}
		}

		private static int GetMechPowerValue(MechPower power) {
			try {
				FieldInfo field = typeof(MechPower).GetField("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null) {
					return (int)field.GetValue(power);
				}
			} catch { }
			return 0;
		}
	}

	public static class ModLoader {
		public static bool IsReloaded;
		public static System.Collections.Generic.Dictionary<string, object> ReloadedInstances;
	}
}
