using ChromaVale.Core.GameLogic;
using UnityEngine;
using System.Collections.Generic;

namespace ChromaVale.Presentation.Views.Components
{
    public class PiecePlacer : MonoBehaviour
    {
        public static PiecePlacer Instance { get; private set; }

        [Header("Trace Pieces — Dormant (Fallback)")]
        [SerializeField] private GameObject _straightDormantPrefab;
        [SerializeField] private GameObject _cornerDormantPrefab;
        [SerializeField] private GameObject _tJunctionDormantPrefab;
        [SerializeField] private GameObject _crossDormantPrefab;

        [Header("Trace Pieces — Active (Fallback)")]
        [SerializeField] private GameObject _straightActivePrefab;
        [SerializeField] private GameObject _cornerActivePrefab;
        [SerializeField] private GameObject _tJunctionActivePrefab;
        [SerializeField] private GameObject _crossActivePrefab;

        [Header("Trace Pieces — Blender Models (v3)")]
        [SerializeField] private GameObject _straightTraceModel;
        [SerializeField] private GameObject _cornerTraceModel;
        [SerializeField] private GameObject _splitterTraceModel;
        [SerializeField] private GameObject _crossTraceModel;

        [Header("Board Tiles — Blender Models (v3)")]
        [SerializeField] private GameObject _flatTileModel;
        [SerializeField] private GameObject _groovedTileModel;

        [Header("Node Pieces")]
        [SerializeField] private GameObject _sourcePadPrefab;
        [SerializeField] private GameObject _destPadPrefab;

        [Header("Board Structure (Fallback)")]
        [SerializeField] private GameObject _boardSubstratePrefab;
        [SerializeField] private GameObject _emptyCellPrefab;

        [Header("Decorative")]
        [SerializeField] private GameObject _mountingHolePrefab;
        [SerializeField] private GameObject _boardEdgePrefab;

        private GameObject _boardSubstrateInstance;
        private readonly Dictionary<(int x, int y), GameObject> _cellPieces = new();

        // Shared copper trace material — one instance, per-tile variation via MaterialPropertyBlock
        private static Material _traceMaterial;
        private static Material TraceMaterial
        {
            get
            {
                if (_traceMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _traceMaterial = new Material(shader)
                    {
                        color = ChromaPalette.CopperActive // #B87333 bright copper
                    };
                    _traceMaterial.SetFloat("_Metallic", 0.9f);
                    _traceMaterial.SetFloat("_Smoothness", 0.6f);
                    _traceMaterial.EnableKeyword("_EMISSION");
                    _traceMaterial.SetColor("_EmissionColor", new Color(0.15f, 0.08f, 0.02f)); // Subtle warm idle glow
                    _traceMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _traceMaterial;
            }
        }

        private void Awake()
        {
            Instance = this;
            LoadPrefabsIfNeeded();
        }

        private void LoadPrefabsIfNeeded()
        {
#if UNITY_EDITOR
            // ── v3 Blender trace models (single mesh, material handles active/dormant) ──
            if (_straightTraceModel == null)
            {
                _straightTraceModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Models/Pieces/straight_trace.glb");
                _cornerTraceModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Models/Pieces/corner_trace.glb");
                _splitterTraceModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Models/Pieces/splitter_trace.glb");
                _crossTraceModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Models/Pieces/cross_trace.glb");
                _flatTileModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Models/Pieces/flat_tile.glb");
                _groovedTileModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Models/Pieces/grooved_tile.glb");
                Debug.Log("[PiecePlacer] Auto-loaded 6 Blender trace/tile models (v3).");
            }

            // ── Fallback: old Sketchfab prefabs ──
            if (_straightDormantPrefab == null)
            {
                _straightDormantPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/straight_dormant.prefab");
                _straightActivePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/straight_active.prefab");
                _cornerDormantPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/corner_dormant.prefab");
                _cornerActivePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/corner_active.prefab");
                _tJunctionDormantPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/t_junction_dormant.prefab");
                _tJunctionActivePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/t_junction_active.prefab");
                _crossDormantPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/cross_dormant.prefab");
                _crossActivePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/cross_active.prefab");
                _sourcePadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/source_pad.prefab");
                _destPadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/dest_pad.prefab");
                _boardSubstratePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/board_substrate.prefab");
                _emptyCellPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/empty_cell.prefab");
                _mountingHolePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/mounting_hole.prefab");
                _boardEdgePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Pieces/board_edge.prefab");
                Debug.Log("[PiecePlacer] Auto-loaded all 14 legacy prefab references (fallback).");
            }
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Get the single-mesh Blender trace model for the given shape.
        /// Falls back to old dormant prefab if v3 model not available.
        /// </summary>
        private GameObject GetTraceModel(SegmentShape shape)
        {
            GameObject model = shape switch
            {
                SegmentShape.Straight      => _straightTraceModel,
                SegmentShape.Corner        => _cornerTraceModel,
                SegmentShape.Splitter      => _splitterTraceModel,
                SegmentShape.CrossJunction => _crossTraceModel,
                _                           => null
            };

            if (model != null) return model;

            // Fallback to old dormant prefab
            Debug.LogWarning($"[PiecePlacer] v3 model missing for {shape}, using legacy dormant prefab.");
            return shape switch
            {
                SegmentShape.Straight      => _straightDormantPrefab,
                SegmentShape.Corner        => _cornerDormantPrefab,
                SegmentShape.Splitter      => _tJunctionDormantPrefab,
                SegmentShape.CrossJunction => _crossDormantPrefab,
                _                           => _straightDormantPrefab
            };
        }

        // Legacy accessors — kept for backward compat but always return the v3 single mesh
        public GameObject GetDormantPrefab(SegmentShape shape) => GetTraceModel(shape);
        public GameObject GetActivePrefab(SegmentShape shape) => GetTraceModel(shape);

        /// <summary>
        /// Apply the shared copper trace material to all MeshRenderers on a trace
        /// root and its children.  The per-tile visual state (ghost/idle/energised)
        /// is driven later via TileVisual.ApplyColor's MaterialPropertyBlock.
        /// </summary>
        private static void ApplyTraceMaterial(GameObject traceRoot)
        {
            if (traceRoot == null) return;
            var renderers = traceRoot.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // Only replace if the current material is from the .glb import
                // (default Lit or missing shader).  Don't override indicator materials.
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null
                    || mat.shader.name == "Hidden/InternalErrorShader"
                    || mat.shader.name == "Standard"
                    || mat.shader.name == "Universal Render Pipeline/Lit")
                {
                    r.sharedMaterial = TraceMaterial;
                }
            }
        }

