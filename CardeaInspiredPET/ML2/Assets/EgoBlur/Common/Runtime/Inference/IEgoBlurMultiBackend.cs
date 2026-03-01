using System;

namespace EgoBlur
{
    public interface IEgoBlurMultiBackend : IDisposable
    {
        void Load(string faceModelPathOnDisk, string lpModelPathOnDisk);
        (EgoBlurResult face, EgoBlurResult lp) RunBoth(float[] inputCHW, float scoreThreshold);
    }
}
