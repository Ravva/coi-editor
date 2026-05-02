using System;
using Mafi.Core.Products;
using Mafi.Localization;
using Mafi.Unity.Ui;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using UnityEngine;

namespace ResourceQuantityEditor {

	public sealed class ResourceQuantityEditorUi : MonoBehaviour {
		private static ResourceQuantityEditorUi s_instance;
		private static StorageResourceEditorService s_storageEditor;
		private static GlobalResourceEditorService s_globalEditor;
		private static SandboxFeatureService s_sandboxFeatures;
		private static TreeRangeRemovalService s_treeRangeRemoval;
		private static UiContext s_uiContext;
		private const string ICON_EMPTY = "Assets/Unity/UserInterface/General/Empty128.png";
		private const string ICON_POPULATION = "Assets/Unity/UserInterface/General/Population.svg";

		private Window m_window;
		private TextField m_statusField;
		private int m_tab;
		private int m_maintainFrame;
		private string m_productFilter = "";
		private bool m_productFilterWasEntered;
		private string m_weatherFilter = "";
		private bool m_weatherFilterWasEntered;
		private string m_selectedWeatherId = "";
		private string m_selectedWeatherName = "";
		private string m_selectedProductId = "";
		private string m_selectedProductName = "";
		private string m_resourceAmount = "100";
		private string m_populationAmount = "100";
		private string m_weatherSunIntensity = "100";
		private string m_weatherRainIntensity = "0";
		private string m_cargoShipsAmount = "2";
		private string m_vehicleLimitAmount = "100";
		private string m_status = "";

		public static void Install(
			StorageResourceEditorService storageEditor,
			GlobalResourceEditorService globalEditor,
			SandboxFeatureService sandboxFeatures,
			TreeRangeRemovalService treeRangeRemoval,
			UiContext uiContext) {
			s_storageEditor = storageEditor;
			s_globalEditor = globalEditor;
			s_sandboxFeatures = sandboxFeatures;
			s_treeRangeRemoval = treeRangeRemoval;
			s_uiContext = uiContext;
			if (s_instance != null) {
				return;
			}

			GameObject go = new GameObject("ResourceQuantityEditorUi");
			UnityEngine.Object.DontDestroyOnLoad(go);
			s_instance = go.AddComponent<ResourceQuantityEditorUi>();
		}

		public static void Uninstall() {
			if (s_instance != null) {
				s_instance.CloseWindow();
				UnityEngine.Object.Destroy(s_instance.gameObject);
				s_instance = null;
			}
		}

		public static void Toggle() {
			if (s_instance != null) {
				s_instance.ToggleWindow();
			}
		}

		private void Update() {
			if (Input.GetKeyDown(KeyCode.F8)) {
				ToggleWindow();
			}
			m_maintainFrame++;
			if (m_maintainFrame >= 60) {
				m_maintainFrame = 0;
				RunSafely(() => s_sandboxFeatures.MaintainUnlimitedOptions());
			}
			if (m_window != null && m_window.IsOpen) {
				m_window.InputUpdate();
			}
		}

		private void ToggleWindow() {
			if (m_window != null && m_window.IsOpen) {
				CloseWindow();
				return;
			}
			OpenWindow();
		}

		private void OpenWindow() {
			CloseWindow();
			m_window = BuildWindow();
			m_window.Open(s_uiContext.UiRoot);
		}

		private void CloseWindow() {
			if (m_window != null) {
				m_window.CloseNoFade();
				m_window = null;
				m_statusField = null;
			}
		}

		private Window BuildWindow() {
			Window window = new Window(L("COI Editor"), addFullscreenButton: false)
				.WindowSize(1120, 900)
				.MakeMovable()
				.ShortcutToShow("F8");

			TabContainer tabs = new TabContainer().ReducedPaddingBody();
			tabs.Add(L("Global"), ICON_EMPTY, BuildGlobalTab(), Scroll.No);
			tabs.Add(L("Settlement"), ICON_POPULATION, BuildSettlementTab(), Scroll.No);
			tabs.Add(L("Economy"), ICON_EMPTY, BuildEconomyTab(), Scroll.No);
			tabs.Add(L("Environment"), ICON_EMPTY, BuildEnvironmentTab(), Scroll.No);
			tabs.Add(L("Terrain"), ICON_EMPTY, BuildTerrainTab(), Scroll.No);
			tabs.Add(L("Weather"), ICON_EMPTY, BuildWeatherTab(), Scroll.No);
			tabs.Add(L("Logistics"), ICON_EMPTY, BuildLogisticsTab(), Scroll.No);
			if (m_tab > 6) {
				m_tab = 0;
			}
			tabs.SwitchToTab(m_tab);
			tabs.OnTabActivate(delegate {
				if (tabs.ActiveTabIndex.HasValue) {
					m_tab = tabs.ActiveTabIndex.Value;
				}
			});

			window.AddBodySingle(new UiComponent[] { tabs, BuildStatusRow() });
			return window;
		}

		private UiComponent BuildGlobalTab() {
			Column root = new Column(12);
			root.Add(BuildGlobalCommandDeck());
			root.Add(BuildGlobalProductsList());
			return root.FlexGrow(1);
		}

