using Mafi;
using Mafi.Core;
using Mafi.Core.Console;

namespace ResourceQuantityEditor {

	public sealed class StorageResourceConsoleCommands {
		private readonly ResourceQuantityEditor m_mod;
		private readonly StorageResourceEditorService m_editor;
		private readonly GlobalResourceEditorService m_globalEditor;
		private readonly SandboxFeatureService m_sandboxFeatures;

		public StorageResourceConsoleCommands(
			ResourceQuantityEditor mod,
			StorageResourceEditorService editor,
			GlobalResourceEditorService globalEditor,
			SandboxFeatureService sandboxFeatures) {
			m_mod = mod;
			m_editor = editor;
			m_globalEditor = globalEditor;
			m_sandboxFeatures = sandboxFeatures;
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Lists all storages known to Resource Quantity Editor.",
			customCommandName: "rqe_list_storages")]
		private string ListStorages() {
			return m_editor.ListStorages();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Lists storable product ids. Usage: rqe_list_products or rqe_list_products <filter>",
			customCommandName: "rqe_list_products")]
		private string ListProducts(string filter = "") {
			return m_editor.ListProducts(filter);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Lists globally available products. Usage: rqe_list_global or rqe_list_global <filter>",
			customCommandName: "rqe_list_global")]
		private string ListGlobal(string filter = "") {
			return m_globalEditor.ListGlobalProducts(filter);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Sets globally available product quantity. Usage: rqe_set_global <productId> <amount>",
			customCommandName: "rqe_set_global")]
		private string SetGlobal(string productId, int amount) {
			ValidateMaxAmount(amount);
			try {
				return m_globalEditor.SetGlobal(productId, amount);
			} catch (System.ArgumentException ex) {
				return ex.Message;
			}
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Adds a globally available product quantity. Usage: rqe_add_global <productId> <amount>",
			customCommandName: "rqe_add_global")]
		private string AddGlobal(string productId, int amount) {
			ValidateMaxAmount(amount);
			try {
				return m_globalEditor.AddGlobal(productId, amount);
			} catch (System.ArgumentException ex) {
				return ex.Message;
			}
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Removes a globally available product quantity. Usage: rqe_remove_global <productId> <amount>",
			customCommandName: "rqe_remove_global")]
		private string RemoveGlobal(string productId, int amount) {
			ValidateMaxAmount(amount);
			try {
				return m_globalEditor.RemoveGlobal(productId, amount);
			} catch (System.ArgumentException ex) {
				return ex.Message;
			}
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles the Resource Quantity Editor window.",
			customCommandName: "rqe_ui")]
		private string ToggleUi() {
			ResourceQuantityEditorUi.Toggle();
			return "Resource Quantity Editor UI toggled.";
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Shows sandbox feature status.",
			customCommandName: "rqe_sandbox_status")]
		private string SandboxStatus() {
			return m_sandboxFeatures.GetStatus();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Lists available weather ids. Usage: rqe_list_weather or rqe_list_weather <filter>",
			customCommandName: "rqe_list_weather")]
		private string ListWeather(string filter = "") {
			WeatherRow[] rows = m_sandboxFeatures.GetWeatherRows(filter);
			if (rows.Length == 0) {
				return "No weather found.";
			}
			return string.Join(System.Environment.NewLine, System.Array.ConvertAll(rows, x => x.Id + " | " + x.Name));
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Enables sandbox product source/sink buildings in a normal game.",
			customCommandName: "rqe_enable_sources_sinks")]
		private string EnableSourceSinks() {
			return m_sandboxFeatures.EnableSourceSinks();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Sets sandbox source/sink buildings. Usage: rqe_set_sources_sinks true",
			customCommandName: "rqe_set_sources_sinks")]
		private string SetSourceSinks(bool enabled) {
			return m_sandboxFeatures.SetSourceSinks(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Enables common sandbox cheats in a normal game.",
			customCommandName: "rqe_enable_sandbox_cheats")]
		private string EnableSandboxCheats() {
			return m_sandboxFeatures.EnableCommonCheats();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Disables common sandbox cheats.",
			customCommandName: "rqe_disable_sandbox_cheats")]
		private string DisableSandboxCheats() {
			return m_sandboxFeatures.DisableCommonCheats();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Sets instant build mode. Usage: rqe_set_insta_build true",
			customCommandName: "rqe_set_insta_build")]
		private string SetInstaBuild(bool enabled) {
			return m_sandboxFeatures.SetInstaBuild(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Sets instant construction. Usage: rqe_enable_instant_construction true",
			customCommandName: "rqe_enable_instant_construction")]
		private string SetInstantConstruction(bool enabled) {
			return m_sandboxFeatures.SetEnableInstantConstruction(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles missing maintenance requirements. Usage: rqe_ignore_missing_maintenance true",
			customCommandName: "rqe_ignore_missing_maintenance")]
		private string IgnoreMissingMaintenance(bool ignore) {
			return m_sandboxFeatures.SetIgnoreMissingMaintenance(ignore);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles fuel consumption. Usage: rqe_ignore_fuel_consumption true",
			customCommandName: "rqe_ignore_fuel_consumption")]
		private string IgnoreFuelConsumption(bool ignore) {
			return m_sandboxFeatures.SetIgnoreFuelConsumption(ignore);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles missing power requirements. Usage: rqe_ignore_missing_power true",
			customCommandName: "rqe_ignore_missing_power")]
		private string IgnoreMissingPower(bool ignore) {
			return m_sandboxFeatures.SetIgnoreMissingPower(ignore);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles missing computing requirements. Usage: rqe_ignore_missing_computing true",
			customCommandName: "rqe_ignore_missing_computing")]
		private string IgnoreMissingComputing(bool ignore) {
			return m_sandboxFeatures.SetIgnoreMissingComputing(ignore);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles missing unity requirements. Usage: rqe_ignore_missing_unity true",
			customCommandName: "rqe_ignore_missing_unity")]
		private string IgnoreMissingUnity(bool ignore) {
			return m_sandboxFeatures.SetIgnoreMissingUnity(ignore);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles missing food requirements. Usage: rqe_ignore_missing_food true",
			customCommandName: "rqe_ignore_missing_food")]
		private string IgnoreMissingFood(bool ignore) {
			return m_sandboxFeatures.SetIgnoreMissingFood(ignore);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles zero food consumption for all food prototypes. Usage: rqe_unlimited_food true",
			customCommandName: "rqe_unlimited_food")]
		private string UnlimitedFood(bool enabled) {
			return m_sandboxFeatures.SetUnlimitedFood(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles suppression of settlement bio waste. Usage: rqe_no_bio_waste true",
			customCommandName: "rqe_no_bio_waste")]
		private string NoBioWaste(bool enabled) {
			return m_sandboxFeatures.SetNoBioWaste(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles suppression of settlement landfill waste. Usage: rqe_no_landfill_waste true",
			customCommandName: "rqe_no_landfill_waste")]
		private string NoLandfillWaste(bool enabled) {
			return m_sandboxFeatures.SetNoLandfillWaste(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles suppression of toxic slurry waste. Usage: rqe_no_toxic_waste true",
			customCommandName: "rqe_no_toxic_waste")]
		private string NoToxicWaste(bool enabled) {
			return m_sandboxFeatures.SetNoToxicWaste(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles suppression of depleted uranium radioactive waste. Usage: rqe_no_radioactive_waste true",
			customCommandName: "rqe_no_radioactive_waste")]
		private string NoRadioactiveWaste(bool enabled) {
			return m_sandboxFeatures.SetNoRadioactiveWaste(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles unlimited unity reserve. Usage: rqe_unlimited_unity true",
			customCommandName: "rqe_unlimited_unity")]
		private string UnlimitedUnity(bool enabled) {
			return m_sandboxFeatures.SetUnlimitedUnity(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles unlimited vehicle fuel. Usage: rqe_unlimited_vehicle_fuel true",
			customCommandName: "rqe_unlimited_vehicle_fuel")]
		private string UnlimitedVehicleFuel(bool enabled) {
			return m_sandboxFeatures.SetUnlimitedVehicleFuel(enabled);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Unlocks all research.",
			customCommandName: "rqe_unlock_all_research")]
		private string UnlockAllResearch() {
			return m_sandboxFeatures.UnlockAllResearch();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Sets population. Usage: rqe_set_population <amount>",
			customCommandName: "rqe_set_population")]
		private string SetPopulation(int targetPopulation) {
			return m_sandboxFeatures.SetPopulation(targetPopulation);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Shows population and worker status.",
			customCommandName: "rqe_workers_status")]
		private string WorkersStatus() {
			return m_sandboxFeatures.GetWorkersStatus("Workers status.");
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Adds workers/population. Usage: rqe_add_workers <amount>",
			customCommandName: "rqe_add_workers")]
		private string AddWorkers(int workers) {
			return m_sandboxFeatures.AddWorkers(workers);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Removes population as much as possible. Usage: rqe_remove_population <amount>",
			customCommandName: "rqe_remove_population")]
		private string RemovePopulation(int population) {
			return m_sandboxFeatures.RemovePopulation(population);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles missing worker requirements. Usage: rqe_ignore_missing_workers true",
			customCommandName: "rqe_ignore_missing_workers")]
		private string IgnoreMissingWorkers(bool ignore) {
			return m_sandboxFeatures.SetIgnoreMissingWorkers(ignore);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Adds a repaired cargo ship.",
			customCommandName: "rqe_add_repaired_cargo_ship")]
		private string AddRepairedCargoShip() {
			return m_sandboxFeatures.AddRepairedCargoShip();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Removes selected trees.",
			customCommandName: "rqe_remove_selected_trees")]
		private string RemoveSelectedTrees() {
			return m_sandboxFeatures.RemoveSelectedTrees();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Locks weather. Usage: rqe_set_weather <weatherId>",
			customCommandName: "rqe_set_weather")]
		private string SetWeather(string weatherId) {
			return m_sandboxFeatures.SetWeather(weatherId);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Clears weather override.",
			customCommandName: "rqe_clear_weather")]
		private string ClearWeather() {
			return m_sandboxFeatures.ClearWeather();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Toggles sunny weather override.",
			customCommandName: "rqe_toggle_sunny_weather")]
		private string ToggleSunnyWeather() {
			return m_sandboxFeatures.ToggleSunnyWeather();
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Sets storage product quantity. Usage: rqe_set_storage <storageId> <productId> <amount>",
			customCommandName: "rqe_set_storage")]
		private string SetStorage(int storageId, string productId, int amount) {
			ValidateMaxAmount(amount);
			bool allowReplace = m_mod.JsonConfig.GetBool("allow_replace_non_empty_storage", defaultValue: false);
			try {
				return m_editor.SetStorage(new EntityId(storageId), productId, amount, allowReplace);
			} catch (System.ArgumentException ex) {
				return ex.Message;
			}
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Adds quantity to an already assigned storage product. Usage: rqe_add_storage <storageId> <amount>",
			customCommandName: "rqe_add_storage")]
		private string AddStorage(int storageId, int amount) {
			ValidateMaxAmount(amount);
			return m_editor.AddStorage(new EntityId(storageId), amount);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Removes quantity from a storage. Usage: rqe_remove_storage <storageId> <amount>",
			customCommandName: "rqe_remove_storage")]
		private string RemoveStorage(int storageId, int amount) {
			ValidateMaxAmount(amount);
			return m_editor.RemoveStorage(new EntityId(storageId), amount);
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Fills a storage to capacity. Usage: rqe_fill_storage <storageId>",
			customCommandName: "rqe_fill_storage")]
		private string FillStorage(int storageId) {
			return m_editor.FillStorage(new EntityId(storageId));
		}

		[ConsoleCommand(
			invokeOnMainThread: true,
			invokeDuringSync: false,
			documentation: "Empties a storage. Usage: rqe_empty_storage <storageId>",
			customCommandName: "rqe_empty_storage")]
		private string EmptyStorage(int storageId) {
			return m_editor.EmptyStorage(new EntityId(storageId));
		}

		private void ValidateMaxAmount(int amount) {
			int maxAmount = m_mod.JsonConfig.GetInt("max_single_operation_amount", defaultValue: 1000000);
			if (amount > maxAmount) {
				throw new System.ArgumentOutOfRangeException(
					"amount",
					amount,
					"Amount exceeds max_single_operation_amount.");
			}
		}
	}
}
