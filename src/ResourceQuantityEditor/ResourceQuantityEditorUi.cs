using System;
using System.Linq;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Products;
using Mafi.Core.Terrain;
using Mafi.Core.Trains;
using Mafi.Localization;
using Mafi.Unity.Ui;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using UnityEngine;

namespace ResourceQuantityEditor {

	public sealed class ResourceQuantityEditorUi : MonoBehaviour {
		private static ResourceQuantityEditorUi s_instance;
		private static GlobalResourceEditorService s_globalEditor;
		private static SandboxFeatureService s_sandboxFeatures;
		private static TreeRangeRemovalService s_treeRangeRemoval;
		private static UiContext s_uiContext;
		private static OptionsStateService s_optionsState;
		private static LocomotiveEditorService s_locoEditor;
		private const string ICON_EMPTY = "Assets/Unity/UserInterface/General/Empty128.png";
		private const string ICON_POPULATION = "Assets/Unity/UserInterface/General/Population.svg";
		private const string ICON_STORAGE = "Assets/Unity/UserInterface/General/Storage.svg";
		private const string ICON_WEATHER = "Assets/Unity/UserInterface/General/Weather.svg";
		private const string ICON_WRENCH = "Assets/Unity/UserInterface/General/Wrench.svg";
		private const string ICON_TRUCK = "Assets/Unity/UserInterface/General/Truck.svg";


		private Window m_window;
		private TextField m_statusField;
		private int m_tab;
		private int m_maintainFrame;
		private string m_productFilter = "";
		private string m_weatherFilter = "";
		private bool m_weatherFilterWasEntered;
		private bool m_showProductsList;
		private bool m_showWeatherList;
		private bool m_showAsteroidsList;
		private string m_selectedWeatherId = "";
		private string m_selectedWeatherName = "";
		private string m_selectedAsteroidMaterial1Id = "";
		private string m_selectedAsteroidMaterial1Name = "";
		private string m_selectedAsteroidMaterial2Id = "";
		private string m_selectedAsteroidMaterial2Name = "";
		private int m_selectedAsteroidId;
		private bool m_hasSelectedAsteroid;
		private string m_selectedProductId = "";
		private string m_selectedProductName = "";
		private Dropdown<ProductProto> m_productDropdown;
		private Dropdown<TerrainMaterialProto> m_asteroidMaterial1Dropdown;
		private Dropdown<TerrainMaterialProto> m_asteroidMaterial2Dropdown;
		private string m_resourceAmount = "100";
		private string m_populationAmount = "100";
		private string m_weatherSunIntensity = "100";
		private string m_weatherRainIntensity = "0";
		private string m_asteroidRadius = "45";
		private string m_asteroidMaterialRatio = "1";
		private string m_asteroidLandingX = "0";
		private string m_asteroidLandingY = "0";
		private string m_cargoShipsAmount = "2";
		private string m_vehicleLimitAmount = "100";
		private string m_status = "";
		private string m_locoFilter = "";
		private bool m_locoFilterWasEntered;
		private LocomotiveEditState[] m_locoStates = Array.Empty<LocomotiveEditState>();

		private class LocomotiveEditState {
			public string Speed = "";
			public string MassEmpty = "";
			public string MassFull = "";
			public string EnginePower = "";
			public string TractiveEffort = "";
			public string BrakingForce = "";

			public float OrigSpeed;
			public float OrigMassEmpty;
			public float OrigMassFull;
			public float OrigEnginePower;
			public float OrigTractiveEffort;
			public float OrigBrakingForce;

			public bool HasChanges =>
				Speed != FmtSpeed(OrigSpeed) ||
				MassEmpty != FmtMass(OrigMassEmpty) ||
				MassFull != FmtMass(OrigMassFull) ||
				EnginePower != FmtPower(OrigEnginePower) ||
				TractiveEffort != FmtForce(OrigTractiveEffort) ||
				BrakingForce != FmtForce(OrigBrakingForce);
		}

		private static readonly (string Field, string Header, Func<float, string> Format)[] LocoFieldMeta = {
			("MaxSpeed",               "Speed\nkm/h",  v => FmtSpeed(v)),
			("MassTonsWhenEmpty",      "Empty\nmass t", v => FmtMass(v)),
			("MassTonsWhenFull",       "Full\nmass t",  v => FmtMass(v)),
			("EnginePowerKw",          "Power\nkW",     v => FmtPower(v)),
			("MaximumTractiveEffortKn","Tractive\nkN",  v => FmtForce(v)),
			("BrakingForceKn",         "Braking\nkN",   v => FmtForce(v)),
		};

