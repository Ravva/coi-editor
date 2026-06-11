using System;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Core.Prototypes;
using Mafi.Core.Trains;

namespace ResourceQuantityEditor {

	public sealed class LocomotiveEditorService {
		private readonly ProtosDb m_protosDb;
		private readonly TrainsManager m_trainsManager;

		public LocomotiveEditorService(ProtosDb protosDb, TrainsManager trainsManager) {
			m_protosDb = protosDb;
			m_trainsManager = trainsManager;
			UpdateGlobalMaxSpeedLimit();
			OverwriteSafetyMargins();
			OverwritePushPullPenalties();
		}

		private static void OverwriteSafetyMargins() {
			try {
				SetStaticField(typeof(Train), "BRAKING_SAFETY_MARGIN", (Fix32)1.10f);
				SetStaticField(typeof(Train), "FOLLOWING_SAFETY_MARGIN", (Fix32)0.20f);
				SetStaticField(typeof(Train), "RESERVE_EXTRA_FACTOR_MULT", (Fix32)1.10f);
				Mafi.Log.Info("LocomotiveEditorService: Successfully set train safety margins to tight values.");
			} catch (Exception ex) {
				Mafi.Log.Error("LocomotiveEditorService: Failed to overwrite safety margins: " + ex);
			}
		}

		private static void OverwritePushPullPenalties() {
			try {
				SetStaticField(typeof(TrainStaticData), "PUSH_PULL_WAGONS_MULT", Percent.Hundred);
				SetStaticField(typeof(TrainStaticData), "PUSHED_WAGON_POWER_PENALTY", Percent.Zero);
				Mafi.Log.Info("LocomotiveEditorService: Successfully disabled push/pull train penalties.");
			} catch (Exception ex) {
				Mafi.Log.Error("LocomotiveEditorService: Failed to overwrite push/pull penalties: " + ex);
			}
		}

		private static void SetStaticField(Type type, string name, object value) {
			try {
				FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null) {
					try {
						FieldInfo attributesField = typeof(FieldInfo).GetField("m_fieldAttributes", BindingFlags.Instance | BindingFlags.NonPublic);
						if (attributesField != null) {
							FieldAttributes attrs = field.Attributes;
							attrs &= ~FieldAttributes.InitOnly;
							attributesField.SetValue(field, attrs);
						}
					} catch {
					}
					field.SetValue(null, value);
				}
			} catch (Exception ex) {
				Mafi.Log.Error("Failed to set static field " + name + " on type " + type.Name + ": " + ex);
			}
		}

		public void UpdateGlobalMaxSpeedLimit() {
			try {
				float maxSpeed = 144f;
				foreach (var loco in m_protosDb.All<LocomotiveProto>()) {
					if (loco.GetType() == typeof(LocomotiveProto)) {
						float speed = GetMaxSpeedKmh(loco);
						if (speed > maxSpeed) {
							maxSpeed = speed;
						}
					}
				}
				OverwriteMaxSpeedLimit(maxSpeed);
			} catch (Exception ex) {
				Mafi.Log.Error("Failed to update global max speed: " + ex);
			}
		}

		private static void OverwriteMaxSpeedLimit(float kmh) {
			RelTile1f newSpeed = RelTile1fExtensions.Kmh((double)kmh);
			SetStaticField(typeof(Train), "MAX_SPEED", newSpeed);
			Mafi.Log.Info("LocomotiveEditorService: Successfully set Train.MAX_SPEED to " + kmh + " km/h");
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

		public void UpdateActiveTrains() {
			try {
				if (m_trainsManager == null || m_trainsManager.Trains == null) return;

				FieldInfo maxSpeedField = typeof(Train).GetField("<MaxSpeed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
				FieldInfo resDistField = typeof(Train).GetField("<AttemptedReservationDistance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

				foreach (var train in m_trainsManager.Trains) {
					if (train == null || train.Data == null) continue;

					// 1. Recompute performance curves based on new prototype values
					typeof(TrainStaticData).GetMethod("recomputeSpeeds", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						?.Invoke(train.Data, null);

					// 2. Update train max speed field
					if (maxSpeedField != null) {
						maxSpeedField.SetValue(train, train.Data.MaxSpeedBasedOnConstruction);
					}

					// 3. Re-calculate attempted reservation distance based on new braking specs
					if (resDistField != null) {
						RelTile1f stoppingDistance = train.Data.ComputeStoppingDistanceEstimate(
							train.Data.MaxSpeedBasedOnConstruction,
							train.Data.MaxBrakingForceKn,
							train.Data.MassTonsWhenFull);
						RelTile1f resDist = stoppingDistance * (Fix32)1.10f;
						resDistField.SetValue(train, resDist);
					}
				}
				Mafi.Log.Info("LocomotiveEditorService: Successfully updated " + m_trainsManager.Trains.Count + " active trains.");
			} catch (Exception ex) {
				Mafi.Log.Error("LocomotiveEditorService: Failed to update active trains: " + ex);
			}
		}

		public float GetMaxSpeedKmh(LocomotiveProto proto) {
			FieldInfo field = FindField(typeof(TrainCarBaseProto), "MaxSpeed");
			if (field == null) return 0f;
			RelTile1f speed = (RelTile1f)field.GetValue(proto);
			Fix32 kmh = speed.SpeedTilesPerTickToKmPerHour();
			return kmh.RawValue / 1024f;
		}

		public bool SetMaxSpeedKmh(LocomotiveProto proto, float kmh) {
			FieldInfo field = FindField(typeof(TrainCarBaseProto), "MaxSpeed");
			if (field == null) return false;
			RelTile1f newSpeed = RelTile1fExtensions.Kmh((double)kmh);
			field.SetValue(proto, newSpeed);

			// Also update all tenders and cargo wagons to have at least this speed
			// so they don't bottleneck the train!
			try {
				var cars = new System.Collections.Generic.List<TrainCarBaseProto>();
				try { cars.AddRange(m_protosDb.All<LocomotiveProto>()); } catch {}
				try { cars.AddRange(m_protosDb.All<TenderWagonProto>()); } catch {}
				try { cars.AddRange(m_protosDb.All<CargoWagonProto>()); } catch {}
				try { cars.AddRange(m_protosDb.All<CargoWagonLooseProto>()); } catch {}
				try { cars.AddRange(m_protosDb.All<CargoWagonMoltenProto>()); } catch {}
				try { cars.AddRange(m_protosDb.All<CargoWagonUnitProto>()); } catch {}

				foreach (var car in cars) {
					if (car.GetType() != typeof(LocomotiveProto)) {
						RelTile1f currentCarSpeed = (RelTile1f)field.GetValue(car);
						Fix32 carKmh = currentCarSpeed.SpeedTilesPerTickToKmPerHour();
						float carKmhFloat = carKmh.RawValue / 1024f;
						if (carKmhFloat < kmh) {
							field.SetValue(car, newSpeed);
						}
					}
				}
			} catch (Exception ex) {
				Mafi.Log.Error("Failed to update wagon speeds: " + ex);
			}

			RelTile1f readBack = (RelTile1f)field.GetValue(proto);
			Fix32 readKmh = readBack.SpeedTilesPerTickToKmPerHour();
			float actual = readKmh.RawValue / 1024f;
			
			// Dynamically update global Train.MAX_SPEED based on all locomotive configurations
			UpdateGlobalMaxSpeedLimit();
			
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
