namespace ChromaVale.Core.GameLogic
{
    public interface ITraceRouter
    {
        bool CanPlace(int x, int y, int colorIndex);
        void Place(int x, int y, int colorIndex);
        void Undo();
        bool IsPathConnected(int sourceX, int sourceY, int targetX, int targetY);
    }
}