		private UiComponent BuildSandboxTab() {
			Column root = new Column(12);

			root.Add(BuildSandboxCommandDeck());
			root.Add(BuildSandboxCheatsDeck());
			root.Add(BuildSandboxPopulationDeck());
			root.Add(BuildSandboxWorldDeck());
			return root.FlexGrow(1);
		}

		private UiComponent BuildSettlementTab() {
			Column root = new Column(12);

			root.Add(BuildSandboxPopulationDeck());
			root.Add(BuildSettlementNeedsDeck());
			return root.FlexGrow(1);
		}

		private UiComponent BuildEconomyTab() {
			Column root = new Column(12);

			root.Add(BuildSandboxCommandDeck());
			root.Add(BuildEconomyCheatsDeck());
			root.Add(BuildResearchDeck());
			return root.FlexGrow(1);
		}

		private UiComponent BuildEnvironmentTab() {
			Column root = new Column(12);

			root.Add(BuildEnvironmentCheatsDeck());
			root.Add(BuildSandboxWorldDeck());
			return root.FlexGrow(1);
		}

		private UiComponent BuildWeatherTab() {
			Column root = new Column(12);

			root.Add(BuildWeatherControlsDeck());
			root.Add(BuildWeatherListDeck());
			return root.FlexGrow(1);
		}

		private UiComponent BuildTerrainTab() {
			Column root = new Column(12);

			root.Add(BuildTerrainControlsDeck());
			return root.FlexGrow(1);
		}

		private UiComponent BuildLogisticsTab() {
			Column root = new Column(12);

			root.Add(BuildLogisticsDeck());
			return root.FlexGrow(1);
		}

		private UiComponent BuildGlobalCommandDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("GLOBAL RESOURCE CONTROL"));
			panel.AttachTitleIcon(ICON_EMPTY);
			panel.BodyGap(10);

			Row selectedRow = CenteredRow();
			ProductProto selected = GetSelectedProduct();
			selectedRow.Add(selected != null
				? (UiComponent)new Icon(selected, noTooltip: true, noRightClick: true).Size(28)
				: new Icon(ICON_EMPTY).Size(28));
			selectedRow.Add(new Label(L(string.IsNullOrEmpty(m_selectedProductName) ? "No product selected" : m_selectedProductName)).IncFontSize().Width(360));
			selectedRow.Add(new Label(L("Amount")).UpperCase(false).Width(72));
			selectedRow.Add(new TextField()
				.Text(m_resourceAmount)
				.NumericOnly()
				.CharLimit(12)
				.OnValueChanged(x => m_resourceAmount = x, isDelayed: false)
				.Width(160));
			FinishCenteredRow(selectedRow);

			Row actionsRow = CenteredRow();
			actionsRow.Add(ActionButton("Set exact amount", RunGlobalSet, Button.Primary).Width(170));
			actionsRow.Add(ActionButton("Add amount", RunGlobalAdd, Button.General).Width(140));
			actionsRow.Add(ActionButton("Remove amount", RunGlobalRemove, Button.Warning).Width(165));
			actionsRow.Add(new Label(L("Search")).UpperCase(false).Width(56));
			TextField productFilterField = new TextField()
				.Text(m_productFilter)
				.OnValueChanged(delegate(string x) {
					if (!string.IsNullOrEmpty(x)) {
						m_productFilterWasEntered = true;
					}
					m_productFilter = x;
					RefreshWindow();
				}, isDelayed: true)
				.Width(260);
			if (!m_productFilterWasEntered) {
				productFilterField.Placeholder(L("id or name"));
			}
			actionsRow.Add(productFilterField);
			FinishCenteredRow(actionsRow);

