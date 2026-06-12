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
			depBuilder.RegisterDependency<LocomotiveEditorService>().AsSelf();
		}

		public void EarlyInit(DependencyResolver resolver) {
			try {
				_harmony = new Harmony("ResourceQuantityEditor");
				_harmony.PatchAll(Assembly.GetExecutingAssembly());
				
				UnlimitedDesignationsPatch.Initialize();

				// --- Патчи физики движения поездов ---
				// SPEED_MODULATION_SPACE_FACTOR: 1.4 → 0.8 (снижаем порог прощупывания)
				// RESERVE_EXTRA_FACTOR_MULT: 1.5 → 1.0 (убираем лишний множитель резервирования)
				PatchFix32StaticField("SPEED_MODULATION_SPACE_FACTOR", 0.8f);
				PatchFix32StaticField("RESERVE_EXTRA_FACTOR_MULT", 1.0f);
				
				Log.Info("ResourceQuantityEditor: All patches initialized in EarlyInit");
			} catch (Exception ex) {
				Log.Error("ResourceQuantityEditor: Failed to initialize patches: " + ex.Message);
			}
		}

		/// <summary>
		/// Записывает новое значение в статическое readonly Fix32-поле класса Train.
		/// </summary>
		private static void PatchFix32StaticField(string fieldName, float targetValue) {
			try {
				FieldInfo field = typeof(Train).GetField(
					fieldName,
					BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (field == null) {
					Log.Error("ResourceQuantityEditor: Train." + fieldName + " field not found");
					return;
				}

				int newRaw = (int)Math.Round(targetValue * 1024.0);

				object newFix32 = System.Runtime.Serialization.FormatterServices
					.GetUninitializedObject(field.FieldType);
				FieldInfo rawField = field.FieldType.GetField(
					"RawValue",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (rawField == null) {
					Log.Error("ResourceQuantityEditor: Fix32.RawValue field not found");
					return;
				}
				rawField.SetValue(newFix32, newRaw);

				System.Reflection.FieldAttributes attrs = field.Attributes;
				FieldInfo attrField = typeof(FieldInfo).GetField(
					"m_fieldAttributes",
					BindingFlags.NonPublic | BindingFlags.Instance);
				if (attrField != null) {
					attrField.SetValue(field, attrs & ~System.Reflection.FieldAttributes.InitOnly);
					field.SetValue(null, newFix32);
					attrField.SetValue(field, attrs);
				} else {
					field.SetValue(null, newFix32);
				}

				object readBack = field.GetValue(null);
				int readRawVal = rawField != null ? (int)rawField.GetValue(readBack) : -1;
				Log.Info(string.Format(
					"ResourceQuantityEditor: Train.{0} patched to {1:F4} (raw={2})",
					fieldName, (readRawVal / 1024.0), readRawVal));
			} catch (Exception ex) {
				Log.Error("ResourceQuantityEditor: Failed to patch Train." + fieldName + ": " + ex);
			}
		}

		public void Initialize(DependencyResolver resolver, bool gameWasLoaded) {
			SandboxFeatureService sandboxFeatures = resolver.Resolve<SandboxFeatureService>();
			OptionsStateService optionsState = resolver.Resolve<OptionsStateService>();
			LocomotiveEditorService locoEditor = resolver.Resolve<LocomotiveEditorService>();
			
			ResourceQuantityEditorUi.Install(
				resolver.Resolve<GlobalResourceEditorService>(),
				sandboxFeatures,
				resolver.Resolve<TreeRangeRemovalService>(),
				resolver.Resolve<UiContext>(),
				optionsState,
				locoEditor);
			
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

			// Update active trains once on load to propagate modified speed limits to existing trains
			try {
				locoEditor.UpdateActiveTrains();
				Log.Info("ResourceQuantityEditor: Successfully auto-updated active trains on mod initialization");
			} catch (Exception ex) {
				Log.Error("ResourceQuantityEditor: Failed to auto-update active trains: " + ex.Message);
			}
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

	public static class ModLoader {
		public static bool IsReloaded;
		public static System.Collections.Generic.Dictionary<string, object> ReloadedInstances;
	}
}