        public GameObject PlaceTracePiece(SegmentShape shape, int rotationDeg,
            Transform parent, Vector3? worldPosition = null)
        {
            var model = GetTraceModel(shape);
            if (model == null)
            {
                Debug.LogWarning($"[PiecePlacer] No model for {shape}");
                return null;
            }
            var instance = Instantiate(model, parent);
            instance.name = $"Piece_{shape}_trace";
            if (worldPosition.HasValue)
                instance.transform.position = worldPosition.Value;
            else
                instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
            instance.transform.localScale = Vector3.one;

            // Apply shared copper material before TileVisual's MPB overrides kick in
            ApplyTraceMaterial(instance);

            return instance;
        }

        /// <summary>
        /// Legacy ActivatePiece — v3 uses a single mesh with material-driven state,
        /// so activating just means swapping material property blocks (handled by
        /// TileVisual.ApplyColor).  For backward compat with callers that still
        /// invoke this method, return the dormant instance unchanged.
        /// </summary>
        public GameObject ActivatePiece(SegmentShape shape, Transform dormantInstance)
        {
            // v3: single mesh — no geometry swap needed.  TileVisual handles the
            // active/dormant distinction via MaterialPropertyBlock in ApplyColor().
            // Just log and return the existing instance.
            Debug.Log($"[PiecePlacer] ActivatePiece({shape}) — v3 single-mesh, no-op swap.");
            return dormantInstance?.gameObject;
        }

        public GameObject PlacePieceAtCell(SegmentShape shape, int rotationDeg,
            int cellX, int cellY, Vector3 worldPosition, Transform parent)
        {
            var model = GetTraceModel(shape);
            if (model == null) return null;
            var instance = Instantiate(model, worldPosition,
                Quaternion.Euler(0f, 0f, rotationDeg), parent);
            instance.name = $"Piece_{shape}_{cellX}_{cellY}_trace";
            instance.transform.localScale = Vector3.one;
            ApplyTraceMaterial(instance);
            _cellPieces[(cellX, cellY)] = instance;
            return instance;
        }