			panel.BodyAdd(new UiComponent[] { selectedRow, actionsRow });
			return panel;
		}

		private UiComponent BuildGlobalProductsList() {
			PanelWithHeader products = new PanelWithHeader(L("GLOBAL INVENTORY"));
			products.AttachTitleIcon(ICON_EMPTY);

			Column list = new Column(8);
			list.Add(BuildItemHeader(includeQuantity: true));
			foreach (GlobalProductRow row in s_globalEditor.GetGlobalProducts(m_productFilter)) {
				list.Add(BuildGlobalRow(row));
			}

			ScrollColumn scroll = new ScrollColumn();
			scroll.ScrollerAlwaysVisible();
			scroll.FlexGrow(1);
			scroll.Add(list);
			products.BodyAdd(new UiComponent[] { scroll });
			return products.FlexGrow(1);
		}

		private UiComponent BuildSandboxCommandDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("COI EDITOR"));
			panel.AttachTitleIcon(ICON_EMPTY);

			Row primary = CenteredRow();
			primary.Add(ActionButton("Enable common cheats", () => RunSandboxCommand(() => s_sandboxFeatures.EnableCommonCheats()), Button.Primary).Width(210));
			primary.Add(ActionButton("Disable common cheats", () => RunSandboxCommand(() => s_sandboxFeatures.DisableCommonCheats()), Button.General).Width(215));
			primary.Add(ActionButton("Unlock all research", () => RunSandboxCommand(() => s_sandboxFeatures.UnlockAllResearch()), Button.General).Width(190));
			primary.Add(ActionButton("Add repaired cargo ship", () => RunSandboxCommand(() => s_sandboxFeatures.AddRepairedCargoShip()), Button.General).Width(220));
			primary.Add(ActionButton("Refresh status", () => RunSandboxCommand(() => s_sandboxFeatures.GetStatus()), Button.General).Width(150));
			FinishCenteredRow(primary);

			panel.BodyAdd(new UiComponent[] { primary });
			return panel;
		}

		private UiComponent BuildSettlementNeedsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("SETTLEMENT NEEDS"));
			panel.AttachTitleIcon(ICON_POPULATION);
			panel.BodyGap(8);

			Row togglesA = CenteredRow();
			togglesA.Add(SandboxToggle("No food need", s_sandboxFeatures.IgnoreMissingFood, () => s_sandboxFeatures.SetIgnoreMissingFood(!s_sandboxFeatures.IgnoreMissingFood)).Width(190));
			togglesA.Add(SandboxToggle("No workers need", s_sandboxFeatures.IgnoreMissingWorkers, () => s_sandboxFeatures.SetIgnoreMissingWorkers(!s_sandboxFeatures.IgnoreMissingWorkers)).Width(205));
			togglesA.Add(SandboxToggle("No power need", s_sandboxFeatures.IgnoreMissingPower, () => s_sandboxFeatures.SetIgnoreMissingPower(!s_sandboxFeatures.IgnoreMissingPower)).Width(190));
			FinishCenteredRow(togglesA);

			Row togglesB = CenteredRow();
			togglesB.Add(SandboxToggle("No computing need", s_sandboxFeatures.IgnoreMissingComputing, () => s_sandboxFeatures.SetIgnoreMissingComputing(!s_sandboxFeatures.IgnoreMissingComputing)).Width(220));
			togglesB.Add(SandboxToggle("No unity need", s_sandboxFeatures.IgnoreMissingUnity, () => s_sandboxFeatures.SetIgnoreMissingUnity(!s_sandboxFeatures.IgnoreMissingUnity)).Width(190));
			togglesB.Add(SandboxToggle("Unlimited unity", s_sandboxFeatures.UnlimitedUnity, () => s_sandboxFeatures.SetUnlimitedUnity(!s_sandboxFeatures.UnlimitedUnity)).Width(190));
			FinishCenteredRow(togglesB);

			panel.BodyAdd(new UiComponent[] { togglesA, togglesB });
			return panel;
		}

		private UiComponent BuildEconomyCheatsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("ECONOMY CHEATS"));
			panel.AttachTitleIcon(ICON_EMPTY);
			panel.BodyGap(8);

			Row togglesA = CenteredRow();
			togglesA.Add(SandboxToggle("Instant build", s_sandboxFeatures.IsInstaBuildEnabled, () => s_sandboxFeatures.SetInstaBuild(!s_sandboxFeatures.IsInstaBuildEnabled)).Width(175));
			togglesA.Add(SandboxToggle("Instant construction", s_sandboxFeatures.EnableInstantConstruction, () => s_sandboxFeatures.SetEnableInstantConstruction(!s_sandboxFeatures.EnableInstantConstruction)).Width(225));
			togglesA.Add(SandboxToggle("Source/sink buildings", s_sandboxFeatures.AreSourcesAndSinksAllowed, () => s_sandboxFeatures.SetSourceSinks(!s_sandboxFeatures.AreSourcesAndSinksAllowed)).Width(230));
			FinishCenteredRow(togglesA);

			Row togglesB = CenteredRow();
			togglesB.Add(SandboxToggle("No maintenance", s_sandboxFeatures.IgnoreMissingMaintenance, () => s_sandboxFeatures.SetIgnoreMissingMaintenance(!s_sandboxFeatures.IgnoreMissingMaintenance)).Width(190));
			togglesB.Add(SandboxToggle("No fuel consumption", s_sandboxFeatures.IgnoreFuelConsumption, () => s_sandboxFeatures.SetIgnoreFuelConsumption(!s_sandboxFeatures.IgnoreFuelConsumption)).Width(220));
			togglesB.Add(SandboxToggle("Unlimited vehicle fuel", s_sandboxFeatures.UnlimitedVehicleFuel, () => s_sandboxFeatures.SetUnlimitedVehicleFuel(!s_sandboxFeatures.UnlimitedVehicleFuel)).Width(230));
			FinishCenteredRow(togglesB);

			Row togglesC = CenteredRow();
			togglesC.Add(SandboxToggle("No construction costs", s_sandboxFeatures.NoConstructionCosts, () => s_sandboxFeatures.SetNoConstructionCosts(!s_sandboxFeatures.NoConstructionCosts)).Width(230));
			togglesC.Add(SandboxToggle("Free research", s_sandboxFeatures.FreeResearch, () => s_sandboxFeatures.SetFreeResearch(!s_sandboxFeatures.FreeResearch)).Width(180));
			togglesC.Add(SandboxToggle("Infinite focus", s_sandboxFeatures.InfiniteFocus, () => s_sandboxFeatures.SetInfiniteFocus(!s_sandboxFeatures.InfiniteFocus)).Width(180));
			FinishCenteredRow(togglesC);

			panel.BodyAdd(new UiComponent[] { togglesA, togglesB, togglesC });
			return panel;
		}

		private UiComponent BuildResearchDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("RESEARCH"));
			panel.AttachTitleIcon(ICON_EMPTY);

			Row row = CenteredRow();
			row.Add(new Label(L("All research: " + (s_sandboxFeatures.IsAllResearchUnlocked ? "Unlocked" : "Locked"))).Width(260));
			row.Add(ActionButton("Finish all research", () => RunSandboxCommand(() => s_sandboxFeatures.UnlockAllResearch()), Button.Primary).Width(220));
			row.Add(ActionButton("Finish current", () => RunSandboxCommand(() => s_sandboxFeatures.FinishCurrentResearch()), Button.General).Width(180));
			row.Add(ActionButton("Finish repeatable", () => RunSandboxCommand(() => s_sandboxFeatures.FinishRepeatableResearch()), Button.General).Width(190));
			FinishCenteredRow(row);

			panel.BodyAdd(new UiComponent[] { row });
			return panel;
		}

		private UiComponent BuildEnvironmentCheatsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("ENVIRONMENT CHEATS"));
			panel.AttachTitleIcon(ICON_EMPTY);
			panel.BodyGap(8);

			Row togglesA = CenteredRow();
			togglesA.Add(SandboxToggle("Disable food consumption", s_sandboxFeatures.UnlimitedFood, () => s_sandboxFeatures.SetUnlimitedFood(!s_sandboxFeatures.UnlimitedFood)).Width(260));
			togglesA.Add(SandboxToggle("No bio waste", s_sandboxFeatures.NoBioWaste, () => s_sandboxFeatures.SetNoBioWaste(!s_sandboxFeatures.NoBioWaste)).Width(180));
			togglesA.Add(SandboxToggle("No landfill waste", s_sandboxFeatures.NoLandfillWaste, () => s_sandboxFeatures.SetNoLandfillWaste(!s_sandboxFeatures.NoLandfillWaste)).Width(210));
			FinishCenteredRow(togglesA);

			Row togglesB = CenteredRow();
			togglesB.Add(SandboxToggle("No toxic slurry waste", s_sandboxFeatures.NoToxicWaste, () => s_sandboxFeatures.SetNoToxicWaste(!s_sandboxFeatures.NoToxicWaste)).Width(240));
			togglesB.Add(SandboxToggle("No depleted uranium waste", s_sandboxFeatures.NoRadioactiveWaste, () => s_sandboxFeatures.SetNoRadioactiveWaste(!s_sandboxFeatures.NoRadioactiveWaste)).Width(260));
			togglesB.Add(SandboxToggle("Instant tree growth", s_sandboxFeatures.InstantTreeGrowth, () => s_sandboxFeatures.SetInstantTreeGrowth(!s_sandboxFeatures.InstantTreeGrowth)).Width(220));
			FinishCenteredRow(togglesB);

			Row pollutionA = CenteredRow();
			pollutionA.Add(SandboxToggle("No air pollution", s_sandboxFeatures.NoAirPollution, () => s_sandboxFeatures.SetNoAirPollution(!s_sandboxFeatures.NoAirPollution)).Width(200));
			pollutionA.Add(SandboxToggle("No water pollution", s_sandboxFeatures.NoWaterPollution, () => s_sandboxFeatures.SetNoWaterPollution(!s_sandboxFeatures.NoWaterPollution)).Width(220));
			pollutionA.Add(SandboxToggle("No ship pollution", s_sandboxFeatures.NoShipPollution, () => s_sandboxFeatures.SetNoShipPollution(!s_sandboxFeatures.NoShipPollution)).Width(200));
			FinishCenteredRow(pollutionA);

			Row pollutionB = CenteredRow();
			pollutionB.Add(SandboxToggle("No train pollution", s_sandboxFeatures.NoTrainPollution, () => s_sandboxFeatures.SetNoTrainPollution(!s_sandboxFeatures.NoTrainPollution)).Width(210));
			pollutionB.Add(SandboxToggle("No vehicle pollution", s_sandboxFeatures.NoVehiclePollution, () => s_sandboxFeatures.SetNoVehiclePollution(!s_sandboxFeatures.NoVehiclePollution)).Width(220));
			FinishCenteredRow(pollutionB);

			panel.BodyAdd(new UiComponent[] { togglesA, togglesB, pollutionA, pollutionB });
			return panel;
		}

		private UiComponent BuildWeatherControlsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("WEATHER CONTROLS"));
			panel.AttachTitleIcon(ICON_EMPTY);
			panel.BodyGap(8);

			Row current = CenteredRow();
			current.Add(new Label(L("Current weather")).UpperCase(false).Width(130));
			current.Add(new Label(L(s_sandboxFeatures.CurrentWeatherName)).Width(260));
			current.Add(SandboxToggle("Sunny weather", s_sandboxFeatures.IsSunnyWeatherEnabled, () => s_sandboxFeatures.ToggleSunnyWeather()).Width(180));
			current.Add(ActionButton("Reset weather", () => RunSandboxCommand(() => s_sandboxFeatures.ClearWeather()), Button.General).Width(170));
			FinishCenteredRow(current);

			Row selected = CenteredRow();
			selected.Add(new Label(L("Selected")).UpperCase(false).Width(130));
			selected.Add(new Label(L(string.IsNullOrEmpty(m_selectedWeatherName) ? "No weather selected" : m_selectedWeatherName)).Width(260));
			selected.Add(ActionButton("Apply selected", RunSelectedWeather, Button.Primary).Width(180));
			FinishCenteredRow(selected);

			Row filter = CenteredRow();
			filter.Add(new Label(L("Search")).UpperCase(false).Width(130));
			TextField weatherFilterField = new TextField()
				.Text(m_weatherFilter)
				.OnValueChanged(delegate(string x) {
					if (!string.IsNullOrEmpty(x)) {
						m_weatherFilterWasEntered = true;
					}
					m_weatherFilter = x;
					RefreshWindow();
				}, isDelayed: true)
				.Width(260);
			if (!m_weatherFilterWasEntered) {
				weatherFilterField.Placeholder(L("id or name"));
			}
			filter.Add(weatherFilterField);
			FinishCenteredRow(filter);

			Row intensity = CenteredRow();
			intensity.Add(new Label(L("Sun intensity")).UpperCase(false).Width(130));
			intensity.Add(new TextField()
				.Text(m_weatherSunIntensity)
				.NumericOnly()
				.CharLimit(5)
				.OnValueChanged(x => m_weatherSunIntensity = x, isDelayed: false)
				.Width(90));
			intensity.Add(new Label(L("Rain intensity")).UpperCase(false).Width(130));
			intensity.Add(new TextField()
				.Text(m_weatherRainIntensity)
				.NumericOnly()
				.CharLimit(5)
				.OnValueChanged(x => m_weatherRainIntensity = x, isDelayed: false)
				.Width(90));
			intensity.Add(ActionButton("Apply intensity", RunWeatherIntensity, Button.General).Width(170));
			FinishCenteredRow(intensity);

			panel.BodyAdd(new UiComponent[] { current, selected, filter, intensity });
			return panel;
		}

		private UiComponent BuildTerrainControlsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("TERRAIN CONTROLS"));
			panel.AttachTitleIcon(ICON_EMPTY);
			panel.BodyGap(8);

			Row rowA = CenteredRow();
			rowA.Add(SandboxToggle("Process all mining designations", s_sandboxFeatures.ProcessTerrainDesignations, () => s_sandboxFeatures.SetProcessTerrainDesignations(!s_sandboxFeatures.ProcessTerrainDesignations)).Width(280));
			rowA.Add(SandboxToggle("Process all surface designations", s_sandboxFeatures.ProcessSurfaceDesignations, () => s_sandboxFeatures.SetProcessSurfaceDesignations(!s_sandboxFeatures.ProcessSurfaceDesignations)).Width(280));
			FinishCenteredRow(rowA);

			Row rowB = CenteredRow();
			rowB.Add(ActionButton("Clear all terrain designations", () => RunSandboxCommand(() => s_sandboxFeatures.ClearAllTerrainDesignations()), Button.Warning).Width(260));
			rowB.Add(SandboxToggle("Disable terrain physics", s_sandboxFeatures.DisableTerrainPhysics, () => s_sandboxFeatures.SetDisableTerrainPhysics(!s_sandboxFeatures.DisableTerrainPhysics)).Width(230));
			FinishCenteredRow(rowB);

			panel.BodyAdd(new UiComponent[] { rowA, rowB });
			return panel;
		}

		private UiComponent BuildWeatherListDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("WEATHER LIST"));
			panel.AttachTitleIcon(ICON_EMPTY);

			Column list = new Column(8);
			foreach (WeatherRow row in s_sandboxFeatures.GetWeatherRows(m_weatherFilter)) {
				list.Add(BuildWeatherRow(row));
			}

			ScrollColumn scroll = new ScrollColumn();
			scroll.ScrollerAlwaysVisible();
			scroll.FlexGrow(1);
			scroll.Add(list);
			panel.BodyAdd(new UiComponent[] { scroll });
			return panel.FlexGrow(1);
		}

		private UiComponent BuildWeatherRow(WeatherRow weather) {
			Row row = CenteredRow();
			row.Add(new Label(L(weather.Id)).TinyFontSize().Width(260));
			row.Add(new ButtonText(Button.Area, L(weather.Name), delegate {
				m_selectedWeatherId = weather.Id;
				m_selectedWeatherName = weather.Name;
				RefreshWindow();
			}).Width(360));
			FinishCenteredRow(row);
			return row;
		}

		private UiComponent BuildLogisticsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("EXPLORATION & LOGISTICS"));
			panel.AttachTitleIcon(ICON_EMPTY);
			panel.BodyGap(8);

			Row actions = CenteredRow();
			actions.Add(new Label(L("Cargo ships")).UpperCase(false).Width(110));
			actions.Add(new TextField()
				.Text(m_cargoShipsAmount)
				.NumericOnly()
				.CharLimit(5)
				.OnValueChanged(x => m_cargoShipsAmount = x, isDelayed: false)
				.Width(80));
			actions.Add(ActionButton("Add repaired ships", RunAddCargoShips, Button.Primary).Width(190));
			actions.Add(SandboxToggle("No fuel consumption", s_sandboxFeatures.IgnoreFuelConsumption, () => s_sandboxFeatures.SetIgnoreFuelConsumption(!s_sandboxFeatures.IgnoreFuelConsumption)).Width(220));
			actions.Add(SandboxToggle("Unlimited vehicle fuel", s_sandboxFeatures.UnlimitedVehicleFuel, () => s_sandboxFeatures.SetUnlimitedVehicleFuel(!s_sandboxFeatures.UnlimitedVehicleFuel)).Width(230));
			FinishCenteredRow(actions);

			Row world = CenteredRow();
			world.Add(ActionButton("Reveal all locations", () => RunSandboxCommand(() => s_sandboxFeatures.RevealAllLocations()), Button.General).Width(200));
			world.Add(ActionButton("Scan all locations", () => RunSandboxCommand(() => s_sandboxFeatures.ScanAllLocations()), Button.General).Width(190));
			world.Add(ActionButton("Visit all locations", () => RunSandboxCommand(() => s_sandboxFeatures.VisitAllLocations()), Button.General).Width(190));
			world.Add(ActionButton("Defeat all enemies", () => RunSandboxCommand(() => s_sandboxFeatures.DefeatAllEnemies()), Button.Warning).Width(190));
			FinishCenteredRow(world);

			Row vehicles = CenteredRow();
			vehicles.Add(new Label(L("Vehicle limit")).UpperCase(false).Width(110));
			vehicles.Add(new TextField()
				.Text(m_vehicleLimitAmount)
				.NumericOnly()
				.CharLimit(6)
				.OnValueChanged(x => m_vehicleLimitAmount = x, isDelayed: false)
				.Width(90));
			vehicles.Add(ActionButton("Increase limit", RunIncreaseVehicleLimit, Button.General).Width(170));
			FinishCenteredRow(vehicles);

			panel.BodyAdd(new UiComponent[] { actions, world, vehicles });
			return panel;
		}

		private UiComponent BuildSandboxCheatsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("SANDBOX MODIFIERS"));
			panel.AttachTitleIcon(ICON_EMPTY);
			panel.BodyGap(8);

			Row togglesA = CenteredRow();
			togglesA.Add(SandboxToggle("Instant build mode", s_sandboxFeatures.IsInstaBuildEnabled, () => s_sandboxFeatures.SetInstaBuild(!s_sandboxFeatures.IsInstaBuildEnabled)).Width(220));
			togglesA.Add(SandboxToggle("Instant construction", s_sandboxFeatures.EnableInstantConstruction, () => s_sandboxFeatures.SetEnableInstantConstruction(!s_sandboxFeatures.EnableInstantConstruction)).Width(230));
			togglesA.Add(SandboxToggle("Source/sink buildings", s_sandboxFeatures.AreSourcesAndSinksAllowed, () => s_sandboxFeatures.SetSourceSinks(!s_sandboxFeatures.AreSourcesAndSinksAllowed)).Width(230));
			togglesA.Add(SandboxToggle("Ignore missing workers", s_sandboxFeatures.IgnoreMissingWorkers, () => s_sandboxFeatures.SetIgnoreMissingWorkers(!s_sandboxFeatures.IgnoreMissingWorkers)).Width(230));
			FinishCenteredRow(togglesA);

			Row togglesB = CenteredRow();
			togglesB.Add(SandboxToggle("Ignore missing power", s_sandboxFeatures.IgnoreMissingPower, () => s_sandboxFeatures.SetIgnoreMissingPower(!s_sandboxFeatures.IgnoreMissingPower)).Width(220));
			togglesB.Add(SandboxToggle("Ignore missing computing", s_sandboxFeatures.IgnoreMissingComputing, () => s_sandboxFeatures.SetIgnoreMissingComputing(!s_sandboxFeatures.IgnoreMissingComputing)).Width(245));
			togglesB.Add(SandboxToggle("Ignore missing unity", s_sandboxFeatures.IgnoreMissingUnity, () => s_sandboxFeatures.SetIgnoreMissingUnity(!s_sandboxFeatures.IgnoreMissingUnity)).Width(220));
			togglesB.Add(SandboxToggle("Ignore missing food", s_sandboxFeatures.IgnoreMissingFood, () => s_sandboxFeatures.SetIgnoreMissingFood(!s_sandboxFeatures.IgnoreMissingFood)).Width(210));
			FinishCenteredRow(togglesB);

			Row togglesC = CenteredRow();
			togglesC.Add(SandboxToggle("Ignore fuel consumption", s_sandboxFeatures.IgnoreFuelConsumption, () => s_sandboxFeatures.SetIgnoreFuelConsumption(!s_sandboxFeatures.IgnoreFuelConsumption)).Width(240));
			togglesC.Add(SandboxToggle("Ignore missing maintenance", s_sandboxFeatures.IgnoreMissingMaintenance, () => s_sandboxFeatures.SetIgnoreMissingMaintenance(!s_sandboxFeatures.IgnoreMissingMaintenance)).Width(260));
			FinishCenteredRow(togglesC);

			Row togglesD = CenteredRow();
			togglesD.Add(SandboxToggle("Disable food consumption", s_sandboxFeatures.UnlimitedFood, () => s_sandboxFeatures.SetUnlimitedFood(!s_sandboxFeatures.UnlimitedFood)).Width(260));
			togglesD.Add(SandboxToggle("No bio waste", s_sandboxFeatures.NoBioWaste, () => s_sandboxFeatures.SetNoBioWaste(!s_sandboxFeatures.NoBioWaste)).Width(180));
			togglesD.Add(SandboxToggle("No landfill waste", s_sandboxFeatures.NoLandfillWaste, () => s_sandboxFeatures.SetNoLandfillWaste(!s_sandboxFeatures.NoLandfillWaste)).Width(210));
			togglesD.Add(SandboxToggle("No toxic slurry waste", s_sandboxFeatures.NoToxicWaste, () => s_sandboxFeatures.SetNoToxicWaste(!s_sandboxFeatures.NoToxicWaste)).Width(240));
			FinishCenteredRow(togglesD);

			Row togglesE = CenteredRow();
			togglesE.Add(SandboxToggle("No depleted uranium waste", s_sandboxFeatures.NoRadioactiveWaste, () => s_sandboxFeatures.SetNoRadioactiveWaste(!s_sandboxFeatures.NoRadioactiveWaste)).Width(260));
			togglesE.Add(SandboxToggle("Unlimited unity reserve", s_sandboxFeatures.UnlimitedUnity, () => s_sandboxFeatures.SetUnlimitedUnity(!s_sandboxFeatures.UnlimitedUnity)).Width(250));
			togglesE.Add(SandboxToggle("Unlimited vehicle fuel", s_sandboxFeatures.UnlimitedVehicleFuel, () => s_sandboxFeatures.SetUnlimitedVehicleFuel(!s_sandboxFeatures.UnlimitedVehicleFuel)).Width(250));
			FinishCenteredRow(togglesE);

			panel.BodyAdd(new UiComponent[] { togglesA, togglesB, togglesC, togglesD, togglesE });
			return panel;
		}

		private UiComponent BuildSandboxPopulationDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("POPULATION"));
			panel.AttachTitleIcon(ICON_POPULATION);

			Row people = CenteredRow();
			people.Add(new Label(L("Amount")).UpperCase(false).Width(72));
			people.Add(new TextField()
				.Text(m_populationAmount)
				.NumericOnly()
				.CharLimit(12)
				.OnValueChanged(x => m_populationAmount = x, isDelayed: false)
				.Width(120));
			people.Add(ActionButton("Set total population", () => RunPopulationAction("Population must be a number.", amount => SetStatus(s_sandboxFeatures.SetPopulation(amount))), Button.Primary).Width(200));
			people.Add(ActionButton("Add workers", () => RunPopulationAction("Workers amount must be a number.", amount => SetStatus(s_sandboxFeatures.AddWorkers(amount))), Button.General).Width(140));
			people.Add(ActionButton("Remove population", () => RunPopulationAction("Population must be a number.", amount => SetStatus(s_sandboxFeatures.RemovePopulation(amount))), Button.Warning).Width(190));
			FinishCenteredRow(people);

			panel.BodyAdd(new UiComponent[] { people });
			return panel;
		}

		private UiComponent BuildSandboxWorldDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("WORLD"));
			panel.AttachTitleIcon(ICON_EMPTY);

			Row actions = CenteredRow();
			actions.Add(ActionButton("Remove selected trees", StartTreeRangeRemoval, Button.Warning).Width(210));
			actions.Add(SandboxToggle("Sunny weather", s_sandboxFeatures.IsSunnyWeatherEnabled, () => s_sandboxFeatures.ToggleSunnyWeather()).Width(190));
			FinishCenteredRow(actions);

			panel.BodyAdd(new UiComponent[] { actions });
			return panel;
		}

		private void StartTreeRangeRemoval() {
			CloseWindow();
			RunSafely(() => s_treeRangeRemoval.BeginRemoveSelection());
		}

		private Row CenteredRow() {
			Row row = new Row(12);
			row.Add(new Label(L("")).FlexGrow(1));
			return row;
		}

		private void FinishCenteredRow(Row row) {
			row.Add(new Label(L("")).FlexGrow(1));
		}

		private ButtonText SandboxToggle(string title, bool enabled, Func<string> action) {
			return ActionButton(title, () => RunSandboxCommand(action), enabled ? Button.Primary : Button.General);
		}

		private UiComponent BuildItemHeader(bool includeQuantity) {
			Row row = CenteredRow();
			row.Add(new Label(L("")).Width(28));
			row.Add(new Label(L("Item ID")).TinyFontSize().Width(190));
			row.Add(new Label(L("Item name")).TinyFontSize().Width(360));
			if (includeQuantity) {
				row.Add(new Label(L("Quantity")).TinyFontSize().Width(120));
			}
			FinishCenteredRow(row);
			return row;
		}

		private UiComponent BuildGlobalRow(GlobalProductRow globalRow) {
			Row row = CenteredRow();
			row.Add(new Icon(globalRow.Product, noTooltip: true, noRightClick: true).Size(24));
			row.Add(new Label(L(GetProductId(globalRow.Product))).TinyFontSize().Width(190));
			row.Add(new ButtonText(Button.Area, L(GetProductName(globalRow.Product)), delegate {
				SelectProduct(globalRow.Product);
				RefreshWindow();
			}).Width(360));
			row.Add(new Label(L(globalRow.Amount.ToString())).Width(120));
			FinishCenteredRow(row);
			return row;
		}

		private UiComponent BuildStatusRow() {
			PanelWithHeader status = new PanelWithHeader(L("STATUS"));
			status.AttachTitleIcon(ICON_EMPTY);
			m_statusField = new TextField()
				.Text(string.IsNullOrEmpty(m_status) ? "Ready." : m_status)
				.Readonly(true);
			status.BodyAdd(new UiComponent[] { m_statusField });
			return status;
		}

		private ButtonText ActionButton(string text, Action action, ButtonVariant variant) {
			return new ButtonText(variant, L(text), () => RunSafely(action));
		}

		private ProductProto GetSelectedProduct() {
			if (string.IsNullOrEmpty(m_selectedProductId)) {
				return null;
			}
			ProductProto[] products = s_storageEditor.GetProducts(m_selectedProductId);
			for (int i = 0; i < products.Length; i++) {
				if (products[i].Id.ToString() == m_selectedProductId) {
					return products[i];
				}
			}
			return null;
		}

		private void SelectProduct(ProductProto product) {
			m_selectedProductId = GetProductId(product);
			m_selectedProductName = GetProductName(product);
		}

		private static string GetProductId(ProductProto product) {
			return product.Id.ToString();
		}

		private static string GetProductName(ProductProto product) {
			return product.Strings.Name.ToString();
		}

		private void RunPopulationAction(string errorMessage, Action<int> action) {
			int amount;
			if (!int.TryParse(m_populationAmount, out amount)) {
				SetStatus(errorMessage);
				return;
			}
			action(amount);
			RefreshWindow();
		}

		private void RunSandboxCommand(Func<string> action) {
			SetStatus(action());
			RefreshWindow();
		}

		private void RunSelectedWeather() {
			if (string.IsNullOrEmpty(m_selectedWeatherId)) {
				SetStatus("Select weather first.");
				return;
			}
			RunSandboxCommand(() => s_sandboxFeatures.SetWeather(m_selectedWeatherId));
		}

		private void RunWeatherIntensity() {
			int sun;
			int rain;
			if (!int.TryParse(m_weatherSunIntensity, out sun) || !int.TryParse(m_weatherRainIntensity, out rain)) {
				SetStatus("Weather intensity must be a number.");
				return;
			}
			RunSandboxCommand(() => s_sandboxFeatures.SetCurrentWeatherIntensity(sun, rain));
		}

		private void RunAddCargoShips() {
			int amount;
			if (!int.TryParse(m_cargoShipsAmount, out amount)) {
				SetStatus("Cargo ship amount must be a number.");
				return;
			}
			RunSandboxCommand(() => s_sandboxFeatures.AddCargoShips(amount));
		}

		private void RunIncreaseVehicleLimit() {
			int amount;
			if (!int.TryParse(m_vehicleLimitAmount, out amount)) {
				SetStatus("Vehicle limit amount must be a number.");
				return;
			}
			RunSandboxCommand(() => s_sandboxFeatures.IncreaseVehicleLimit(amount));
		}

		private void RunGlobalSet() {
			int amount;
			if (!TryParseProductAndAmount(out amount)) {
				return;
			}
			SetStatus(s_globalEditor.SetGlobal(m_selectedProductId, amount));
			RefreshWindow();
		}

		private void RunGlobalAdd() {
			int amount;
			if (!TryParseProductAndAmount(out amount)) {
				return;
			}
			SetStatus(s_globalEditor.AddGlobal(m_selectedProductId, amount));
			RefreshWindow();
		}

		private void RunGlobalRemove() {
			int amount;
			if (!TryParseProductAndAmount(out amount)) {
				return;
			}
			SetStatus(s_globalEditor.RemoveGlobal(m_selectedProductId, amount));
			RefreshWindow();
		}

		private bool TryParseProductAndAmount(out int amount) {
			if (string.IsNullOrEmpty(m_selectedProductId)) {
				amount = 0;
				SetStatus("Select a product first.");
				return false;
			}
			if (!int.TryParse(m_resourceAmount, out amount) || amount < 0) {
				SetStatus("Amount must be a non-negative number.");
				return false;
			}
			return true;
		}

		private void SetStatus(string status) {
			m_status = status ?? "";
			if (m_statusField != null) {
				m_statusField.Text(m_status);
			}
		}

		private void RefreshWindow() {
			if (m_window != null && m_window.IsOpen) {
				OpenWindow();
			}
		}

		private void RunSafely(Action action) {
			try {
				action();
			} catch (Exception ex) {
				SetStatus(ex.Message);
			}
		}

		private static LocStrFormatted L(string value) {
			return new LocStrFormatted(value);
		}
	}
}
