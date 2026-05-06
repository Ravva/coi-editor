using System;
using System.Reflection;
using Mafi;
using Mafi.Core;
using Mafi.Core.Terrain;

namespace ResourceQuantityEditor
{
    public static class UnlimitedDesignationsPatch
    {
        private static RelTile1i _originalTerrainAdd;
        private static RelTile1i _originalTerrainRemove;
        private static RelTile1i _originalSurfaceAdd;
        private static RelTile1i _originalSurfaceRemove;
        private static RelTile1i _originalTreeEdgeSize;

        private static object _originalMaxEdgeDistance;
        private static object _originalMaxEdgeDistanceSqr;

        private static readonly BindingFlags FieldFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static bool _initialized;

        public static void Initialize()
        {
            _originalTerrainAdd = GetStaticFieldValue<RelTile1i>("Mafi.Unity.Ui.Controllers.Designations.TerrainDesignationController", "MAX_AREA_SIZE_ADD");
            _originalTerrainRemove = GetStaticFieldValue<RelTile1i>("Mafi.Unity.Ui.Controllers.Designations.TerrainDesignationController", "MAX_AREA_SIZE_REMOVE");
            _originalSurfaceAdd = GetStaticFieldValue<RelTile1i>("Mafi.Unity.Ui.Controllers.SurfaceDesignationController", "MAX_AREA_SIZE_ADD");
            _originalSurfaceRemove = GetStaticFieldValue<RelTile1i>("Mafi.Unity.Ui.Controllers.SurfaceDesignationController", "MAX_AREA_SIZE_REMOVE");
            _originalTreeEdgeSize = GetStaticFieldValue<RelTile1i>("Mafi.Unity.Ui.Controllers.TreeHarvestingDesignatorController", "MAX_AREA_EDGE_SIZE");

            _originalMaxEdgeDistance = GetStaticFieldValue<object>("Mafi.Unity.Ui.Controllers.PolygonAreaSelectionController", "MAX_EDGE_DISTANCE");
            _originalMaxEdgeDistanceSqr = GetStaticFieldValue<object>("Mafi.Unity.Ui.Controllers.PolygonAreaSelectionController", "MAX_EDGE_DISTANCE_SQR");

            _initialized = true;
        }

        public static void SetUnlimitedMining(bool enabled)
        {
            RelTile1i addValue = enabled ? new RelTile1i(int.MaxValue) : (_initialized ? _originalTerrainAdd : new RelTile1i(127));
            RelTile1i removeValue = enabled ? new RelTile1i(int.MaxValue) : (_initialized ? _originalTerrainRemove : new RelTile1i(191));
            RelTile1i treeValue = enabled ? new RelTile1i(int.MaxValue) : (_initialized ? _originalTreeEdgeSize : new RelTile1i(191));

            SetStaticField("Mafi.Unity.Ui.Controllers.Designations.TerrainDesignationController", "MAX_AREA_SIZE_ADD", addValue);
            SetStaticField("Mafi.Unity.Ui.Controllers.Designations.TerrainDesignationController", "MAX_AREA_SIZE_REMOVE", removeValue);
            SetStaticField("Mafi.Unity.Ui.Controllers.SurfaceDesignationController", "MAX_AREA_SIZE_ADD", addValue);
            SetStaticField("Mafi.Unity.Ui.Controllers.SurfaceDesignationController", "MAX_AREA_SIZE_REMOVE", removeValue);
            SetStaticField("Mafi.Unity.Ui.Controllers.TreeHarvestingDesignatorController", "MAX_AREA_EDGE_SIZE", treeValue);
        }

        public static void SetUnlimitedTowerArea(bool enabled)
        {
            Type fix64Type = Type.GetType("Mafi.Fix64, Mafi");
            if (fix64Type == null)
                return;

            MethodInfo fromInt = fix64Type.GetMethod("FromInt", BindingFlags.Public | BindingFlags.Static);
            if (fromInt == null)
                return;

            object distanceValue;
            object distanceSqrValue;

            if (enabled)
            {
                distanceValue = fromInt.Invoke(null, new object[] { 10000 });
                distanceSqrValue = fromInt.Invoke(null, new object[] { 100000000 });
            }
            else
            {
                distanceValue = _originalMaxEdgeDistance ?? fromInt.Invoke(null, new object[] { 2 });
                distanceSqrValue = _originalMaxEdgeDistanceSqr ?? fromInt.Invoke(null, new object[] { 4 });
            }

            SetStaticField("Mafi.Unity.Ui.Controllers.PolygonAreaSelectionController", "MAX_EDGE_DISTANCE", distanceValue);
            SetStaticField("Mafi.Unity.Ui.Controllers.PolygonAreaSelectionController", "MAX_EDGE_DISTANCE_SQR", distanceSqrValue);
        }

        private static T GetStaticFieldValue<T>(string typeName, string fieldName)
        {
            try
            {
                Type type = Type.GetType(typeName);
                if (type == null)
                    return default(T);

                FieldInfo field = type.GetField(fieldName, FieldFlags);
                if (field == null)
                    return default(T);

                return (T)field.GetValue(null);
            }
            catch
            {
                return default(T);
            }
        }

        private static void SetStaticField(string typeName, string fieldName, object value)
        {
            try
            {
                Type type = Type.GetType(typeName);
                if (type == null)
                    return;

                FieldInfo field = type.GetField(fieldName, FieldFlags);
                if (field == null)
                    return;

                field.SetValue(null, value);
            }
            catch
            {
                // Silently ignore missing fields or version mismatches
            }
        }
    }
}