        /// <summary>
        /// Legacy ActivatePieceAtCell — v3 single-mesh, no-op geometry swap.
        /// TileVisual handles the visual state via MaterialPropertyBlock.
        /// </summary>
        public void ActivatePieceAtCell(int cellX, int cellY, SegmentShape shape,
            int rotationDeg, Transform parent)
        {
            // v3: no-op — the mesh stays the same, only MPB changes
            Debug.Log($"[PiecePlacer] ActivatePieceAtCell({cellX},{cellY}) — v3 no-op.");
        }

        public void RemovePieceAtCell(int cellX, int cellY)
        {
            var key = (cellX, cellY);
            if (_cellPieces.TryGetValue(key, out var instance))
            {
                if (instance != null) DestroyImmediate(instance);
                _cellPieces.Remove(key);
            }
        }

        public void ClearAllPieces()
        {
            foreach (var kvp in _cellPieces)
                if (kvp.Value != null) DestroyImmediate(kvp.Value);
            _cellPieces.Clear();
        }

        public GameObject PlaceSourcePad(Vector3 worldPosition, Transform parent)
        {
            if (_sourcePadPrefab == null) { Debug.LogWarning("[PiecePlacer] No source_pad prefab"); return null; }
            var instance = Instantiate(_sourcePadPrefab, worldPosition, Quaternion.identity, parent);
            instance.name = "SourcePad";
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        public GameObject PlaceDestPad(Vector3 worldPosition, Transform parent)
        {
            if (_destPadPrefab == null) { Debug.LogWarning("[PiecePlacer] No dest_pad prefab"); return null; }
            var instance = Instantiate(_destPadPrefab, worldPosition, Quaternion.identity, parent);
            instance.name = "DestPad";
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        public GameObject PlaceBoardSubstrate(int gridWidth, int gridHeight,
            float cellSize, Transform parent)
        {
            // v3: prefer flat_tile Blender model for board substrate
            var substrateModel = _flatTileModel != null ? _flatTileModel : _boardSubstratePrefab;
            if (substrateModel == null) { Debug.LogWarning("[PiecePlacer] No board substrate model"); return null; }

            float boardWorldW = gridWidth * cellSize;
            float boardWorldH = gridHeight * cellSize;
            const float modelSize = 5f;
            var instance = Instantiate(substrateModel, Vector3.zero, Quaternion.identity, parent);
            instance.name = "BoardSubstrate";
            instance.transform.localScale = new Vector3(boardWorldW / modelSize, boardWorldH / modelSize, 1f);
            _boardSubstrateInstance = instance;
            return instance;
        }

        public GameObject PlaceEmptyCell(Vector3 worldPosition, Transform parent)
        {
            // v3: prefer grooved_tile Blender model for empty cells
            var cellModel = _groovedTileModel != null ? _groovedTileModel : _emptyCellPrefab;
            if (cellModel == null) return null;
            var instance = Instantiate(cellModel, worldPosition, Quaternion.identity, parent);
            instance.name = "EmptyCell";
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        public GameObject PlaceMountingHole(Vector3 worldPosition, Transform parent)
        {
            if (_mountingHolePrefab == null) return null;
            var instance = Instantiate(_mountingHolePrefab, worldPosition, Quaternion.identity, parent);
            instance.name = "MountingHole";
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        public GameObject PlaceBoardEdge(Vector3 worldPosition, int rotationDeg, Transform parent)
        {
            if (_boardEdgePrefab == null) return null;
            var instance = Instantiate(_boardEdgePrefab, worldPosition,
                Quaternion.Euler(0f, 0f, rotationDeg), parent);
            instance.name = "BoardEdge";
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        public void ClearBoard()
        {
            ClearAllPieces();
            if (_boardSubstrateInstance != null)
            {
                DestroyImmediate(_boardSubstrateInstance);
                _boardSubstrateInstance = null;
            }
        }

        public bool ValidateLevel1Essentials()
        {
            if (GetTraceModel(SegmentShape.Straight) == null)
                { Debug.LogError("[PiecePlacer] Missing straight trace model"); return false; }
            if (_sourcePadPrefab == null) { Debug.LogError("[PiecePlacer] Missing source_pad"); return false; }
            if (_destPadPrefab == null) { Debug.LogError("[PiecePlacer] Missing dest_pad"); return false; }
            if ((_flatTileModel ?? _boardSubstratePrefab) == null)
                { Debug.LogError("[PiecePlacer] Missing board substrate"); return false; }
            return true;
        }
    }
}
