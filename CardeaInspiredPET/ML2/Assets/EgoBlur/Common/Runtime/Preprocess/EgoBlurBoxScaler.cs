using UnityEngine;

namespace EgoBlur
{
    public static class EgoBlurBoxScaler
    {
        // Direct resize mapping: orig -> (1344x756)
        public static Rect ModelToOriginal(Rect rModel, int origW, int origH)
        {
            float sx = (float)origW / EgoBlurPreprocessorExact.W;
            float sy = (float)origH / EgoBlurPreprocessorExact.H;

            return new Rect(
                rModel.x * sx,
                rModel.y * sy,
                rModel.width * sx,
                rModel.height * sy
            );
        }

        public static Rect ClampToImage(Rect r, int w, int h)
        {
            float x1 = Mathf.Clamp(r.xMin, 0, w - 1);
            float y1 = Mathf.Clamp(r.yMin, 0, h - 1);
            float x2 = Mathf.Clamp(r.xMax, 0, w - 1);
            float y2 = Mathf.Clamp(r.yMax, 0, h - 1);
            return Rect.MinMaxRect(x1, y1, x2, y2);
        }
    }
}
