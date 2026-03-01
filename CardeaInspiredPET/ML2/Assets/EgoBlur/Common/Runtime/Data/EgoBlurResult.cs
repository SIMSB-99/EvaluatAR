using System.Collections.Generic;

namespace EgoBlur
{
    [System.Serializable]
    public class EgoBlurResult
    {
        public int imgW; // model-space width (expected 1344)
        public int imgH; // model-space height (expected 756)
        public List<EgoBlurDet> dets = new List<EgoBlurDet>(64);
    }
}
