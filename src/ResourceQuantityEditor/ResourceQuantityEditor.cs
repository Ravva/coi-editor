using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Game;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
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
				optionsState);
			
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
