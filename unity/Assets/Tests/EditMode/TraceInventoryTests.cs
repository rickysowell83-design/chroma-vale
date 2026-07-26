using NUnit.Framework;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Edit-mode tests for TraceInventory — engine-free placement/undo/burst tracking.
    /// Tests: TryPlace, TryUndo (3-undo cap), MarkShorted, GetEfficiency, ResetAll.
    /// C# 9.0 compatible (no file-scoped namespaces).
    /// </summary>
    public class TraceInventoryTests
    {
        private TraceSegment[] _threeStraights;
        private GridBoard _board3x1;
        private LevelData _simpleLevel;

        [SetUp]
        public void Setup()
        {
            _threeStraights = new[]
            {
                TraceSegment.Straight(2),
                TraceSegment.Straight(2),
                TraceSegment.Straight(2),
            };

            _simpleLevel = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = _threeStraights,
            };

            _board3x1 = new GridBoard(_simpleLevel);
        }

        // ── TryPlace ──

        [Test]
        public void TryPlace_ValidPlacement_ReturnsTrueAndUpdatesState()
        {
            var inventory = new TraceInventory(_threeStraights);

            bool result = inventory.TryPlace(0, _board3x1, 1, 0);

            Assert.IsTrue(result);
            Assert.AreEqual(SegmentState.Placed, _threeStraights[0].State);
            Assert.AreEqual(1, inventory.PlacedCount);
            Assert.AreEqual(2, inventory.AvailableCount);
        }

        [Test]
        public void TryPlace_OnOccupiedCell_ReturnsFalse()
        {
            var inventory = new TraceInventory(_threeStraights);
            inventory.TryPlace(0, _board3x1, 1, 0); // Place piece 0 at (1,0)

            // Try to place piece 1 at the same cell
            bool result = inventory.TryPlace(1, _board3x1, 1, 0);

            Assert.IsFalse(result);
            Assert.AreEqual(SegmentState.InHand, _threeStraights[1].State);
            Assert.AreEqual(1, inventory.PlacedCount);
        }

        [Test]
        public void TryPlace_AlreadyPlacedPiece_ReturnsFalse()
        {
            var inventory = new TraceInventory(_threeStraights);
            inventory.TryPlace(0, _board3x1, 1, 0);

            // Try to place same piece again at a different cell
            // Grid 3x1 only has one valid empty cell (1,0), so this tests the piece state check
            bool result = inventory.TryPlace(0, _board3x1, 1, 0);

            // Should be false because piece 0.State is already Placed
            Assert.IsFalse(result);
        }

        // ── TryUndo ──

        [Test]
        public void TryUndo_ReturnsPieceToHandAndDecrementsUndos()
        {
            var inventory = new TraceInventory(_threeStraights);
            inventory.TryPlace(0, _board3x1, 1, 0);
            Assert.AreEqual(1, inventory.PlacedCount);

            bool undoResult = inventory.TryUndo(_board3x1);

            Assert.IsTrue(undoResult);
            Assert.AreEqual(SegmentState.InHand, _threeStraights[0].State);
            Assert.AreEqual(2, inventory.UndosRemaining); // Started with 3, used 1
            Assert.AreEqual(0, inventory.PlacedCount);
            Assert.AreEqual(3, inventory.AvailableCount);
        }

        [Test]
        public void TryUndo_FourthUndo_ReturnsFalse()
        {
            var level = new LevelData
            {
                Width = 6,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 5, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[]
                {
                    TraceSegment.Straight(2), TraceSegment.Straight(2),
                    TraceSegment.Straight(2), TraceSegment.Straight(2),
                },
            };
            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory, maxUndos: 3);

            Assert.IsTrue(inventory.TryPlace(0, board, 1, 0));
            Assert.IsTrue(inventory.TryPlace(1, board, 2, 0));
            Assert.IsTrue(inventory.TryPlace(2, board, 3, 0));

            Assert.IsTrue(inventory.TryUndo(board)); // undo #1: removes piece at (3,0)
            Assert.IsTrue(inventory.TryUndo(board)); // undo #2: removes piece at (2,0)
            Assert.IsTrue(inventory.TryUndo(board)); // undo #3: removes piece at (1,0)

            // Now UndosRemaining should be 0, 4th undo should fail
            Assert.AreEqual(0, inventory.UndosRemaining);
            Assert.IsFalse(inventory.TryUndo(board));
        }

        // ── MarkShorted ──

        [Test]
        public void MarkShorted_SetsBurstStateAndIncrementsShortedCount()
        {
            var inventory = new TraceInventory(_threeStraights);
            inventory.TryPlace(0, _board3x1, 1, 0);

            bool marked = inventory.MarkShorted(1, 0);

            Assert.IsTrue(marked);
            Assert.AreEqual(1, inventory.ShortedCount);
            Assert.AreEqual(SegmentState.Shorted, _threeStraights[0].State);

            // Burst pieces should not be restorable via undo
            // (TryUndo only undoes the last _placementMap entry, which is still there
            //  since burst doesn't remove from placement map — but the piece state is Burst)
            Assert.AreEqual(SegmentState.Shorted, _threeStraights[0].State);
        }

        [Test]
        public void MarkShorted_OnEmptyCell_ReturnsFalse()
        {
            var inventory = new TraceInventory(_threeStraights);

            bool marked = inventory.MarkShorted(5, 5);

            Assert.IsFalse(marked);
            Assert.AreEqual(0, inventory.ShortedCount);
        }

        // ── GetEfficiency ──

        [Test]
        public void GetEfficiency_ReturnsUnusedRatio()
        {
            // TraceInventory.GetEfficiency() = AvailableCount / total = unused / total
            // 0 = all used, 1 = none used
            var inventory = new TraceInventory(_threeStraights);

            // All 3 in hand: efficiency = 3/3 = 1.0
            Assert.AreEqual(1.0f, inventory.GetEfficiency());

            // Place 2: 1 in hand, 3 total = 1/3 ≈ 0.333
            inventory.TryPlace(0, _board3x1, 1, 0);
            Assert.AreEqual(2.0f / 3.0f, inventory.GetEfficiency(), 0.001f);

            // Place all 3 can't since board is 3×1 and (2,0) is target
        }

        [Test]
        public void GetEfficiency_EmptyInventory_ReturnsOne()
        {
            var inventory = new TraceInventory(System.Array.Empty<TraceSegment>());
            Assert.AreEqual(1.0f, inventory.GetEfficiency());
        }

        // ── ResetAll ──

        [Test]
        public void ResetAll_RestoresAllPiecesAndUndos()
        {
            var inventory = new TraceInventory(_threeStraights);
            inventory.TryPlace(0, _board3x1, 1, 0);
            inventory.TryUndo(_board3x1);
            inventory.TryPlace(1, _board3x1, 1, 0);
            inventory.MarkShorted(1, 0);

            inventory.ResetAll();

            // PINNED BEHAVIOR: ResetAll restores Placed → InHand and resets undos,
            // but Burst pieces stay Burst (destroyed pipes are not resurrected).
            // So only 2 of 3 pieces return to hand.
            Assert.AreEqual(2, inventory.AvailableCount);
            Assert.AreEqual(0, inventory.PlacedCount);
            Assert.AreEqual(3, inventory.UndosRemaining);
        }

        // ── GetAvailablePieces / GetCount ──

        [Test]
        public void GetAvailablePieces_ReturnsOnlyInHand()
        {
            var inventory = new TraceInventory(_threeStraights);
            inventory.TryPlace(0, _board3x1, 1, 0);

            var available = inventory.GetAvailablePieces();

            Assert.AreEqual(2, available.Length);
            Assert.AreEqual(SegmentState.InHand, available[0].State);
            Assert.AreEqual(SegmentState.InHand, available[1].State);
        }

        [Test]
        public void GetCount_FiltersByShape()
        {
            var pieces = new TraceSegment[]
            {
                TraceSegment.Straight(2),
                TraceSegment.Straight(2),
                TraceSegment.Corner(2),
            };
            var inventory = new TraceInventory(pieces);

            int count = inventory.GetCount(SegmentShape.Straight);
            Assert.AreEqual(2, count);
        }
    }
}
