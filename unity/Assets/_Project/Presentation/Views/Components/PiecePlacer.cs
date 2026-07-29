using ChromaVale.Core.GameLogic;
using UnityEngine;
using System.Collections.Generic;

namespace ChromaVale.Presentation.Views.Components
{
    public class PiecePlacer : MonoBehaviour
    {
        public static PiecePlacer Instance { get; private set; }

        [Header("Trace Pieces — Dormant")]
        [SerializeField] private GameObject _straightDormantPrefab;
        [SerializeField] private GameObject _cornerDormantPrefab;
        [SerializeField] private GameObject _tJunctionDormantPrefab;
        [SerializeField] private GameObject _crossDormantPrefab;

        [Header("Trace Pieces — Active")]
        [SerializeField] private GameObject _straightActivePrefab;
        [SerializeField] private GameObject _cornerActivePrefab;
        [SerializeField] private GameObject _tJunctionActivePrefab;
        [SerializeField] private GameObject _crossActivePrefab;

        [Header("Node Pieces")]
        [SerializeField] private GameObject _sourcePadPrefab;
        [SerializeField] private GameObject _destPadPrefab;

        [Header("Board Structure")]
        [SerializeField] private GameObject _boardSubstratePrefab;
        [SerializeField] private GameObject _emptyCellPrefab;

        [Header("Decorative")]
        [SerializeField] private GameObject _mountingHolePrefab;
        [SerializeField] private GameObject _boardEdgePrefab;

        private GameObject _boardSubstrateInstance;
        private readonly Dictionary<(int x, int y), GameObject> _cellPieces = new();

        private void Awake()
        {
            Instance = this;
            LoadPrefabsIfNeeded();
        }

        private void LoadPrefabsIfNeeded()
        {
#if UNITY_EDITOR
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
                Debug.Log("[PiecePlacer] Auto-loaded all 14 prefab references.");
            }
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public GameObject GetDormantPrefab(SegmentShape shape) => shape switch
        {
            SegmentShape.Straight      => _straightDormantPrefab,
            SegmentShape.Corner        => _cornerDormantPrefab,
            SegmentShape.Splitter      => _tJunctionDormantPrefab,
            SegmentShape.CrossJunction => _crossDormantPrefab,
            _                           => _straightDormantPrefab
        };

        public GameObject GetActivePrefab(SegmentShape shape) => shape switch
        {
            SegmentShape.Straight      => _straightActivePrefab,
            SegmentShape.Corner        => _cornerActivePrefab,
            SegmentShape.Splitter      => _tJunctionActivePrefab,
            SegmentShape.CrossJunction => _crossActivePrefab,
            _                           => _straightActivePrefab
        };

        public GameObject PlaceTracePiece(SegmentShape shape, int rotationDeg,
            Transform parent, Vector3? worldPosition = null)
        {
            var prefab = GetDormantPrefab(shape);
            if (prefab == null)
            {
                Debug.LogWarning($"[PiecePlacer] No prefab for {shape}");
                return null;
            }
            var instance = Instantiate(prefab, parent);
            instance.name = $"Piece_{shape}_dormant";
            if (worldPosition.HasValue)
                instance.transform.position = worldPosition.Value;
            else
                instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        public GameObject ActivatePiece(SegmentShape shape, Transform dormantInstance)
        {
            var activePrefab = GetActivePrefab(shape);
            if (activePrefab == null || dormantInstance == null) return null;
            var parent = dormantInstance.parent;
            var pos = dormantInstance.localPosition;
            var rot = dormantInstance.localRotation;
            var scale = dormantInstance.localScale;
            DestroyImmediate(dormantInstance.gameObject);
            var active = Instantiate(activePrefab, parent);
            active.name = $"Piece_{shape}_active";
            active.transform.localPosition = pos;
            active.transform.localRotation = rot;
            active.transform.localScale = scale;
            return active;
        }

        public GameObject PlacePieceAtCell(SegmentShape shape, int rotationDeg,
            int cellX, int cellY, Vector3 worldPosition, Transform parent)
        {
            var prefab = GetDormantPrefab(shape);
            if (prefab == null) return null;
            var instance = Instantiate(prefab, worldPosition,
                Quaternion.Euler(0f, 0f, rotationDeg), parent);
            instance.name = $"Piece_{shape}_{cellX}_{cellY}_dormant";
            instance.transform.localScale = Vector3.one;
            _cellPieces[(cellX, cellY)] = instance;
            return instance;
        }

        public void ActivatePieceAtCell(int cellX, int cellY, SegmentShape shape,
            int rotationDeg, Transform parent)
        {
            var key = (cellX, cellY);
            if (!_cellPieces.TryGetValue(key, out var dormant)) return;
            var activePrefab = GetActivePrefab(shape);
            if (activePrefab == null) return;
            var pos = dormant.transform.position;
            var rot = dormant.transform.rotation;
            DestroyImmediate(dormant);
            var active = Instantiate(activePrefab, pos, rot, parent);
            active.name = $"Piece_{shape}_{cellX}_{cellY}_active";
            active.transform.localScale = Vector3.one;
            _cellPieces[key] = active;
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
            if (_boardSubstratePrefab == null) { Debug.LogWarning("[PiecePlacer] No board_substrate prefab"); return null; }
            float boardWorldW = gridWidth * cellSize;
            float boardWorldH = gridHeight * cellSize;
            const float modelSize = 5f;
            var instance = Instantiate(_boardSubstratePrefab, Vector3.zero, Quaternion.identity, parent);
            instance.name = "BoardSubstrate";
            instance.transform.localScale = new Vector3(boardWorldW / modelSize, boardWorldH / modelSize, 1f);
            _boardSubstrateInstance = instance;
            return instance;
        }

        public GameObject PlaceEmptyCell(Vector3 worldPosition, Transform parent)
        {
            if (_emptyCellPrefab == null) return null;
            var instance = Instantiate(_emptyCellPrefab, worldPosition, Quaternion.identity, parent);
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
            if (_straightDormantPrefab == null) { Debug.LogError("[PiecePlacer] Missing straight_dormant"); return false; }
            if (_straightActivePrefab == null) { Debug.LogError("[PiecePlacer] Missing straight_active"); return false; }
            if (_sourcePadPrefab == null) { Debug.LogError("[PiecePlacer] Missing source_pad"); return false; }
            if (_destPadPrefab == null) { Debug.LogError("[PiecePlacer] Missing dest_pad"); return false; }
            if (_boardSubstratePrefab == null) { Debug.LogError("[PiecePlacer] Missing board_substrate"); return false; }
            return true;
        }
    }
}