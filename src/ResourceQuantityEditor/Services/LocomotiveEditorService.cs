using System;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Core.Prototypes;
using Mafi.Core.Trains;

namespace ResourceQuantityEditor {

	public sealed class LocomotiveEditorService {
		private readonly ProtosDb m_protosDb;

		public LocomotiveEditorService(ProtosDb protosDb) {
			m_protosDb = protosDb;
		}

		public LocomotiveProto[] GetAllLocomotives() {
			return m_protosDb.All<LocomotiveProto>()
				.Where(p => p.GetType() == typeof(LocomotiveProto))
				.OrderBy(x => x.Id.ToString())
				.ToArray();
		}

		public float GetFieldFloat(object proto, string fieldName) {
			FieldInfo field = FindField(proto.GetType(), fieldName);
			if (field == null) return 0f;
			return ConvertToFloat(field.GetValue(proto));
		}

		public bool SetFieldFloat(object proto, string fieldName, float value) {
			FieldInfo field = FindField(proto.GetType(), fieldName);
			if (field == null) return false;
			object converted = ConvertFromFloat(value, field.FieldType);
			field.SetValue(proto, converted);
			object readBack = field.GetValue(proto);
			if (readBack == null) return false;
			float readVal = ConvertToFloat(readBack);
			return Math.Abs(readVal - value) < 1f;
		}

		public float GetMaxSpeedKmh(LocomotiveProto proto) {
			FieldInfo field = FindField(typeof(LocomotiveProto), "MaxSpeed");
			if (field == null) return 0f;
			RelTile1f speed = (RelTile1f)field.GetValue(proto);
			Fix32 kmh = speed.SpeedTilesPerTickToKmPerHour();
			return kmh.RawValue / 1024f;
		}

		public bool SetMaxSpeedKmh(LocomotiveProto proto, float kmh) {
			FieldInfo field = FindField(typeof(LocomotiveProto), "MaxSpeed");
			if (field == null) return false;
			RelTile1f newSpeed = RelTile1fExtensions.Kmh((double)kmh);
			field.SetValue(proto, newSpeed);
			RelTile1f readBack = (RelTile1f)field.GetValue(proto);
			Fix32 readKmh = readBack.SpeedTilesPerTickToKmPerHour();
			float actual = readKmh.RawValue / 1024f;
			return Math.Abs(actual - kmh) < 1f;
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

			if (type == typeof(Fix32)) {
				int raw = ((Fix32)val).RawValue;
				return raw / 1024f;
			}

			try {
				return System.Convert.ToSingle(val);
			} catch {
			}

			try {
				FieldInfo[] allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				for (int i = 0; i < allFields.Length; i++) {
					try {
						object inner = allFields[i].GetValue(val);
						return ConvertToFloat(inner);
					} catch {
					}
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
			string name = targetType.Name;
			if (name == "RelTile1f") {
				return Activator.CreateInstance(targetType, new object[] { (Fix32)value });
			}
			if (name == "MechPower") {
				return Activator.CreateInstance(targetType, new object[] { (int)value });
			}
			try {
				return System.Convert.ChangeType(value, targetType);
			} catch {
				return value;
			}
		}
	}
}
