using UnityEngine;

namespace EgoBlur
{
    [System.Serializable]
    public struct EgoBlurDet
    {
        public Rect rect;     // xywh (top-left origin in image coords)
        public float score;
        public long label;    // ONNX output uses int64
    }
}
