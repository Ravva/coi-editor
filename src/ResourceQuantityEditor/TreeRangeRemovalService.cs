using System;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Trees;
using Mafi.Unity.InputControl.AreaTool;
using Mafi.Unity.Terrain;
using Mafi.Unity.Trees;

namespace ResourceQuantityEditor {

	public sealed class TreeRangeRemovalService {
		private static readonly RelTile1i MAX_AREA_EDGE_SIZE = new RelTile1i(512);

		private readonly TreesManager m_treesManager;
		private readonly TreesRenderer m_treesRenderer;
		private readonly AreaSelectionTool m_areaSelectionTool;
		private readonly Set<TreeId> m_selectedTrees;
		private readonly Lyst<TreeId> m_selectedTreesTmp;

		private bool m_isActive;

		public TreeRangeRemovalService(
			TreesManager treesManager,
			TreesRenderer treesRenderer,
			AreaSelectionToolFactory areaSelectionToolFactory,
			NewInstanceOf<TerrainAreaOutlineRenderer> terrainOutlineRenderer) {
			m_treesManager = treesManager;
			m_treesRenderer = treesRenderer;
			m_areaSelectionTool = areaSelectionToolFactory.CreateInstance(
				terrainOutlineRenderer.Instance,
				UpdateSelectionSync,
				SelectionDone,
				CancelSelection,
				CancelSelection);
			m_areaSelectionTool.SetEdgeSizeLimit(MAX_AREA_EDGE_SIZE);
			m_areaSelectionTool.SetLeftClickColor(TreeHarvestingHighlightManager.MARKED_FOR_HARVEST_COLOR);
			m_selectedTrees = new Set<TreeId>();
			m_selectedTreesTmp = new Lyst<TreeId>();
		}

		public void BeginRemoveSelection() {
			CancelSelection();
			m_isActive = true;
			m_areaSelectionTool.TerrainCursor.Activate();
			m_areaSelectionTool.Activate(additionMode: true);
		}

		private void SelectionDone(RectangleTerrainArea2i area, bool leftClick) {
			try {
				m_selectedTreesTmp.Clear();
				m_selectedTreesTmp.AddRange(m_selectedTrees);
				ClearSelectionHighlights();

				Lyst<TreeId>.Enumerator enumerator = m_selectedTreesTmp.GetEnumerator();
				while (enumerator.MoveNext()) {
					m_treesManager.TryRemoveTree(enumerator.Current, skipAddingStump: true);
				}
			} finally {
				m_selectedTreesTmp.Clear();
				DeactivateTool();
			}
		}

		private void UpdateSelectionSync(RectangleTerrainArea2i area, bool leftClick) {
			ClearSelectionHighlights();
			if (!m_isActive || area.IsEmpty) {
				return;
			}

			foreach (TreeId treeId in m_treesManager.EnumerateTreesInArea(PolygonTerrainArea2i.FromRectArea(area))) {
				m_selectedTrees.Add(treeId);
				m_treesRenderer.AddHighlight(treeId, TreeHarvestingHighlightManager.MARKED_FOR_HARVEST_COLOR);
			}
		}

		private void CancelSelection() {
			ClearSelectionHighlights();
			DeactivateTool();
		}

		private void DeactivateTool() {
			if (!m_isActive) {
				return;
			}
			m_isActive = false;
			m_areaSelectionTool.Deactivate();
			m_areaSelectionTool.TerrainCursor.Deactivate();
		}

		private void ClearSelectionHighlights() {
			foreach (TreeId treeId in m_selectedTrees) {
				m_treesRenderer.RemoveHighlight(treeId, TreeHarvestingHighlightManager.MARKED_FOR_HARVEST_COLOR, isStump: false, ignoreDestroyedTrees: true);
			}
			m_selectedTrees.Clear();
		}
	}
}
