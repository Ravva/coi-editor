using System;
using System.Reflection;
using Mafi.Unity.Camera;
using Mafi.Unity.Weather;

namespace ResourceQuantityEditor {

	public sealed class VisualTweaksService {
		private readonly FogController m_fogController;
		private readonly WeatherController m_weatherController;

		public bool CloudsEnabled { get; private set; }
		public bool FogEnabled { get; private set; }
		public bool WeatherEffectsEnabled { get; private set; }

		public VisualTweaksService(FogController fogController, WeatherController weatherController) {
			m_fogController = fogController;
			m_weatherController = weatherController;

			CloudsEnabled = true;
			FogEnabled = true;
			WeatherEffectsEnabled = true;
		}

		public void SetCloudsEnabled(bool enabled) {
			m_weatherController.SetCloudsEnabled(enabled);
			CloudsEnabled = enabled;
		}

		public void SetFogRendering(bool enabled) {
			InvokeNonPublic(m_fogController, "SetFogRenderingState", enabled);
			FogEnabled = enabled;
		}

		public void SetWeatherEffectsVisible(bool enabled) {
			m_weatherController.SetWeatherEffectsVisibility(enabled);
			WeatherEffectsEnabled = enabled;
		}

		private static void InvokeNonPublic(object target, string methodName, params object[] args) {
			MethodInfo method = target.GetType().GetMethod(
				methodName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null) {
				throw new MissingMethodException(target.GetType().FullName, methodName);
			}

			method.Invoke(target, args);
		}
	}
}
