using System.Numerics;
using Dalamud.Interface.Utility;

namespace AetherBlackbox.DrawingLogic
{
    public class CanvasProjector
    {
        private readonly float _scale;
        private readonly Vector2 _canvasCenter;
        private readonly Vector3 _worldCenter;

        public CanvasProjector(Vector2 canvasSize, Vector3 worldCenter, float zoom)
        {
            _scale = 8f * ImGuiHelpers.GlobalScale * zoom;
            _canvasCenter = canvasSize / 2;
            _worldCenter = worldCenter;
        }

        public Vector2 WorldToCanvas(Vector3 worldPos)
        {
            var relative = worldPos - _worldCenter;
            return new Vector2(
                _canvasCenter.X + (relative.X * _scale),
                _canvasCenter.Y + (relative.Z * _scale)
            );
        }

        public float ScaledUnit(float unit) => unit * _scale;
    }
}