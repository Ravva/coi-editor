using System;
using System.Linq;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Products;
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
		private string m_asteroidMaterialFilter = "";
		private bool m_asteroidMaterialFilterWasEntered;
		private bool m_showAsteroidMaterial1Dropdown;
		private bool m_showAsteroidMaterial2Dropdown;
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

		public static void Install(
			GlobalResourceEditorService globalEditor,
			SandboxFeatureService sandboxFeatures,
			TreeRangeRemovalService treeRangeRemoval,
			UiContext uiContext) {
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
				.WindowSize(900, 800)
				.MakeMovable()
				.ShortcutToShow("F8");

			TabContainer tabs = new TabContainer().ReducedPaddingBody();
			tabs.Add(L("Resources"), null, BuildResourcesTab(), Scroll.No);
			tabs.Add(L("World"), null, BuildWorldTab(), Scroll.No);
			tabs.Add(L("Sandbox"), null, BuildSandboxTab(), Scroll.No);
			tabs.Add(L("Settlement"), null, BuildSettlementTab(), Scroll.No);
			tabs.Add(L("Logistics"), null, BuildLogisticsTab(), Scroll.No);
			if (m_tab > 4) {
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
				.NumericOnly()
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

			panel.BodyAdd(new UiComponent[] { primary });
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
				.NumericOnly()
				.CharLimit(5)
				.OnValueChanged(x => m_weatherSunIntensity = x, isDelayed: false)
				.Width(80));
			intensityRow.Add(new Label(L("Rain")).UpperCase(false).Width(40));
			intensityRow.Add(new TextField()
				.Text(m_weatherRainIntensity)
				.NumericOnly()
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

			Row materials = CenteredRow();
			materials.Add(BuildAsteroidMaterialDropdown(1, "Material 1", m_selectedAsteroidMaterial1Id, m_selectedAsteroidMaterial1Name, m_showAsteroidMaterial1Dropdown).Width(390));
			materials.Add(BuildAsteroidMaterialDropdown(2, "Material 2", m_selectedAsteroidMaterial2Id, m_selectedAsteroidMaterial2Name, m_showAsteroidMaterial2Dropdown).Width(390));
			FinishCenteredRow(materials);

			Row create = CenteredRow();
			create.Add(new Label(L("Radius")).UpperCase(false).Width(70));
			create.Add(new TextField()
				.Text(m_asteroidRadius)
				.NumericOnly()
				.CharLimit(4)
				.OnValueChanged(x => m_asteroidRadius = x, isDelayed: false)
				.Width(80));
			create.Add(new Label(L("M1:M2 ratio")).UpperCase(false).Width(100));
			create.Add(new TextField()
				.Text(m_asteroidMaterialRatio)
				.NumericOnly()
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
				.NumericOnly()
				.CharLimit(6)
				.OnValueChanged(x => m_asteroidLandingX = x, isDelayed: false)
				.Width(90));
			landing.Add(new Label(L("Y")).UpperCase(false).Width(30));
			landing.Add(new TextField()
				.Text(m_asteroidLandingY)
				.NumericOnly()
				.CharLimit(6)
				.OnValueChanged(x => m_asteroidLandingY = x, isDelayed: false)
				.Width(90));
			landing.Add(ActionButton("Land selected asteroid", RunDropAsteroid, Button.Warning).Width(210));
			FinishCenteredRow(landing);

			Row filter = CenteredRow();
			filter.Add(new Label(L("Search materials")).UpperCase(false).Width(130));
			TextField materialFilterField = new TextField()
				.Text(m_asteroidMaterialFilter)
				.OnValueChanged(delegate(string x) {
					if (!string.IsNullOrEmpty(x)) {
						m_asteroidMaterialFilterWasEntered = true;
					}
					m_asteroidMaterialFilter = x;
					RefreshWindow();
				}, isDelayed: false)
				.Width(260);
			if (!m_asteroidMaterialFilterWasEntered) {
				materialFilterField.Placeholder(L("id or name"));
			}
			filter.Add(materialFilterField);
			FinishCenteredRow(filter);

			Column body = new Column(8);
			body.Add(materials);
			if (m_showAsteroidMaterial1Dropdown || m_showAsteroidMaterial2Dropdown) {
				body.Add(BuildAsteroidMaterialDropdownList(m_showAsteroidMaterial1Dropdown ? 1 : 2));
			}
			body.Add(create);
			body.Add(landing);
			body.Add(filter);

			panel.BodyAdd(new UiComponent[] { body });
			return panel;
		}

		private UiComponent BuildAsteroidMaterialDropdown(int slot, string label, string selectedId, string selectedName, bool isOpen) {
			Column column = new Column(4);
			Row title = new Row(6);
			title.Add(new Label(L(label)).UpperCase(false).Width(85));
			if (slot == 2) {
				title.Add(ActionButton("Clear", delegate {
					m_selectedAsteroidMaterial2Id = "";
					m_selectedAsteroidMaterial2Name = "";
					m_showAsteroidMaterial2Dropdown = false;
					RefreshWindow();
				}, Button.General).Width(80));
			}
			column.Add(title);

			Row selector = new Row(8);
			ProductProto selectedProduct = GetAsteroidMaterialProduct(selectedId);
			selector.Add(selectedProduct != null
				? (UiComponent)new Icon(selectedProduct, noTooltip: true, noRightClick: true).Size(24)
				: new Icon(ICON_EMPTY).Size(24));
			selector.Add(new ButtonText(Button.Area, L(string.IsNullOrEmpty(selectedName) ? "None" : selectedName), delegate {
				m_showAsteroidMaterial1Dropdown = slot == 1 && !isOpen;
				m_showAsteroidMaterial2Dropdown = slot == 2 && !isOpen;
				RefreshWindow();
			}).Width(330));
			column.Add(selector);
			return column;
		}

		private UiComponent BuildAsteroidMaterialDropdownList(int slot) {
			PanelWithHeader panel = new PanelWithHeader(L(slot == 1 ? "SELECT MATERIAL 1" : "SELECT MATERIAL 2"));

			Column list = new Column(6);
			if (slot == 2) {
				Row none = CenteredRow();
				none.Add(new Icon(ICON_EMPTY).Size(24));
				none.Add(new ButtonText(Button.Area, L("None"), delegate {
					m_selectedAsteroidMaterial2Id = "";
					m_selectedAsteroidMaterial2Name = "";
					m_showAsteroidMaterial2Dropdown = false;
					RefreshWindow();
				}).Width(420));
				FinishCenteredRow(none);
				list.Add(none);
			}

			foreach (AsteroidMaterialRow material in s_sandboxFeatures.GetAsteroidMaterialRows(m_asteroidMaterialFilter)) {
				list.Add(BuildAsteroidMaterialOptionRow(material, slot));
			}

			ScrollColumn scroll = new ScrollColumn();
			scroll.ScrollerAlwaysVisible();
			scroll.Height(230);
			scroll.Add(list);
			panel.BodyAdd(new UiComponent[] { scroll });
			return panel;
		}

		private UiComponent BuildAsteroidMaterialOptionRow(AsteroidMaterialRow material, int slot) {
			Row row = CenteredRow();
			row.Add(new Icon(material.Product, noTooltip: true, noRightClick: true).Size(24));
			row.Add(new ButtonText(Button.Area, L(material.Name), delegate {
				SelectAsteroidMaterial(material, slot);
				RefreshWindow();
			}).Width(300));
			row.Add(new Label(L(material.Id)).TinyFontSize().Width(220));
			row.Add(new Label(L(material.IsFiller ? "Filler" : "Ore")).Width(70));
			FinishCenteredRow(row);
			return row;
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
				.NumericOnly()
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
				.NumericOnly()
				.CharLimit(6)
				.OnValueChanged(x => m_vehicleLimitAmount = x, isDelayed: false)
				.Width(90));
			vehicles.Add(ActionButton("Increase limit", RunIncreaseVehicleLimit, Button.General).Width(170));
			FinishCenteredRow(vehicles);

			panel.BodyAdd(new UiComponent[] { actions, grid, world, vehicles });
			return panel;
		}

		private UiComponent BuildSandboxPopulationDeck() {
			PanelWithHeader panel = new PanelWithHeader(L("POPULATION"));

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

		private void SelectProduct(ProductProto product) {
			m_selectedProductId = GetProductId(product);
			m_selectedProductName = GetProductName(product);
		}

		private ProductProto GetAsteroidMaterialProduct(string materialId) {
			if (string.IsNullOrEmpty(materialId)) {
				return null;
			}
			foreach (AsteroidMaterialRow material in s_sandboxFeatures.GetAsteroidMaterialRows("")) {
				if (material.Id == materialId) {
					return material.Product;
				}
			}
			return null;
		}

		private void SelectAsteroidMaterial(AsteroidMaterialRow material, int slot) {
			if (slot == 1) {
				m_selectedAsteroidMaterial1Id = material.Id;
				m_selectedAsteroidMaterial1Name = material.Name;
				m_showAsteroidMaterial1Dropdown = false;
				return;
			}
			m_selectedAsteroidMaterial2Id = material.Id;
			m_selectedAsteroidMaterial2Name = material.Name;
			m_showAsteroidMaterial2Dropdown = false;
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