		public static void Install(
			GlobalResourceEditorService globalEditor,
			SandboxFeatureService sandboxFeatures,
			TreeRangeRemovalService treeRangeRemoval,
			UiContext uiContext,
			OptionsStateService optionsState,
			LocomotiveEditorService locoEditor) {
			s_globalEditor = globalEditor;
			s_sandboxFeatures = sandboxFeatures;
			s_treeRangeRemoval = treeRangeRemoval;
			s_uiContext = uiContext;
			s_optionsState = optionsState;
			s_locoEditor = locoEditor;
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
			try {
				m_window = BuildWindow();
				m_window.Open(s_uiContext.UiRoot);
			} catch (Exception ex) {
				SetStatus("OpenWindow error: " + ex.Message);
			}
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
				.WindowSize(900, 800)
				.MakeMovable()
				.ShortcutToShow("F8");

			TabContainer tabs = new TabContainer().ReducedPaddingBody();
			tabs.Add(L("Resources"), null, BuildResourcesTab(), Scroll.No);
			tabs.Add(L("World"), null, BuildWorldTab(), Scroll.No);
			tabs.Add(L("Sandbox"), null, BuildSandboxTab(), Scroll.No);
			tabs.Add(L("Settlement"), null, BuildSettlementTab(), Scroll.No);
			tabs.Add(L("Logistics"), null, BuildLogisticsTab(), Scroll.No);
			tabs.Add(L("Locomotives"), null, BuildLocomotivesTab(), Scroll.No);
			if (m_tab > 5) {
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

		private UiComponent BuildResourcesTab() {
			Column root = new Column(12);
			ScrollColumn scroll = new ScrollColumn().FlexGrow(1);
			scroll.Add(BuildGlobalCommandDeck());
			if (m_showProductsList) {
				scroll.Add(BuildGlobalProductsList());
			}
			scroll.Add(BuildEconomyCheatsDeck());
			scroll.Add(BuildResearchDeck());
			root.Add(scroll);
			return root.FlexGrow(1);
		}

		private UiComponent BuildWorldTab() {
			Column root = new Column(12);
			ScrollColumn scroll = new ScrollColumn().FlexGrow(1);
			scroll.Add(BuildEnvironmentCheatsDeck());
			scroll.Add(BuildWeatherControlsDeck());
			if (m_showWeatherList) {
				scroll.Add(BuildWeatherListDeck());
			}
			scroll.Add(BuildTerrainControlsDeck());
			scroll.Add(BuildAsteroidControlsDeck());
			if (m_showAsteroidsList) {
				scroll.Add(BuildAsteroidsListDeck());
			}
			root.Add(scroll);
			return root.FlexGrow(1);
		}

		private UiComponent BuildSandboxTab() {
			Column root = new Column(12);
			ScrollColumn scroll = new ScrollColumn().FlexGrow(1);
			scroll.Add(BuildSandboxCommandDeck());
			scroll.Add(BuildTreeActionsDeck());
			root.Add(scroll);
			return root.FlexGrow(1);
		}

		private UiComponent BuildTreeActionsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("TREE ACTIONS"));

			Row actions = CenteredRow();
			actions.Add(ActionButton("Remove selected trees", StartTreeRangeRemoval, Button.Warning).Width(240));
			FinishCenteredRow(actions);

			panel.BodyAdd(new UiComponent[] { actions });
			return panel;
		}

		private UiComponent BuildSettlementTab() {
			Column root = new Column(12);
			ScrollColumn scroll = new ScrollColumn().FlexGrow(1);
			scroll.Add(BuildSandboxPopulationDeck());
			scroll.Add(BuildSettlementNeedsDeck());
			root.Add(scroll);
			return root.FlexGrow(1);
		}

		private UiComponent BuildLogisticsTab() {
			Column root = new Column(12);
			ScrollColumn scroll = new ScrollColumn().FlexGrow(1);
			scroll.Add(BuildLogisticsDeck());
			root.Add(scroll);
			return root.FlexGrow(1);
		}

		private UiComponent BuildGlobalCommandDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("GLOBAL RESOURCE CONTROL"));
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

