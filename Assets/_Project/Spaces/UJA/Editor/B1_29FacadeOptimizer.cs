using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UJA.EditorTools
{
    public static class B1_29FacadeOptimizer
    {
        private const string SourceObjectName = "B1_29";
        private const string SourceModelPath = "Assets/_Project/Spaces/UJA/Scenes/B1_29.fbx";
        private const string OptimizedPrefabPath = "Assets/_Project/Spaces/UJA/Prefabs/B1_29_Facade.prefab";
        private const string ReportPath = "Assets/_Project/Spaces/UJA/Prefabs/B1_29_Facade_OptimizationReport.txt";
        private const float ShellDepthMeters = 12f;
        private const float RoofDepthMeters = 2f;

        [MenuItem("UJA/Building Import/B1_29/Create Facade Prefab And Replace Scene Instance")]
        public static void CreateFacadePrefabAndReplaceSceneInstance()
        {
            GameObject workingCopy = null;

            try
            {
                DeleteExistingWorkingCopies();
                Debug.Log("Starting B1_29 facade optimization.");

                GameObject sceneInstance = GameObject.Find(SourceObjectName);
                if (sceneInstance == null)
                {
                    Debug.LogError($"Could not find scene object named {SourceObjectName}.");
                    return;
                }

                GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
                if (sourceModel == null)
                {
                    Debug.LogError($"Could not load source model at {SourceModelPath}.");
                    return;
                }

                workingCopy = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
                if (workingCopy == null)
                {
                    Debug.LogError($"Could not instantiate source model at {SourceModelPath}.");
                    return;
                }

                workingCopy.name = "B1_29_Facade_WorkingCopy";
                workingCopy.transform.SetPositionAndRotation(sceneInstance.transform.position, sceneInstance.transform.rotation);
                workingCopy.transform.localScale = sceneInstance.transform.localScale;

                Renderer[] sourceRenderers = workingCopy.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer != null && renderer.enabled)
                    .ToArray();

                if (sourceRenderers.Length == 0)
                {
                    Debug.LogError($"{SourceObjectName} has no enabled renderers to optimize.");
                    return;
                }

                Bounds sourceBounds = CalculateWorldBounds(sourceRenderers);
                Transform sourceParent = sceneInstance.transform.parent;
                Vector3 sourcePosition = sceneInstance.transform.position;
                Quaternion sourceRotation = sceneInstance.transform.rotation;
                Vector3 sourceScale = sceneInstance.transform.localScale;
                string sourceName = sceneInstance.name;

                if (PrefabUtility.IsPartOfPrefabInstance(workingCopy))
                    PrefabUtility.UnpackPrefabInstance(workingCopy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                Debug.Log("B1_29 working copy created.");

                OptimizationStats stats = StripToFacadeShell(workingCopy, sourceBounds);
                Debug.Log($"B1_29 facade shell stripped. Kept {stats.KeptRenderers}, removed {stats.RemovedRenderers}.");

                AddSimpleBuildingCollider(workingCopy, sourceBounds);
                PruneEmptyTransforms(workingCopy.transform);
                SetStaticRecursive(workingCopy);

                EnsureFolder(Path.GetDirectoryName(OptimizedPrefabPath)?.Replace("\\", "/") ?? "Assets/_Project/Spaces/UJA/Prefabs");
                PrefabUtility.SaveAsPrefabAsset(workingCopy, OptimizedPrefabPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError($"Failed to save optimized prefab at {OptimizedPrefabPath}.");
                    return;
                }

                GameObject optimizedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptimizedPrefabPath);
                if (optimizedPrefab == null)
                {
                    Debug.LogError($"Could not load optimized prefab at {OptimizedPrefabPath}.");
                    return;
                }

                UnityEngine.Object.DestroyImmediate(sceneInstance);

                GameObject optimizedInstance = PrefabUtility.InstantiatePrefab(optimizedPrefab) as GameObject;
                if (optimizedInstance == null)
                {
                    Debug.LogError("Could not instantiate optimized facade prefab.");
                    return;
                }

                optimizedInstance.name = sourceName;
                optimizedInstance.transform.SetParent(sourceParent, false);
                optimizedInstance.transform.SetPositionAndRotation(sourcePosition, sourceRotation);
                optimizedInstance.transform.localScale = sourceScale;

                WriteReport(stats, sourceBounds);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(optimizedInstance.scene);
                EditorSceneManager.SaveScene(optimizedInstance.scene);

                Debug.Log($"B1_29 facade optimized. Kept {stats.KeptRenderers} renderers and removed {stats.RemovedRenderers}. Prefab: {OptimizedPrefabPath}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (workingCopy != null)
                    UnityEngine.Object.DestroyImmediate(workingCopy);
            }
        }

        private static OptimizationStats StripToFacadeShell(GameObject root, Bounds totalBounds)
        {
            OptimizationStats stats = new OptimizationStats();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            GameObject[] objectsToDelete = renderers
                .Where(renderer => renderer != null)
                .Where(renderer =>
                {
                    stats.TotalRenderers++;
                    if (ShouldKeepRenderer(renderer, totalBounds))
                    {
                        stats.KeptRenderers++;
                        return false;
                    }

                    stats.RemovedRenderers++;
                    return true;
                })
                .Select(renderer => renderer.gameObject)
                .Distinct()
                .OrderByDescending(GetHierarchyDepth)
                .ToArray();

            foreach (GameObject gameObject in objectsToDelete)
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);

            return stats;
        }

        private static bool ShouldKeepRenderer(Renderer renderer, Bounds totalBounds)
        {
            Bounds bounds = renderer.bounds;
            string semanticName = GetSemanticName(renderer);

            bool touchesWest = bounds.min.x <= totalBounds.min.x + ShellDepthMeters;
            bool touchesEast = bounds.max.x >= totalBounds.max.x - ShellDepthMeters;
            bool touchesSouth = bounds.min.z <= totalBounds.min.z + ShellDepthMeters;
            bool touchesNorth = bounds.max.z >= totalBounds.max.z - ShellDepthMeters;
            bool touchesRoof = bounds.max.y >= totalBounds.max.y - RoofDepthMeters;
            bool hasFacadeMaterial = ContainsAny(semanticName,
                "fachada", "muro", "pared", "wall", "vidrio", "glass", "ventana", "window",
                "ladrillo", "brick", "piedra", "stone", "caliza", "marmol", "mármol",
                "aluminio", "aluminum", "aluminium", "transparente", "carpinteria", "carpintería");

            return touchesWest || touchesEast || touchesSouth || touchesNorth || touchesRoof || hasFacadeMaterial;
        }

        private static string GetSemanticName(Renderer renderer)
        {
            StringBuilder builder = new StringBuilder(renderer.name.ToLowerInvariant());
            foreach (Material material in renderer.sharedMaterials)
                if (material != null)
                    builder.Append(' ').Append(material.name.ToLowerInvariant());

            return builder.ToString();
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            return tokens.Any(value.Contains);
        }

        private static void AddSimpleBuildingCollider(GameObject root, Bounds worldBounds)
        {
            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider == null)
                collider = root.AddComponent<BoxCollider>();

            collider.center = root.transform.InverseTransformPoint(worldBounds.center);
            Vector3 localMin = root.transform.InverseTransformPoint(worldBounds.min);
            Vector3 localMax = root.transform.InverseTransformPoint(worldBounds.max);
            collider.size = new Vector3(
                Mathf.Abs(localMax.x - localMin.x),
                Mathf.Abs(localMax.y - localMin.y),
                Mathf.Abs(localMax.z - localMin.z));
        }

        private static Bounds CalculateWorldBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static bool PruneEmptyTransforms(Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (PruneEmptyTransforms(child))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            return transform.childCount == 0
                && transform.GetComponents<Component>().All(component => component is Transform);
        }

        private static int GetHierarchyDepth(GameObject gameObject)
        {
            int depth = 0;
            Transform transform = gameObject.transform;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        private static void DeleteExistingWorkingCopies()
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.name != "B1_29_Facade_WorkingCopy")
                    continue;

                if (EditorUtility.IsPersistent(gameObject))
                    continue;

                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetStaticRecursive(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.isStatic = true;
        }

        private static void WriteReport(OptimizationStats stats, Bounds bounds)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("B1_29 facade optimization report");
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Shell depth meters: {ShellDepthMeters}");
            builder.AppendLine($"Roof depth meters: {RoofDepthMeters}");
            builder.AppendLine($"Total renderers: {stats.TotalRenderers}");
            builder.AppendLine($"Kept renderers: {stats.KeptRenderers}");
            builder.AppendLine($"Removed renderers: {stats.RemovedRenderers}");
            builder.AppendLine($"Bounds center: {bounds.center}");
            builder.AppendLine($"Bounds size: {bounds.size}");

            File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(ReportPath);
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

        private struct OptimizationStats
        {
            public int TotalRenderers;
            public int KeptRenderers;
            public int RemovedRenderers;
        }
    }
}
