using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mafi;
using Mafi.Core.Prototypes;
using Mafi.Core.Trains;

namespace ResourceQuantityEditor {

	public sealed class LocomotiveEditorService {
		private readonly ProtosDb m_protosDb;
		private readonly System.Collections.Generic.Dictionary<string, Action<object, object>> m_setters
			= new System.Collections.Generic.Dictionary<string, Action<object, object>>();

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
			object val = Traverse.Create(proto).Field(fieldName).GetValue();
			return ConvertToFloat(val);
		}

		public void SetFieldFloat(object proto, string fieldName, float value) {
			var targetType = FindFieldType(proto.GetType(), fieldName);
			if (targetType == null) return;
			object converted = ConvertFromFloat(value, targetType);
			SetFieldValue(proto, fieldName, converted);
		}

		public float GetMaxSpeedKmh(LocomotiveProto proto) {
			RelTile1f speed = Traverse.Create(proto).Field("MaxSpeed").GetValue<RelTile1f>();
			Fix32 kmh = speed.SpeedTilesPerTickToKmPerHour();
			return kmh.RawValue / 1024f;
		}

		public void SetMaxSpeedKmh(LocomotiveProto proto, float kmh) {
			RelTile1f newSpeed = RelTile1fExtensions.Kmh((double)kmh);
			SetFieldValue(proto, "MaxSpeed", newSpeed);
		}

		private void SetFieldValue(object proto, string fieldName, object value) {
			if (!m_setters.TryGetValue(fieldName, out var setter)) {
				setter = CreateFieldSetter(proto.GetType(), fieldName);
				m_setters[fieldName] = setter;
			}
			if (setter != null) {
				setter(proto, value);
			}
		}

		private static Action<object, object> CreateFieldSetter(Type declaringType, string fieldName) {
			FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null && declaringType.BaseType != null) {
				field = declaringType.BaseType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (field == null) return null;

			var dm = new DynamicMethod("Set_" + fieldName, typeof(void), new[] { typeof(object), typeof(object) }, declaringType.Module, true);
			var il = dm.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Castclass, declaringType);
			il.Emit(OpCodes.Ldarg_1);
			if (field.FieldType.IsValueType) {
				il.Emit(OpCodes.Unbox_Any, field.FieldType);
			} else {
				il.Emit(OpCodes.Castclass, field.FieldType);
			}
			il.Emit(OpCodes.Stfld, field);
			il.Emit(OpCodes.Ret);
			return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
		}

		private static Type FindFieldType(Type type, string fieldName) {
			FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null && type.BaseType != null) {
				field = type.BaseType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			return field?.FieldType ?? typeof(float);
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

			try {
				PropertyInfo prop = type.GetProperty("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (prop != null) {
					object inner = prop.GetValue(val);
					return ConvertToFloat(inner);
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
