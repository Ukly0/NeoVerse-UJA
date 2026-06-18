using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UJA.EditorTools
{
    public static class UJACampusLightingSetup
    {
        private const string SkyboxPath = "Assets/_Project/Art/Skybox Freebies/Skybox CasualDay.mat";
        private const string LightingSettingsPath = "Assets/_Project/Spaces/UJA/Scenes/UJACampusLightingSettings.lighting";
        private const string VolumeProfilePath = "Assets/_Project/Spaces/UJA/Scenes/UJACampusPostProcessProfile.asset";
        private const string SunName = "UJA Sun - Jaen Noon";
        private const string ReflectionProbeName = "UJA Campus Reflection Probe";
        private const string LightProbeGroupName = "UJA Campus Light Probes";
        private const string GlobalVolumeName = "UJA Campus Global Volume";
        private static readonly string[] BakedGIContributorRoots = { "B1_29", "Static Level", "Plane" };

        [MenuItem("UJA/Lighting/Apply UJACampus Natural Daylight")]
        public static void ApplyNaturalDaylight()
        {
            ConfigureSun();
            ConfigureRenderSettings();
            ConfigureLightingSettings();
            ConfigureBakedGIContributors();
            ConfigureReflectionProbe();
            ConfigureLightProbeGroup();
            ConfigurePostProcessing();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log("UJACampus daylight, skybox, baked GI settings and post-processing profile applied.");
        }

        [MenuItem("UJA/Lighting/Generate UJACampus Lighting")]
        public static void GenerateLighting()
        {
            if (Lightmapping.isRunning)
            {
                Debug.LogWarning("Lightmapping is already running.");
                return;
            }

            bool started = Lightmapping.BakeAsync();
            Debug.Log($"UJACampus lightmap bake requested. started={started}, isRunning={Lightmapping.isRunning}");
        }

        [MenuItem("UJA/Lighting/Validate UJACampus Lighting Setup")]
        public static void ValidateLightingSetup()
        {
            int renderers = 0;
            int bakedContributors = 0;

            foreach (string rootName in BakedGIContributorRoots)
            {
                GameObject root = GameObject.Find(rootName);
                if (root == null)
                    continue;

                MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
                renderers += meshRenderers.Length;

                foreach (MeshRenderer meshRenderer in meshRenderers)
                {
                    if (GameObjectUtility.AreStaticEditorFlagsSet(meshRenderer.gameObject, StaticEditorFlags.ContributeGI))
                        bakedContributors++;
                }
            }

            Debug.Log(
                $"UJACampus lighting validation: renderers={renderers}, bakedGIContributors={bakedContributors}, " +
                $"skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")}, " +
                $"lightingSettings={(Lightmapping.lightingSettings != null ? Lightmapping.lightingSettings.name : "null")}, " +
                $"isRunning={Lightmapping.isRunning}");
        }

        private static void ConfigureSun()
        {
            GameObject sunObject = GameObject.Find(SunName) ?? GameObject.Find("Directional Light");
            if (sunObject == null)
                sunObject = new GameObject(SunName);

            sunObject.name = SunName;
            sunObject.transform.SetPositionAndRotation(
                new Vector3(195.9f, 28.79f, 289.121735f),
                Quaternion.Euler(52f, -32f, 0f));

            Light sun = sunObject.GetComponent<Light>();
            if (sun == null)
                sun = sunObject.AddComponent<Light>();

            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.80f, 1f);
            sun.intensity = 2.65f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowResolution = LightShadowResolution.High;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.renderMode = LightRenderMode.ForcePixel;
        }

        private static void ConfigureRenderSettings()
        {
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (skybox != null)
                RenderSettings.skybox = skybox;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.85f;
            RenderSettings.reflectionIntensity = 0.7f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.90f, 1f);
            RenderSettings.fogDensity = 0.0025f;
        }

        private static void ConfigureLightingSettings()
        {
            EnsureFolder(Path.GetDirectoryName(LightingSettingsPath)?.Replace("\\", "/") ?? "Assets/_Project/Spaces/UJA/Scenes");

            LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }

            Lightmapping.lightingSettings = settings;

            SerializedObject serialized = new SerializedObject(settings);
            SetBool(serialized, "m_BakedGI", true);
            SetBool(serialized, "m_RealtimeGI", false);
            SetInt(serialized, "m_Lightmapper", (int)LightingSettings.Lightmapper.ProgressiveCPU);
            SetFloat(serialized, "m_LightmapResolution", 28f);
            SetInt(serialized, "m_LightmapPadding", 3);
            SetInt(serialized, "m_MaxLightmapSize", 2048);
            SetFloat(serialized, "m_IndirectResolution", 1.5f);
            SetFloat(serialized, "m_AlbedoBoost", 1.15f);
            SetFloat(serialized, "m_IndirectScale", 1.2f);
            SetBool(serialized, "m_AO", true);
            SetFloat(serialized, "m_AOMaxDistance", 3f);
            SetFloat(serialized, "m_AOExponentDirect", 0.7f);
            SetFloat(serialized, "m_AOExponentIndirect", 0.7f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureBakedGIContributors()
        {
            foreach (string rootName in BakedGIContributorRoots)
            {
                GameObject root = GameObject.Find(rootName);
                if (root == null)
                    continue;

                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer meshRenderer in renderers)
                {
                    GameObject rendererObject = meshRenderer.gameObject;
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(rendererObject);
                    flags |= StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic;
                    GameObjectUtility.SetStaticEditorFlags(rendererObject, flags);

                    meshRenderer.receiveGI = ReceiveGI.Lightmaps;
                    EditorUtility.SetDirty(meshRenderer);
                }

                EditorUtility.SetDirty(root);
            }
        }

        private static void ConfigureReflectionProbe()
        {
            GameObject probeObject = GameObject.Find(ReflectionProbeName);
            if (probeObject == null)
                probeObject = new GameObject(ReflectionProbeName);

            probeObject.transform.position = new Vector3(250f, 10f, 350f);

            ReflectionProbe probe = probeObject.GetComponent<ReflectionProbe>();
            if (probe == null)
                probe = probeObject.AddComponent<ReflectionProbe>();

            probe.mode = ReflectionProbeMode.Baked;
            probe.resolution = 256;
            probe.size = new Vector3(220f, 50f, 220f);
            probe.center = Vector3.zero;
            probe.intensity = 0.8f;
            probe.boxProjection = true;
        }

        private static void ConfigureLightProbeGroup()
        {
            GameObject probesObject = GameObject.Find(LightProbeGroupName);
            if (probesObject == null)
                probesObject = new GameObject(LightProbeGroupName);

            probesObject.transform.position = new Vector3(250f, 2f, 350f);

            LightProbeGroup group = probesObject.GetComponent<LightProbeGroup>();
            if (group == null)
                group = probesObject.AddComponent<LightProbeGroup>();

            group.probePositions = new[]
            {
                new Vector3(-70f, 1.5f, -70f), new Vector3(0f, 1.5f, -70f), new Vector3(70f, 1.5f, -70f),
                new Vector3(-70f, 1.5f, 0f), new Vector3(0f, 1.5f, 0f), new Vector3(70f, 1.5f, 0f),
                new Vector3(-70f, 1.5f, 70f), new Vector3(0f, 1.5f, 70f), new Vector3(70f, 1.5f, 70f),
                new Vector3(-70f, 8f, -70f), new Vector3(0f, 8f, -70f), new Vector3(70f, 8f, -70f),
                new Vector3(-70f, 8f, 0f), new Vector3(0f, 8f, 0f), new Vector3(70f, 8f, 0f),
                new Vector3(-70f, 8f, 70f), new Vector3(0f, 8f, 70f), new Vector3(70f, 8f, 70f)
            };
        }

        private static void ConfigurePostProcessing()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.value = 1.2f;
            bloom.intensity.value = 0.18f;

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.value = 0.12f;
            vignette.smoothness.value = 0.45f;

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.value = 0.05f;
            color.contrast.value = 8f;
            color.saturation.value = 4f;
            color.colorFilter.value = new Color(1f, 0.96f, 0.88f, 1f);

            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.active = true;
            whiteBalance.temperature.value = 12f;
            whiteBalance.tint.value = -2f;

            EditorUtility.SetDirty(profile);

            GameObject volumeObject = GameObject.Find(GlobalVolumeName);
            if (volumeObject == null)
                volumeObject = new GameObject(GlobalVolumeName);

            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null)
                volume = volumeObject.AddComponent<Volume>();

            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
                component = profile.Add<T>(true);
            return component;
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
