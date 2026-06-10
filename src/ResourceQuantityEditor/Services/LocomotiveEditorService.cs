using System;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Core.Prototypes;
using Mafi.Core.Trains;

namespace ResourceQuantityEditor {

	public sealed class LocomotiveEditorService {
		private readonly ProtosDb m_protosDb;

		private const float REL_TILE_1F_TO_KMH = 1200f;

		public LocomotiveEditorService(ProtosDb protosDb) {
			m_protosDb = protosDb;
		}

		public LocomotiveProto[] GetAllLocomotives() {
			return m_protosDb.All<LocomotiveProto>()
				.OrderBy(x => x.Id.ToString())
				.ToArray();
		}

		public float GetFieldFloat(object proto, string fieldName) {
			object val = GetRawFieldValue(proto, fieldName);
			return ConvertToFloat(val);
		}

		public void SetFieldFloat(object proto, string fieldName, float value) {
			FieldInfo field = FindField(proto.GetType(), fieldName);
			if (field == null) {
				return;
			}
			field.SetValue(proto, ConvertFromFloat(value, field.FieldType));
		}

		public float GetMaxSpeedKmh(LocomotiveProto proto) {
			object val = GetRawFieldValue(proto, "MaxSpeed");
			return ConvertToFloat(val) * REL_TILE_1F_TO_KMH;
		}

		public void SetMaxSpeedKmh(LocomotiveProto proto, float kmh) {
			FieldInfo field = FindField(typeof(LocomotiveProto), "MaxSpeed");
			if (field == null) {
				field = FindField(typeof(TrainCarBaseProto), "MaxSpeed");
			}
			if (field == null) {
				return;
			}
			field.SetValue(proto, ConvertFromFloat(kmh / REL_TILE_1F_TO_KMH, field.FieldType));
		}

		public static object GetRawFieldValue(object proto, string fieldName) {
			FieldInfo field = FindField(proto.GetType(), fieldName);
			if (field == null) {
				// Try backing field name for auto-properties
				string backing = "<" + fieldName + ">k__BackingField";
				field = FindField(proto.GetType(), backing);
			}
			return field?.GetValue(proto);
		}

		public static FieldInfo FindField(Type type, string fieldName) {
			FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null && type.BaseType != null) {
				field = type.BaseType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			return field;
		}

		public static float ConvertToFloat(object val) {
			if (val == null) {
				return 0f;
			}
			Type type = val.GetType();

			// Fix32: use RawValue / 1024 (FRACTIONAL_BITS = 10)
			if (type == typeof(Fix32)) {
				int raw = ((Fix32)val).RawValue;
				return raw / 1024f;
			}

			// Try IConvertible (float, int, double, etc.)
			try {
				return System.Convert.ToSingle(val);
			} catch {
			}

			// Find any numeric field on this struct via GetAllFields
			try {
				FieldInfo[] allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				for (int i = 0; i < allFields.Length; i++) {
					try {
						object inner = allFields[i].GetValue(val);
						return System.Convert.ToSingle(inner);
					} catch {
					}
				}
			} catch {
			}

			// Try Value property
			try {
				PropertyInfo prop = type.GetProperty("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (prop != null) {
					object inner = prop.GetValue(val);
					return System.Convert.ToSingle(inner);
				}
			} catch {
			}

			// Try ToFloat method
			try {
				MethodInfo method = type.GetMethod("ToFloat", BindingFlags.Public | BindingFlags.Instance);
				if (method != null) {
					object inner = method.Invoke(val, null);
					return System.Convert.ToSingle(inner);
				}
			} catch {
			}

			return 0f;
		}

		public static object ConvertFromFloat(float value, Type targetType) {
			if (targetType == typeof(Fix32)) {
				return (Fix32)value;
			}
			if (targetType == typeof(float)) {
				return value;
			}
			if (targetType.Name == "RelTile1f") {
				return Activator.CreateInstance(targetType, new object[] { value });
			}
			try {
				return System.Convert.ChangeType(value, targetType);
			} catch {
				return value;
			}
		}
	}
}