				.CharLimit(12)
				.OnValueChanged(x => m_resourceAmount = x, isDelayed: false)
				.Width(160));
			FinishCenteredRow(selectedRow);

			Row actionsRow = CenteredRow();
			actionsRow.Add(ActionButton("Set amount", RunGlobalSet, Button.Primary).Width(130));
			actionsRow.Add(ActionButton("Add", RunGlobalAdd, Button.General).Width(100));
			actionsRow.Add(ActionButton("Remove", RunGlobalRemove, Button.Warning).Width(110));
			actionsRow.Add(ActionButton(m_showProductsList ? "Hide inventory" : "Show inventory", () => { m_showProductsList = !m_showProductsList; RefreshWindow(); }, Button.General).Width(160));
			
			// Настройка Dropdown
			actionsRow.Add(new Label(L("Select")).UpperCase(false).Width(56));
			
			m_productDropdown = new Dropdown<ProductProto>(ProductOptionFactory)
				.OnValueChanged((p, idx) => {
					SelectProduct(p);
					RefreshWindow();
				})
				.SetSearchStringLookup(p => p.Strings.Name.TranslatedString)
				.Width(350);
			
			// Получаем все продукты (сервис теперь сам фильтрует и сортирует их по алфавиту)
			var sortedProducts = s_globalEditor.GetGlobalProducts("")
				.Select(x => x.Product)
				.ToImmutableArray();
				
			m_productDropdown.SetOptions(sortedProducts);
			
			// Устанавливаем текущее значение
			if (selected != null) {
				m_productDropdown.SetValue(selected);
			}
			
			actionsRow.Add(m_productDropdown);
			FinishCenteredRow(actionsRow);

			panel.BodyAdd(new UiComponent[] { selectedRow, actionsRow });
			return panel;
		}

		private UiComponent ProductOptionFactory(ProductProto product, int index, bool isInDropdown) {
			Row row = new Row(8);
			if (product != null) {
				row.Add(new Icon(product, noTooltip: true, noRightClick: true).Size(24));
				row.Add(new Label(L(product.Strings.Name.TranslatedString)).FlexGrow(1));
			} else {
				row.Add(new Icon(ICON_EMPTY).Size(24));
				row.Add(new Label(L("None")).FlexGrow(1));
			}
			return row;
		}

		private UiComponent AsteroidMaterialOptionFactory(TerrainMaterialProto material, int index, bool isInDropdown) {
			Row row = new Row(8);
			if (material != null) {
				row.Add(new Icon(material.MinedProduct, noTooltip: true, noRightClick: true).Size(24));
				row.Add(new Label(L(material.MinedProduct.Strings.Name.TranslatedString)).FlexGrow(1));
			} else {
				row.Add(new Icon(ICON_EMPTY).Size(24));
				row.Add(new Label(L("None")).FlexGrow(1));
			}
			return row;
		}

		private UiComponent BuildGlobalProductsList() {
			PanelWithHeader products = new PanelWithHeader(L("GLOBAL INVENTORY"));

			Column list = new Column(8);
			list.Add(BuildItemHeader(includeQuantity: true));
			foreach (GlobalProductRow row in s_globalEditor.GetGlobalProducts(m_productFilter)) {
				list.Add(BuildGlobalRow(row));
			}

			ScrollColumn scroll = new ScrollColumn();
			scroll.ScrollerAlwaysVisible();
			scroll.Height(400);
			scroll.Add(list);
			products.BodyAdd(new UiComponent[] { scroll });
			return products;
		}

		private UiComponent BuildSandboxCommandDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("COI EDITOR"));

			Row primary = CenteredRow();
			primary.Add(ActionButton("Enable common cheats", () => RunSandboxCommand(() => s_sandboxFeatures.EnableCommonCheats()), Button.Primary).Width(210));
			primary.Add(ActionButton("Disable common cheats", () => RunSandboxCommand(() => s_sandboxFeatures.DisableCommonCheats()), Button.General).Width(215));
			primary.Add(ActionButton("Unlock all research", () => RunSandboxCommand(() => s_sandboxFeatures.UnlockAllResearch()), Button.General).Width(190));
			primary.Add(ActionButton("Add repaired cargo ship", () => RunSandboxCommand(() => s_sandboxFeatures.AddRepairedCargoShip()), Button.General).Width(220));
			primary.Add(ActionButton("Refresh status", () => RunSandboxCommand(() => s_sandboxFeatures.GetStatus()), Button.General).Width(150));
			FinishCenteredRow(primary);

			Row optionsState = CenteredRow();
			optionsState.Add(new Label(L("Options state")).UpperCase(false).Width(110));
			optionsState.Add(ActionButton("Save current", () => RunSandboxCommand(() => s_optionsState.SaveCurrentState()), Button.Primary).Width(150));
			optionsState.Add(ActionButton("Load saved", () => RunSandboxCommand(() => s_optionsState.LoadSavedState()), Button.General).Width(140));
			optionsState.Add(ActionButton("Clear saved", () => RunSandboxCommand(() => s_optionsState.ClearSavedState()), Button.Warning).Width(140));
			optionsState.Add(new Label(L(s_optionsState.HasSavedState() ? "Saved state exists" : "No saved state")).Width(180));
			FinishCenteredRow(optionsState);

			panel.BodyAdd(new UiComponent[] { primary, optionsState });
			return panel;
		}

		private UiComponent BuildSettlementNeedsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("SETTLEMENT NEEDS"));
			panel.BodyGap(8);

			Row grid = new Row(20); // Большой отступ между колонками
			
			Column col1 = new Column(8);
			col1.Add(SandboxToggle("No food need", s_sandboxFeatures.IgnoreMissingFood, () => s_sandboxFeatures.SetIgnoreMissingFood(!s_sandboxFeatures.IgnoreMissingFood)));
			col1.Add(SandboxToggle("No workers need", s_sandboxFeatures.IgnoreMissingWorkers, () => s_sandboxFeatures.SetIgnoreMissingWorkers(!s_sandboxFeatures.IgnoreMissingWorkers)));
			col1.Add(SandboxToggle("No power need", s_sandboxFeatures.IgnoreMissingPower, () => s_sandboxFeatures.SetIgnoreMissingPower(!s_sandboxFeatures.IgnoreMissingPower)));
			col1.Add(SandboxToggle("No clean water need", s_sandboxFeatures.NoCleanWaterNeed, () => s_sandboxFeatures.SetNoCleanWaterNeed(!s_sandboxFeatures.NoCleanWaterNeed)));
			
			Column col2 = new Column(8);
			col2.Add(SandboxToggle("No computing need", s_sandboxFeatures.IgnoreMissingComputing, () => s_sandboxFeatures.SetIgnoreMissingComputing(!s_sandboxFeatures.IgnoreMissingComputing)));
			col2.Add(SandboxToggle("No unity need", s_sandboxFeatures.IgnoreMissingUnity, () => s_sandboxFeatures.SetIgnoreMissingUnity(!s_sandboxFeatures.IgnoreMissingUnity)));
			col2.Add(SandboxToggle("Unlimited unity", s_sandboxFeatures.UnlimitedUnity, () => s_sandboxFeatures.SetUnlimitedUnity(!s_sandboxFeatures.UnlimitedUnity)));
			col2.Add(SandboxToggle("No disease effects", s_sandboxFeatures.NoDiseaseEffects, () => s_sandboxFeatures.SetNoDiseaseEffects(!s_sandboxFeatures.NoDiseaseEffects)));
			col2.Add(SandboxToggle("No wastewater prod.", s_sandboxFeatures.NoWastewaterProduction, () => s_sandboxFeatures.SetNoWastewaterProduction(!s_sandboxFeatures.NoWastewaterProduction)));


			grid.Add(col1.FlexGrow(1));
			grid.Add(col2.FlexGrow(1));

			panel.BodyAdd(new UiComponent[] { grid });
			return panel;
		}

		private UiComponent BuildEconomyCheatsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("ECONOMY CHEATS"));
			panel.BodyGap(8);

			Row grid = new Row(20);
			
			Column col1 = new Column(8);
			col1.Add(SandboxToggle("Instant build & construction", s_sandboxFeatures.InstantBuildAndConstruction, () => s_sandboxFeatures.SetInstantBuildAndConstruction(!s_sandboxFeatures.InstantBuildAndConstruction)));
			col1.Add(SandboxToggle("Source/sink buildings", s_sandboxFeatures.AreSourcesAndSinksAllowed, () => s_sandboxFeatures.SetSourceSinks(!s_sandboxFeatures.AreSourcesAndSinksAllowed)));
			col1.Add(SandboxToggle("No maintenance", s_sandboxFeatures.IgnoreMissingMaintenance, () => s_sandboxFeatures.SetIgnoreMissingMaintenance(!s_sandboxFeatures.IgnoreMissingMaintenance)));
			
			Column col2 = new Column(8);
			col2.Add(SandboxToggle("No construction costs", s_sandboxFeatures.NoConstructionCosts, () => s_sandboxFeatures.SetNoConstructionCosts(!s_sandboxFeatures.NoConstructionCosts)));
			col2.Add(SandboxToggle("Free research", s_sandboxFeatures.FreeResearch, () => s_sandboxFeatures.SetFreeResearch(!s_sandboxFeatures.FreeResearch)));
			col2.Add(SandboxToggle("Infinite focus", s_sandboxFeatures.InfiniteFocus, () => s_sandboxFeatures.SetInfiniteFocus(!s_sandboxFeatures.InfiniteFocus)));

			grid.Add(col1.FlexGrow(1));
			grid.Add(col2.FlexGrow(1));

			panel.BodyAdd(new UiComponent[] { grid });
			return panel;
		}

		private UiComponent BuildResearchDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("RESEARCH"));

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
			panel.BodyGap(8);

			Row grid = new Row(20);
			
			Column col1 = new Column(8);
			col1.Add(SandboxToggle("Disable food consumption", s_sandboxFeatures.UnlimitedFood, () => s_sandboxFeatures.SetUnlimitedFood(!s_sandboxFeatures.UnlimitedFood)));
			col1.Add(SandboxToggle("Instant tree growth", s_sandboxFeatures.InstantTreeGrowth, () => s_sandboxFeatures.SetInstantTreeGrowth(!s_sandboxFeatures.InstantTreeGrowth)));
			col1.Add(SandboxToggle("No air pollution", s_sandboxFeatures.NoAirPollution, () => s_sandboxFeatures.SetNoAirPollution(!s_sandboxFeatures.NoAirPollution)));
			col1.Add(SandboxToggle("No water pollution", s_sandboxFeatures.NoWaterPollution, () => s_sandboxFeatures.SetNoWaterPollution(!s_sandboxFeatures.NoWaterPollution)));
			col1.Add(SandboxToggle("No ship pollution", s_sandboxFeatures.NoShipPollution, () => s_sandboxFeatures.SetNoShipPollution(!s_sandboxFeatures.NoShipPollution)));
			col1.Add(SandboxToggle("No train pollution", s_sandboxFeatures.NoTrainPollution, () => s_sandboxFeatures.SetNoTrainPollution(!s_sandboxFeatures.NoTrainPollution)));
			col1.Add(SandboxToggle("No vehicle pollution", s_sandboxFeatures.NoVehiclePollution, () => s_sandboxFeatures.SetNoVehiclePollution(!s_sandboxFeatures.NoVehiclePollution)));
			
			Column col2 = new Column(8);
			col2.Add(SandboxToggle("No bio waste", s_sandboxFeatures.NoBioWaste, () => s_sandboxFeatures.SetNoBioWaste(!s_sandboxFeatures.NoBioWaste)));
			col2.Add(SandboxToggle("No landfill waste", s_sandboxFeatures.NoLandfillWaste, () => s_sandboxFeatures.SetNoLandfillWaste(!s_sandboxFeatures.NoLandfillWaste)));
			col2.Add(SandboxToggle("No toxic slurry waste", s_sandboxFeatures.NoToxicWaste, () => s_sandboxFeatures.SetNoToxicWaste(!s_sandboxFeatures.NoToxicWaste)));
			col2.Add(SandboxToggle("No depleted uranium waste", s_sandboxFeatures.NoRadioactiveWaste, () => s_sandboxFeatures.SetNoRadioactiveWaste(!s_sandboxFeatures.NoRadioactiveWaste)));

			grid.Add(col1.FlexGrow(1));
			grid.Add(col2.FlexGrow(1));

			panel.BodyAdd(new UiComponent[] { grid });
			return panel;
		}

		private UiComponent BuildWeatherControlsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("WEATHER CONTROLS"));
			panel.BodyGap(8);

			Row current = CenteredRow();
			current.Add(new Label(L("Current")).UpperCase(false).Width(110));
			current.Add(new Label(L(s_sandboxFeatures.CurrentWeatherName)).Width(240));
			current.Add(ActionButton("Reset", () => RunSandboxCommand(() => s_sandboxFeatures.ClearWeather()), Button.General).Width(100));
			current.Add(ActionButton(m_showWeatherList ? "Hide list" : "Show list", () => { m_showWeatherList = !m_showWeatherList; RefreshWindow(); }, Button.General).Width(130));
			FinishCenteredRow(current);

			Row grid = new Row(20);
			Column col1 = new Column(8);
			col1.Add(SandboxToggle("Sunny weather", s_sandboxFeatures.IsSunnyWeatherEnabled, () => s_sandboxFeatures.ToggleSunnyWeather()));
			col1.Add(SandboxToggle("Clouds", s_sandboxFeatures.CloudsEnabled, () => s_sandboxFeatures.SetCloudsEnabled(!s_sandboxFeatures.CloudsEnabled)));
			col1.Add(SandboxToggle("Fog", s_sandboxFeatures.FogEnabled, () => s_sandboxFeatures.SetFogEnabled(!s_sandboxFeatures.FogEnabled)));
			col1.Add(SandboxToggle("Weather effects", s_sandboxFeatures.WeatherEffectsEnabled, () => s_sandboxFeatures.SetWeatherEffectsEnabled(!s_sandboxFeatures.WeatherEffectsEnabled)));
			
			Column col2 = new Column(8);
			col2.Add(new Label(L("Intensity")).UpperCase(false).TinyFontSize());
			
			Row intensityRow = new Row(4);
			intensityRow.Add(new Label(L("Sun")).UpperCase(false).Width(40));
			intensityRow.Add(new TextField()
				.Text(m_weatherSunIntensity)

				.CharLimit(5)
				.OnValueChanged(x => m_weatherSunIntensity = x, isDelayed: false)
				.Width(80));
			intensityRow.Add(new Label(L("Rain")).UpperCase(false).Width(40));
			intensityRow.Add(new TextField()
				.Text(m_weatherRainIntensity)

				.CharLimit(5)
				.OnValueChanged(x => m_weatherRainIntensity = x, isDelayed: false)
				.Width(80));
			col2.Add(intensityRow);
			col2.Add(ActionButton("Apply intensity", RunWeatherIntensity, Button.General).Width(170));

			grid.Add(col1.FlexGrow(1));
			grid.Add(col2.FlexGrow(1));

			Row selected = CenteredRow();
			selected.Add(new Label(L("Selected")).UpperCase(false).Width(110));
			selected.Add(new Label(L(string.IsNullOrEmpty(m_selectedWeatherName) ? "None" : m_selectedWeatherName)).Width(240));
			selected.Add(ActionButton("Apply selected", RunSelectedWeather, Button.Primary).Width(170));
			FinishCenteredRow(selected);

			Row filter = CenteredRow();
			filter.Add(new Label(L("Search")).UpperCase(false).Width(110));
			TextField weatherFilterField = new TextField()
				.Text(m_weatherFilter)
				.OnValueChanged(delegate(string x) {
					if (!string.IsNullOrEmpty(x)) {
						m_weatherFilterWasEntered = true;
					}
					m_weatherFilter = x;
					RefreshWindow();
				}, isDelayed: false)
				.Width(260);
			if (!m_weatherFilterWasEntered) {
				weatherFilterField.Placeholder(L("id or name"));
			}
			filter.Add(weatherFilterField);
			FinishCenteredRow(filter);

			panel.BodyAdd(new UiComponent[] { current, grid, selected, filter });
			return panel;
		}

		private UiComponent BuildTerrainControlsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("TERRAIN CONTROLS"));
			panel.BodyGap(8);

			Row grid = new Row(20);
			
			Column col1 = new Column(8);
			col1.Add(SandboxToggle("Process mining", s_sandboxFeatures.ProcessTerrainDesignations, () => s_sandboxFeatures.SetProcessTerrainDesignations(!s_sandboxFeatures.ProcessTerrainDesignations)));
			col1.Add(SandboxToggle("Process surface", s_sandboxFeatures.ProcessSurfaceDesignations, () => s_sandboxFeatures.SetProcessSurfaceDesignations(!s_sandboxFeatures.ProcessSurfaceDesignations)));
			col1.Add(SandboxToggle("Disable physics", s_sandboxFeatures.DisableTerrainPhysics, () => s_sandboxFeatures.SetDisableTerrainPhysics(!s_sandboxFeatures.DisableTerrainPhysics)));
			
			Column col2 = new Column(8);
			col2.Add(SandboxToggle("Unlimited designations", s_sandboxFeatures.UnlimitedMiningArea, () => s_sandboxFeatures.SetUnlimitedMiningArea(!s_sandboxFeatures.UnlimitedMiningArea)));
			col2.Add(SandboxToggle("Unlimited tower area", s_sandboxFeatures.UnlimitedTowerArea, () => s_sandboxFeatures.SetUnlimitedTowerArea(!s_sandboxFeatures.UnlimitedTowerArea)));
			col2.Add(ActionButton("Clear designations", () => RunSandboxCommand(() => s_sandboxFeatures.ClearAllTerrainDesignations()), Button.Warning).Width(240));

			grid.Add(col1.FlexGrow(1));
			grid.Add(col2.FlexGrow(1));

			panel.BodyAdd(new UiComponent[] { grid });
			return panel;
		}

		private UiComponent BuildAsteroidControlsDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("ASTEROID CONTROL"));
			panel.BodyGap(8);

			// Получаем все материалы для астероидов
			var allMaterials = s_sandboxFeatures.GetAllAsteroidMaterials();

			Row materials = CenteredRow();
			
			// Material 1 Dropdown
			materials.Add(new Label(L("Material 1")).UpperCase(false).Width(85));
			m_asteroidMaterial1Dropdown = new Dropdown<TerrainMaterialProto>(AsteroidMaterialOptionFactory)
				.OnValueChanged((m, idx) => {
					if (m != null) {
						m_selectedAsteroidMaterial1Id = m.Id.ToString();
						m_selectedAsteroidMaterial1Name = m.MinedProduct.Strings.Name.ToString();
					}
					RefreshWindow();
				})
				.SetSearchStringLookup(m => m != null ? m.MinedProduct.Strings.Name.TranslatedString : "")
				.Width(350);
			m_asteroidMaterial1Dropdown.SetOptions(allMaterials);
			TerrainMaterialProto selectedMat1 = GetSelectedAsteroidMaterial(m_selectedAsteroidMaterial1Id, allMaterials);
			if (selectedMat1 != null) {
				m_asteroidMaterial1Dropdown.SetValue(selectedMat1);
			}
			materials.Add(m_asteroidMaterial1Dropdown);
			
			// Material 2 Dropdown (с возможностью очистки)
			materials.Add(new Label(L("Material 2")).UpperCase(false).Width(85));
			m_asteroidMaterial2Dropdown = new Dropdown<TerrainMaterialProto>(AsteroidMaterialOptionFactory)
				.OnValueChanged((m, idx) => {
					if (m != null) {
						m_selectedAsteroidMaterial2Id = m.Id.ToString();
						m_selectedAsteroidMaterial2Name = m.MinedProduct.Strings.Name.ToString();
					} else {
						m_selectedAsteroidMaterial2Id = "";
						m_selectedAsteroidMaterial2Name = "";
					}
					RefreshWindow();
				})
				.SetSearchStringLookup(m => m != null ? m.MinedProduct.Strings.Name.TranslatedString : "None")
				.Width(350);
			// Добавляем null как первый элемент для возможности очистки
			var materialsWithNone = new TerrainMaterialProto[] { null }.Concat(allMaterials);
			m_asteroidMaterial2Dropdown.SetOptions(materialsWithNone);
			TerrainMaterialProto selectedMat2 = GetSelectedAsteroidMaterial(m_selectedAsteroidMaterial2Id, allMaterials);
			if (selectedMat2 != null) {
				m_asteroidMaterial2Dropdown.SetValue(selectedMat2);
			} else if (string.IsNullOrEmpty(m_selectedAsteroidMaterial2Id)) {
				m_asteroidMaterial2Dropdown.SetValue(null);
			}
			materials.Add(m_asteroidMaterial2Dropdown);
			
			FinishCenteredRow(materials);

			Row create = CenteredRow();
			create.Add(new Label(L("Radius")).UpperCase(false).Width(70));
			create.Add(new TextField()
				.Text(m_asteroidRadius)

				.CharLimit(4)
				.OnValueChanged(x => m_asteroidRadius = x, isDelayed: false)
				.Width(80));
			create.Add(new Label(L("M1:M2 ratio")).UpperCase(false).Width(100));
			create.Add(new TextField()
				.Text(m_asteroidMaterialRatio)

				.CharLimit(4)
				.OnValueChanged(x => m_asteroidMaterialRatio = x, isDelayed: false)
				.Width(80));
			create.Add(ActionButton("Place in orbit", RunSpawnAsteroid, Button.Primary).Width(170));
			create.Add(ActionButton("Capture selected", RunCaptureAsteroid, Button.General).Width(170));
			create.Add(ActionButton(m_showAsteroidsList ? "Hide active list" : "Show active list", () => { m_showAsteroidsList = !m_showAsteroidsList; RefreshWindow(); }, Button.General).Width(170));
			FinishCenteredRow(create);

			Row landing = CenteredRow();
			landing.Add(new Label(L("Selected asteroid")).UpperCase(false).Width(130));
			landing.Add(new Label(L(!m_hasSelectedAsteroid ? "None" : "#" + m_selectedAsteroidId)).Width(80));
			landing.Add(new Label(L("Landing X")).UpperCase(false).Width(80));
			landing.Add(new TextField()
				.Text(m_asteroidLandingX)

				.CharLimit(6)
				.OnValueChanged(x => m_asteroidLandingX = x, isDelayed: false)
				.Width(90));
			landing.Add(new Label(L("Y")).UpperCase(false).Width(30));
			landing.Add(new TextField()
				.Text(m_asteroidLandingY)

				.CharLimit(6)
				.OnValueChanged(x => m_asteroidLandingY = x, isDelayed: false)
				.Width(90));
			landing.Add(ActionButton("Land selected asteroid", RunDropAsteroid, Button.Warning).Width(210));
			FinishCenteredRow(landing);

			Column body = new Column(8);
			body.Add(materials);
			body.Add(create);
			body.Add(landing);

			panel.BodyAdd(new UiComponent[] { body });
			return panel;
		}

		private UiComponent BuildAsteroidsListDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("ACTIVE ASTEROIDS"));

			Column list = new Column(8);
			Row header = CenteredRow();
			header.Add(new Label(L("ID")).TinyFontSize().Width(70));
			header.Add(new Label(L("State")).TinyFontSize().Width(100));
			header.Add(new Label(L("Radius")).TinyFontSize().Width(80));
			header.Add(new Label(L("Materials")).TinyFontSize().Width(500));
			FinishCenteredRow(header);
			list.Add(header);

			foreach (AsteroidRow asteroid in s_sandboxFeatures.GetAsteroidRows()) {
				list.Add(BuildAsteroidRow(asteroid));
			}

			ScrollColumn scroll = new ScrollColumn();
			scroll.ScrollerAlwaysVisible();
			scroll.Height(170);
			scroll.Add(list);
			panel.BodyAdd(new UiComponent[] { scroll });
			return panel;
		}

		private UiComponent BuildAsteroidRow(AsteroidRow asteroid) {
			Row row = CenteredRow();
			row.Add(new ButtonText(Button.Area, L("#" + asteroid.Id), delegate {
				m_selectedAsteroidId = asteroid.Id;
				m_hasSelectedAsteroid = true;
				RefreshWindow();
			}).Width(70));
			row.Add(new Label(L(asteroid.State)).Width(100));
			row.Add(new Label(L(asteroid.Radius.ToString())).Width(80));
			row.Add(new Label(L(asteroid.Materials)).Width(500));
			FinishCenteredRow(row);
			return row;
		}

		private UiComponent BuildWeatherListDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("WEATHER LIST"));

			Column list = new Column(8);
			foreach (WeatherRow row in s_sandboxFeatures.GetWeatherRows(m_weatherFilter)) {
				list.Add(BuildWeatherRow(row));
			}

			ScrollColumn scroll = new ScrollColumn();
			scroll.ScrollerAlwaysVisible();
			scroll.Height(300);
			scroll.Add(list);
			panel.BodyAdd(new UiComponent[] { scroll });
			return panel;
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
			panel.BodyGap(8);

			Row actions = CenteredRow();
			actions.Add(new Label(L("Cargo ships")).UpperCase(false).Width(110));
			actions.Add(new TextField()
				.Text(m_cargoShipsAmount)

				.CharLimit(5)
				.OnValueChanged(x => m_cargoShipsAmount = x, isDelayed: false)
				.Width(80));
			actions.Add(ActionButton("Add repaired ships", RunAddCargoShips, Button.Primary).Width(190));
			FinishCenteredRow(actions);

			Row grid = new Row(20);
			Column col1 = new Column(8);
			col1.Add(SandboxToggle("No fuel consumption", s_sandboxFeatures.IgnoreFuelConsumption, () => s_sandboxFeatures.SetIgnoreFuelConsumption(!s_sandboxFeatures.IgnoreFuelConsumption)));
			col1.Add(SandboxToggle("Unlimited vehicle fuel", s_sandboxFeatures.UnlimitedVehicleFuel, () => s_sandboxFeatures.SetUnlimitedVehicleFuel(!s_sandboxFeatures.UnlimitedVehicleFuel)));
			
			Column col2 = new Column(8);
			col2.Add(SandboxToggle("Instant cargo ships", s_sandboxFeatures.InstantCargoShips, () => s_sandboxFeatures.SetInstantCargoShips(!s_sandboxFeatures.InstantCargoShips)));
			grid.Add(col1.FlexGrow(1));
			grid.Add(col2.FlexGrow(1));

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

				.CharLimit(6)
				.OnValueChanged(x => m_vehicleLimitAmount = x, isDelayed: false)
				.Width(90));
			vehicles.Add(ActionButton("Increase limit", RunIncreaseVehicleLimit, Button.General).Width(170));
			FinishCenteredRow(vehicles);

			panel.BodyAdd(new UiComponent[] { actions, grid, world, vehicles });
			return panel;
		}

		/* ─── Locomotives Tab ─── */

		private UiComponent BuildLocomotivesTab() {
			Column root = new Column(12);
			try {
				ScrollColumn scroll = new ScrollColumn().FlexGrow(1);
				scroll.Add(BuildLocomotivesDeck());
				root.Add(scroll);
			} catch (Exception ex) {
				root.Add(new Label(L("Error loading locomotives: " + ex.Message)));
				Mafi.Log.Error("LocomotivesTab: " + ex);
			}
			return root.FlexGrow(1);
		}

		private UiComponent BuildLocomotivesDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("LOCOMOTIVES"));
			LocomotiveProto[] locos;
			try {
				locos = s_locoEditor.GetAllLocomotives();
			} catch (Exception ex) {
				panel.BodyAdd(new UiComponent[] { new Label(L("Failed to load locomotive data: " + ex.Message)) });
				Mafi.Log.Error("LocomotivesDeck: " + ex);
				return panel;
			}
			InitLocoStates(locos);

			// Toolbar: icon + actions
			Row toolbar = CenteredRow();
			toolbar.Add(new Icon(ICON_WRENCH).Size(24));
			toolbar.Add(new Label(L("Locomotive editor")).IncFontSize().Width(200));
			toolbar.Add(ActionButton("Apply all", () => ApplyAllLocomotives(locos), Button.Primary).Width(130));
			toolbar.Add(ActionButton("Reset all", () => ResetAllLocomotives(locos), Button.Warning).Width(130));

			TextField filterField = new TextField()
				.Text(m_locoFilter)
				.OnValueChanged(delegate(string x) {
					m_locoFilterWasEntered = true;
					m_locoFilter = x;
				}, isDelayed: false)
				.Width(200);
			if (!m_locoFilterWasEntered) {
				filterField.Placeholder(L("filter by name or id"));
			}
			toolbar.Add(filterField);
			FinishCenteredRow(toolbar);

			// Table header
			Column list = new Column(4);
			BuildLocoHeader(list);

			// Rows
			for (int i = 0; i < locos.Length; i++) {
				if (!MatchesLocoFilter(locos[i])) {
					continue;
				}
				try {
					BuildLocoRow(list, locos[i], i);
				} catch (Exception ex) {
					list.Add(new Label(L("Error: " + locos[i].Id + " — " + ex.Message)));
					Mafi.Log.Error("Locomotives row " + i + ": " + ex);
				}
			}

			ScrollColumn scroller = new ScrollColumn();
			scroller.ScrollerAlwaysVisible();
			scroller.Height(480);
			scroller.Add(list);
			panel.BodyAdd(new UiComponent[] { toolbar, scroller });
			return panel;
		}

		private bool MatchesLocoFilter(LocomotiveProto loco) {
			if (string.IsNullOrEmpty(m_locoFilter)) {
				return true;
			}
			string filter = m_locoFilter.Trim();
			return loco.Id.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
				|| loco.Strings.Name.TranslatedString.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private void InitLocoStates(LocomotiveProto[] locos) {
			if (m_locoStates.Length == locos.Length) {
				return;
			}
			m_locoStates = new LocomotiveEditState[locos.Length];
			for (int i = 0; i < m_locoStates.Length; i++) {
				m_locoStates[i] = new LocomotiveEditState();
			}
		}

		private void LoadLocoValues(LocomotiveProto loco, int index) {
			LocomotiveEditState s = m_locoStates[index];
			s.OrigSpeed = s_locoEditor.GetMaxSpeedKmh(loco);
			s.OrigMassEmpty = s_locoEditor.GetFieldFloat(loco, "MassTonsWhenEmpty");
			s.OrigMassFull = s_locoEditor.GetFieldFloat(loco, "MassTonsWhenFull");
			s.OrigEnginePower = s_locoEditor.GetFieldFloat(loco, "EnginePowerKw");
			s.OrigTractiveEffort = s_locoEditor.GetFieldFloat(loco, "MaximumTractiveEffortKn");
			s.OrigBrakingForce = s_locoEditor.GetFieldFloat(loco, "BrakingForceKn");

			s.Speed = FmtSpeed(s.OrigSpeed);
			s.MassEmpty = FmtMass(s.OrigMassEmpty);
			s.MassFull = FmtMass(s.OrigMassFull);
			s.EnginePower = FmtPower(s.OrigEnginePower);
			s.TractiveEffort = FmtForce(s.OrigTractiveEffort);
			s.BrakingForce = FmtForce(s.OrigBrakingForce);
		}

		private void BuildLocoHeader(Column list) {
			Row header = new Row(6);
			header.Add(new Label(L("Locomotive")).TinyFontSize().Width(180));
			foreach ((string field, string head, _) in LocoFieldMeta) {
				header.Add(new Label(L(head)).TinyFontSize().Width(72));
			}
			header.Add(new Label(L("")).Width(140));
			list.Add(header);
		}

		private void BuildLocoRow(Column list, LocomotiveProto loco, int index) {
			LocomotiveEditState state = m_locoStates[index];
			if (string.IsNullOrEmpty(state.Speed)) {
				LoadLocoValues(loco, index);
			}

			bool changed = state.HasChanges;

			Row row = new Row(6);
			row.Add(new Label(L(loco.Strings.Name.TranslatedString)).Width(180));

			AddLocoField(row, state.Speed,          val => state.Speed = val);
			AddLocoField(row, state.MassEmpty,      val => state.MassEmpty = val);
			AddLocoField(row, state.MassFull,       val => state.MassFull = val);
			AddLocoField(row, state.EnginePower,    val => state.EnginePower = val);
			AddLocoField(row, state.TractiveEffort, val => state.TractiveEffort = val);
			AddLocoField(row, state.BrakingForce,   val => state.BrakingForce = val);

			ButtonVariant applyVariant = changed ? Button.Warning : Button.General;
			row.Add(ActionButton("Apply", () => ApplyLocomotive(loco, index), applyVariant).Width(68));
			row.Add(ActionButton("Reset", () => ResetLocomotive(loco, index), Button.Area).Width(68));
			list.Add(row);
		}

		private static void AddLocoField(Row row, string currentVal, Action<string> setter) {
			row.Add(new TextField()
				.Text(currentVal)
				.CharLimit(8)
				.OnValueChanged(setter, isDelayed: false)
				.Width(70));
		}

		private void ApplyLocomotive(LocomotiveProto loco, int index) {
			LocomotiveEditState state = m_locoStates[index];
			RunSafely(() => {
				s_locoEditor.SetMaxSpeedKmh(loco, ParseFloat(state.Speed));
				s_locoEditor.SetFieldFloat(loco, "MassTonsWhenEmpty", ParseFloat(state.MassEmpty));
				s_locoEditor.SetFieldFloat(loco, "MassTonsWhenFull", ParseFloat(state.MassFull));
				s_locoEditor.SetFieldFloat(loco, "EnginePowerKw", ParseFloat(state.EnginePower));
				s_locoEditor.SetFieldFloat(loco, "MaximumTractiveEffortKn", ParseFloat(state.TractiveEffort));
				s_locoEditor.SetFieldFloat(loco, "BrakingForceKn", ParseFloat(state.BrakingForce));
				LoadLocoValues(loco, index);
				SetStatus("Applied: " + loco.Strings.Name);
			});
			RefreshWindow();
		}

		private void ApplyAllLocomotives(LocomotiveProto[] locos) {
			for (int i = 0; i < locos.Length; i++) {
				if (m_locoStates[i].HasChanges) {
					ApplyLocomotive(locos[i], i);
				}
			}
			SetStatus("Applied all changed locomotives.");
		}

		private void ResetLocomotive(LocomotiveProto loco, int index) {
			LocomotiveEditState state = m_locoStates[index];
			state.Speed = FmtSpeed(state.OrigSpeed);
			state.MassEmpty = FmtMass(state.OrigMassEmpty);
			state.MassFull = FmtMass(state.OrigMassFull);
			state.EnginePower = FmtPower(state.OrigEnginePower);
			state.TractiveEffort = FmtForce(state.OrigTractiveEffort);
			state.BrakingForce = FmtForce(state.OrigBrakingForce);
			SetStatus("Reset: " + loco.Strings.Name);
			RefreshWindow();
		}

		private void ResetAllLocomotives(LocomotiveProto[] locos) {
			for (int i = 0; i < locos.Length; i++) {
				ResetLocomotive(locos[i], i);
			}
			SetStatus("Reset all locomotives.");
		}

		private static string FmtSpeed(float v)  => v.ToString("F0");
		private static string FmtMass(float v)   => v.ToString("F1");
		private static string FmtPower(float v)  => v.ToString("F0");
		private static string FmtForce(float v)  => v.ToString("F0");

		private static float ParseFloat(string value) {
			if (string.IsNullOrEmpty(value)) {
				return 0f;
			}
			float result;
			if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result)) {
				return result;
			}
			return 0f;
		}

		/* ─── end Locomotives Tab ─── */

		private UiComponent BuildSandboxPopulationDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("POPULATION"));

			Row people = CenteredRow();
			people.Add(new Label(L("Amount")).UpperCase(false).Width(72));
			people.Add(new TextField()
				.Text(m_populationAmount)

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

			Row actions = CenteredRow();
			actions.Add(ActionButton("Remove selected trees", StartTreeRangeRemoval, Button.Warning).Width(210));
			actions.Add(SandboxToggle("Sunny weather", s_sandboxFeatures.IsSunnyWeatherEnabled, () => s_sandboxFeatures.ToggleSunnyWeather()));
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


		private UiComponent SandboxToggle(string title, bool enabled, Func<string> action) {
			Row row = new Row(6);
			row.Add(new Toggle(false)
				.Value(enabled)
				.OnValueChanged(_ => RunSandboxCommand(action)));
			row.Add(new Label(L(title)).UpperCase(false).IncFontSize());
			return row;
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
			GlobalProductRow[] rows = s_globalEditor.GetGlobalProducts(m_selectedProductId);
			for (int i = 0; i < rows.Length; i++) {
				if (rows[i].Product.Id.ToString() == m_selectedProductId) {
					return rows[i].Product;
				}
			}
			return null;
		}

		private TerrainMaterialProto GetSelectedAsteroidMaterial(string materialId, TerrainMaterialProto[] materials) {
			if (string.IsNullOrEmpty(materialId)) {
				return null;
			}
			foreach (TerrainMaterialProto material in materials) {
				if (material.Id.ToString() == materialId) {
					return material;
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

		private void RunSpawnAsteroid() {
			int radius;
			int ratio;
			if (!int.TryParse(m_asteroidRadius, out radius) || !int.TryParse(m_asteroidMaterialRatio, out ratio)) {
				SetStatus("Asteroid radius and material ratio must be numbers.");
				return;
			}
			RunSandboxCommand(() => s_sandboxFeatures.SpawnAsteroidToOrbit(
				m_selectedAsteroidMaterial1Id,
				m_selectedAsteroidMaterial2Id,
				radius,
				ratio));
		}

		private void RunCaptureAsteroid() {
			if (!m_hasSelectedAsteroid) {
				SetStatus("Select an asteroid first.");
				return;
			}
			RunSandboxCommand(() => s_sandboxFeatures.CaptureAsteroidToOrbit(m_selectedAsteroidId));
		}

		private void RunDropAsteroid() {
			int x;
			int y;
			if (!m_hasSelectedAsteroid) {
				SetStatus("Select an asteroid first.");
				return;
			}
			if (!int.TryParse(m_asteroidLandingX, out x) || !int.TryParse(m_asteroidLandingY, out y)) {
				SetStatus("Landing coordinates must be numbers.");
				return;
			}
			RunSandboxCommand(() => s_sandboxFeatures.DropAsteroidAt(m_selectedAsteroidId, x, y));
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
