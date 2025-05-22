using System;
using UnityEngine;
using UnityEditor;

namespace Nanity
{
    // Serializable asset to store meshlet data
    [CreateAssetMenu(fileName = "MeshletAsset", menuName = "Meshlet/Create MeshletAsset.asset", order = 1)]
    public class MeshletAsset : ScriptableObject
    {
        public MeshletCollection Collection;
        public Mesh SourceMesh;
    }

    // Editor window for generating meshlets
    public class MeshletGenerator : EditorWindow
    {
        private Mesh m_SelectedMesh;
        private string m_SavePath = "Assets/";
        private string m_AssetName;
        private bool m_ProcessingMesh = false;
        private string m_StatusMessage = "";

        private BuildSettings m_Settings = new BuildSettings();
        
        [MenuItem("Window/Nanity/Meshlet Generator")]
        public static void ShowWindow()
        {
            GetWindow<MeshletGenerator>("Meshlet Generator");
        }

        private void OnEnable()
        {
            m_Settings.EnableFuse = true;
            m_Settings.EnableOpt = true;
            m_Settings.EnableRemap = true;
            m_Settings.MaxVertices = 64;
            m_Settings.MaxTriangles = 64;
            m_Settings.ConeWeight = 0.5f;
        }

        private void OnGUI()
        {
            GUILayout.Label("Meshlet Generator", EditorStyles.boldLabel);

            // Mesh selection
            EditorGUILayout.BeginHorizontal();
            m_SelectedMesh = (Mesh)EditorGUILayout.ObjectField("Source Mesh", m_SelectedMesh, typeof(Mesh), false);
            if (m_SelectedMesh) m_AssetName = m_SelectedMesh.name;
            EditorGUILayout.EndHorizontal();

            // Save location
            EditorGUILayout.BeginHorizontal();
            m_SavePath = EditorGUILayout.TextField("Save Path", m_SavePath);
            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                var path = EditorUtility.SaveFolderPanel("Save Meshlet Assets to Folder", m_SavePath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    // Convert to project relative path
                    if (path.StartsWith(Application.dataPath))
                    {
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    }

                    m_SavePath = path;
                }
            }

            EditorGUILayout.EndHorizontal();
            
            m_Settings.EnableFuse = EditorGUILayout.Toggle("Enable Fuse", m_Settings.EnableFuse);
            m_Settings.EnableOpt = EditorGUILayout.Toggle("Enable Opt", m_Settings.EnableOpt);
            m_Settings.EnableRemap = EditorGUILayout.Toggle("Enable Remap", m_Settings.EnableRemap);
            m_Settings.MaxVertices = (uint)EditorGUILayout.IntField("Max Vertices", (int)m_Settings.MaxVertices);
            m_Settings.MaxTriangles = (uint)EditorGUILayout.IntField("Max Triangles", (int)m_Settings.MaxTriangles);
            m_Settings.ConeWeight = EditorGUILayout.FloatField("Cone Weight", m_Settings.ConeWeight);
            
            // Asset name
            m_AssetName = EditorGUILayout.TextField("Asset Name", m_AssetName);
            // Status message
            if (!string.IsNullOrEmpty(m_StatusMessage))
            {
                EditorGUILayout.HelpBox(m_StatusMessage, MessageType.Info);
            }

            // Generation button
            EditorGUI.BeginDisabledGroup(!m_SelectedMesh || m_ProcessingMesh);
            if (GUILayout.Button("Generate Meshlets"))
            {
                GenerateMeshlets();
            }

            EditorGUI.EndDisabledGroup();
        }

        private void GenerateMeshlets()
        {
            if (!m_SelectedMesh)
            {
                m_StatusMessage = "Please select a mesh first.";
                return;
            }

            m_ProcessingMesh = true;
            m_StatusMessage = "Processing mesh...";

            try
            {
                // Get mesh data
                var vertices = m_SelectedMesh.vertices;
                var triangles = m_SelectedMesh.triangles;

                // Convert triangles to uint[]
                var uintTriangles = new uint[triangles.Length];
                for (var i = 0; i < triangles.Length; i++)
                {
                    uintTriangles[i] = (uint)triangles[i];
                }

                // Process the mesh
                var collection = NanityPlugin.ProcessMesh(uintTriangles, vertices, m_Settings);

                // Create and save the asset
                var asset = CreateInstance<MeshletAsset>();
                asset.Collection = collection;
                asset.SourceMesh = m_SelectedMesh;
                var fullPath = System.IO.Path.Combine(m_SavePath, $"{m_AssetName}.asset");
                AssetDatabase.CreateAsset(asset, fullPath);
                AssetDatabase.SaveAssets();
                m_StatusMessage = $"Successfully created meshlet asset: {fullPath}";
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
            }
            catch (Exception ex)
            {
                m_StatusMessage = $"Error generating meshlets: {ex.Message}";
                Debug.LogError($"Error generating meshlets: {ex}");
            }
            finally
            {
                m_ProcessingMesh = false;
            }
        }
    }
}