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
			OverwriteAerodynamics();
		}

		private static void OverwriteSafetyMargins() {
			try {
				SetStaticField(typeof(Train), "BRAKING_SAFETY_MARGIN", (Fix32)1.00f);
				SetStaticField(typeof(Train), "FOLLOWING_SAFETY_MARGIN", (Fix32)0.20f);
				// Increase alignment speed so trains don't crawl at 54 km/h near stations
				RelTile1f highAlignmentSpeed = RelTile1fExtensions.Kmh(200.0);
				SetStaticField(typeof(Train), "MAX_ALIGNMENT_SPEED", highAlignmentSpeed);
				Mafi.Log.Info("LocomotiveEditorService: Successfully set train safety margins and alignment speed.");
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

		/// <summary>
		/// Reduce air drag so that physics simulation does not cap speed below the desired maximum.
		/// AIR_DRAG_FUN_MULTIPLIER defaults to 50% — lowering it to near-zero removes the
		/// aerodynamic terminal velocity that otherwise limits trains to ~150 km/h.
		/// </summary>
		private static void OverwriteAerodynamics() {
			try {
				SetStaticField(typeof(TrainStaticData), "AIR_DRAG_FUN_MULTIPLIER", Percent.FromRatio(5, 100));
				SetStaticField(typeof(TrainStaticData), "ACCELERATION_FUN_FACTOR", Percent.FromRatio(500, 100));
				Mafi.Log.Info("LocomotiveEditorService: Successfully reduced air drag and boosted acceleration.");
			} catch (Exception ex) {
				Mafi.Log.Error("LocomotiveEditorService: Failed to overwrite aerodynamics: " + ex);
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

		private void RobustSetField(object obj, string baseName, float value) {
			Type t = obj.GetType();
			FieldInfo f = FindFieldDeep(t, baseName);
			if (f == null) f = FindFieldDeep(t, "<" + baseName + ">k__BackingField");
			if (f == null) f = FindFieldDeep(t, baseName + "Kn");
			if (f == null) f = FindFieldDeep(t, "<" + baseName + "Kn>k__BackingField");
			if (f == null) f = FindFieldDeep(t, baseName + "Kw");
			if (f == null) f = FindFieldDeep(t, "<" + baseName + "Kw>k__BackingField");
			if (f != null) {
				object converted = ConvertFromFloat(value, f.FieldType);
				f.SetValue(obj, converted);
			}
		}

		private float RobustGetField(object obj, string baseName, float defaultVal = 0f) {
			Type t = obj.GetType();
			FieldInfo f = FindFieldDeep(t, baseName);
			if (f == null) f = FindFieldDeep(t, "<" + baseName + ">k__BackingField");
			if (f == null) f = FindFieldDeep(t, baseName + "Kn");
			if (f == null) f = FindFieldDeep(t, "<" + baseName + "Kn>k__BackingField");
			if (f == null) f = FindFieldDeep(t, baseName + "Kw");
			if (f == null) f = FindFieldDeep(t, "<" + baseName + "Kw>k__BackingField");
			if (f != null) {
				var val = f.GetValue(obj);
				if (val != null) {
					return ConvertToFloat(val);
				}
			}
			return defaultVal;
		}

		public void UpdateGlobalMaxSpeedLimit() {
			try {
				float maxSpeed = 144f;
				
				// Apply 200 km/h and massive boosts to ALL TrainCarProtos (Locomotives + Wagons)
				foreach (var proto in m_protosDb.All<Proto>()) {
					if (proto is TrainCarBaseProto carProto) {
						// 1. Force MaxSpeed to 200 km/h
						float speed = RobustGetField(carProto, "MaxSpeed");
						if (speed < 200f) {
							RobustSetField(carProto, "MaxSpeed", 200f);
						}
						
						// 2. Boost Brakes so loaded trains can stop
						float brake = RobustGetField(carProto, "BrakingForceKn");
						if (brake > 0f && brake < 500f) {
							RobustSetField(carProto, "BrakingForceKn", brake * 50f);
						}
						
						// 3. Boost Power/Traction (for locomotives)
						if (carProto is LocomotiveProto locoProto) {
							float power = RobustGetField(locoProto, "EnginePowerKw");
							if (power > 0f && power < 10000f) {
								RobustSetField(locoProto, "EnginePowerKw", power * 50f);
							}
							
							float tractive = RobustGetField(locoProto, "StartingTractiveEffort");
							if (tractive > 0f && tractive < 1000f) {
								RobustSetField(locoProto, "StartingTractiveEffort", tractive * 50f);
							}
						}
					}
				}

				// Track global max speed
				foreach (var loco in m_protosDb.All<LocomotiveProto>()) {
					if (loco.GetType() == typeof(LocomotiveProto)) {
						float speed = GetMaxSpeedKmh(loco);
						if (speed > maxSpeed) {
							maxSpeed = speed;
						}
					}
				}

				OverwriteMaxSpeedLimit(maxSpeed);
				UpdateTrackSpeedLimits(maxSpeed);
			} catch (Exception ex) {
				Mafi.Log.Error("Failed to update global max speed: " + ex);
			}
		}

		/// <summary>
		/// Overwrite MaxSpeedTilesPerTick on every track and level-crossing prototype in the DB
		/// so that track geometry never limits train speed below the target.
		/// Uses FindFieldDeep to traverse the full inheritance chain.
		/// </summary>
		public void UpdateTrackSpeedLimits(float maxSpeedKmh) {
			try {
				RelTile1f targetSpeedTpt = RelTile1fExtensions.Kmh((double)maxSpeedKmh);
				int count = 0;

				var tracks = m_protosDb.All<Proto>().OfType<IEntityWithTrainTrackBaseProto>().ToList();

				foreach (var proto in tracks) {


					FieldInfo maxSpeedField = FindFieldDeep(proto.GetType(), "<MaxSpeedTilesPerTick>k__BackingField");
					if (maxSpeedField != null) {
						RelTile1f currentSpeed = (RelTile1f)maxSpeedField.GetValue(proto);
						float currentSpeedKmh = currentSpeed.SpeedTilesPerTickToKmPerHour().RawValue / 1024f;
						if (currentSpeedKmh < maxSpeedKmh) {
							maxSpeedField.SetValue(proto, targetSpeedTpt);
							count++;
						}
					}
				}
				Mafi.Log.Info("LocomotiveEditorService: Successfully set " + count + " track speed limits to " + maxSpeedKmh + " km/h");
			} catch (Exception ex) {
				Mafi.Log.Error("LocomotiveEditorService: Failed to update track speed limits: " + ex);
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

				// Re-apply aerodynamics overrides (in case anything reset them)
				OverwritePushPullPenalties();
				OverwriteAerodynamics();

				FieldInfo maxSpeedField = typeof(Train).GetField("<MaxSpeed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

				// TrainStaticData fields
				FieldInfo tsdMaxSpeedConstField = typeof(TrainStaticData).GetField("MaxSpeedBasedOnConstruction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdTotalPowerField = typeof(TrainStaticData).GetField("TotalMaxPower", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdTotalPowerFwdField = typeof(TrainStaticData).GetField("TotalMaxPowerForwards", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdTotalPowerBwdField = typeof(TrainStaticData).GetField("TotalMaxPowerBackwards", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdStartingTractiveField = typeof(TrainStaticData).GetField("StartingTractiveEffortKn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdMaxBrakingField = typeof(TrainStaticData).GetField("MaxBrakingForceKn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdAirDragCoeffField = typeof(TrainStaticData).GetField("AirDragCoefficient", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

				// Computed speed fields that recomputeSpeeds writes to
				FieldInfo tsdMaxFwdSpeedField = typeof(TrainStaticData).GetField("MaxForwardsSpeedCombined", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdMaxBwdSpeedField = typeof(TrainStaticData).GetField("MaxBackwardSpeedCombined", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdMaxSpeedG0Field = typeof(TrainStaticData).GetField("MaxSpeedAtGrade0", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdMaxSpeedG0UnrestField = typeof(TrainStaticData).GetField("MaxSpeedAtGrade0Unrestricted", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdMaxSpeedG12Field = typeof(TrainStaticData).GetField("MaxSpeedAtGrade12", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdMaxSpeedG25Field = typeof(TrainStaticData).GetField("MaxSpeedAtGrade25", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				FieldInfo tsdMaxSpeedG0BwdField = typeof(TrainStaticData).GetField("MaxSpeedAtGrade0Backwards", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

				MethodInfo recomputeSpeedsMethod = typeof(TrainStaticData).GetMethod("recomputeSpeeds", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				foreach (var train in m_trainsManager.Trains) {
					if (train == null || train.Data == null) continue;

					// 1. Calculate combined specs from prototypes in-place
					RelTile1f maxSpeed = RelTile1fExtensions.Kmh(1000f);
					int totalPower = 0;
					Fix32 totalTractive = Fix32.Zero;
					Fix32 totalBraking = Fix32.Zero;

					foreach (var car in train.Data.TrainCars) {
						if (car == null) continue;

						// Speed
						FieldInfo speedField = FindField(car.GetType(), "MaxSpeed");
						if (speedField != null) {
							RelTile1f carSpeed = (RelTile1f)speedField.GetValue(car);
							if (ConvertToFloat(carSpeed) < ConvertToFloat(maxSpeed)) {
								maxSpeed = carSpeed;
							}
						}

						// Braking
						FieldInfo brakingField = FindField(car.GetType(), "BrakingForceKn");
						if (brakingField != null) {
							totalBraking += (Fix32)brakingField.GetValue(car);
						}

						// Locomotive specs
						if (car is LocomotiveProto loco) {
							FieldInfo powerField = FindField(loco.GetType(), "EnginePowerKw");
							if (powerField != null) {
								object powerVal = powerField.GetValue(loco);
								int powerInt = (int)typeof(MechPower).GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(powerVal);
								totalPower += powerInt;
							}

							FieldInfo tractiveField = FindField(loco.GetType(), "StartingTractiveEffort");
							if (tractiveField != null) {
								totalTractive += (Fix32)tractiveField.GetValue(loco);
							}
						}
					}

					// 2. Write new values to TrainStaticData fields
					tsdMaxSpeedConstField?.SetValue(train.Data, maxSpeed);
					tsdTotalPowerField?.SetValue(train.Data, new MechPower(totalPower));
					tsdTotalPowerFwdField?.SetValue(train.Data, new MechPower(totalPower));
					tsdTotalPowerBwdField?.SetValue(train.Data, new MechPower(totalPower));
					tsdStartingTractiveField?.SetValue(train.Data, totalTractive);
					tsdMaxBrakingField?.SetValue(train.Data, totalBraking);

					// 2b. Reduce air drag coefficient on each train so physics don't cap speed
					if (tsdAirDragCoeffField != null) {
						Fix32 currentDrag = (Fix32)tsdAirDragCoeffField.GetValue(train.Data);
						Fix32 reducedDrag = currentDrag * (Fix32)0.05f;
						tsdAirDragCoeffField.SetValue(train.Data, reducedDrag);
					}

					// 3. Trigger game's recomputeSpeeds to rebuild performance curves
					recomputeSpeedsMethod?.Invoke(train.Data, null);

					// 4. Force-override all computed speed fields to at least our target speed
					//    recomputeSpeeds may have computed lower values due to residual drag/physics
					RelTile1f desiredSpeed = maxSpeed;
					ForceMinSpeed(tsdMaxSpeedConstField, train.Data, desiredSpeed);
					ForceMinSpeed(tsdMaxFwdSpeedField, train.Data, desiredSpeed);
					ForceMinSpeed(tsdMaxBwdSpeedField, train.Data, desiredSpeed);
					ForceMinSpeed(tsdMaxSpeedG0Field, train.Data, desiredSpeed);
					ForceMinSpeed(tsdMaxSpeedG0UnrestField, train.Data, desiredSpeed);
					ForceMinSpeed(tsdMaxSpeedG12Field, train.Data, desiredSpeed);
					ForceMinSpeed(tsdMaxSpeedG25Field, train.Data, desiredSpeed);
					ForceMinSpeed(tsdMaxSpeedG0BwdField, train.Data, desiredSpeed);

				// 5. Update Train instance max speed field
				if (maxSpeedField != null) {
					maxSpeedField.SetValue(train, desiredSpeed);
				}
			}
			Mafi.Log.Info("LocomotiveEditorService: Successfully updated " + m_trainsManager.Trains.Count + " active trains in-place.");
			} catch (Exception ex) {
				Mafi.Log.Error("LocomotiveEditorService: Failed to update active trains: " + ex);
			}
		}

		/// <summary>
		/// Ensures the given RelTile1f field on target is at least minSpeed.
		/// </summary>
		private static void ForceMinSpeed(FieldInfo field, object target, RelTile1f minSpeed) {
			if (field == null) return;
			try {
				RelTile1f current = (RelTile1f)field.GetValue(target);
				float currentKmh = current.SpeedTilesPerTickToKmPerHour().RawValue / 1024f;
				float minKmh = minSpeed.SpeedTilesPerTickToKmPerHour().RawValue / 1024f;
				if (currentKmh < minKmh) {
					field.SetValue(target, minSpeed);
				}
			} catch {}
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

		/// <summary>
		/// Searches for a field traversing the full type hierarchy (not just one level up).
		/// </summary>
		public static FieldInfo FindFieldDeep(Type type, string fieldName) {
			Type current = type;
			while (current != null) {
				FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				if (field != null) return field;
				current = current.BaseType;
			}
			return null;
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

			if (type.Name == "MechPower") {
				try {
					FieldInfo field = type.GetField("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (field != null) {
						return Convert.ToSingle(field.GetValue(val));
					}
				} catch {}
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
				return RelTile1fExtensions.Kmh((double)value);
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
