using System;
using System.IO;
using System.Reflection;
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
			
			// Добавляем резолвер для поиска 0Harmony.dll в папке мода
			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
				string assemblyName = new AssemblyName(args.Name).Name;
				if (assemblyName == "0Harmony") {
					string assemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "0Harmony.dll");
					if (File.Exists(assemblyPath)) {
						return Assembly.LoadFrom(assemblyPath);
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
				// Инициализируем патчи как можно раньше
				InstantCargoShipsPatch.ApplyPatch();
				UnlimitedDesignationsPatch.Initialize();
				Log.Info("ResourceQuantityEditor: Patches initialized in EarlyInit");
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
