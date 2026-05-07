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
			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
				string name = new AssemblyName(args.Name).Name;
				if (name == "0Harmony" || name.Contains("Harmony")) {
					if (File.Exists(harmonyPath)) {
						return Assembly.LoadFrom(harmonyPath);
					}
				}
				return null;
			};
			
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
		}

		public void EarlyInit(DependencyResolver resolver) {
			try {
				// Инициализируем все Harmony патчи разом
				Harmony harmony = new Harmony("ResourceQuantityEditor");
				harmony.PatchAll(Assembly.GetExecutingAssembly());
				
				// Инициализируем остальные патчи (на базе Reflection)
				UnlimitedDesignationsPatch.Initialize();
				
				Log.Info("ResourceQuantityEditor: All patches initialized in EarlyInit");
			} catch (Exception ex) {
				Log.Error("ResourceQuantityEditor: Failed to initialize patches: " + ex.Message);
			}
		}

		public void Initialize(DependencyResolver resolver, bool gameWasLoaded) {
			ResourceQuantityEditorUi.Install(
				resolver.Resolve<GlobalResourceEditorService>(),
				resolver.Resolve<SandboxFeatureService>(),
				resolver.Resolve<TreeRangeRemovalService>(),
				resolver.Resolve<UiContext>());
		}

		public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues) {
		}

		public void Dispose() {
			ResourceQuantityEditorUi.Uninstall();
		}
	}
}
