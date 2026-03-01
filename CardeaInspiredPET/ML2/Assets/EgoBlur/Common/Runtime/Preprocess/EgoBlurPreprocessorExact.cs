using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;

namespace EgoBlur
{
    public static class EgoBlurPreprocessorExact
    {
        public const int W = 1344;
        public const int H = 756;

        private static Mat _resized; // reused
        private static byte[] _bgrBytes; // reused

        /// <summary>
        /// Input: BGR CV_8UC3 Mat of any size
        /// Output: float[] CHW (3*H*W) with values 0..255 in BGR order
        /// </summary>
        public static void ToInputCHW_FromBgrMat(Mat bgr8u, float[] outCHW)
        {
            if (_resized == null || _resized.cols() != W || _resized.rows() != H)
                _resized = new Mat(H, W, CvType.CV_8UC3);

            Imgproc.resize(bgr8u, _resized, new Size(W, H), 0, 0, Imgproc.INTER_LINEAR);

            int bytesNeeded = W * H * 3;
            if (_bgrBytes == null || _bgrBytes.Length != bytesNeeded)
                _bgrBytes = new byte[bytesNeeded];

            _resized.get(0, 0, _bgrBytes);

            // Pack to CHW float (B then G then R), still 0..255
            int hw = W * H;
            int c0 = 0;
            int c1 = hw;
            int c2 = hw * 2;

            // _bgrBytes is interleaved BGRBGR...
            // outCHW is planar BBB.. GGG.. RRR..
            int idx = 0;
            for (int i = 0; i < hw; i++)
            {
                byte b = _bgrBytes[idx++];
                byte g = _bgrBytes[idx++];
                byte r = _bgrBytes[idx++];

                outCHW[c0 + i] = b;
                outCHW[c1 + i] = g;
                outCHW[c2 + i] = r;
            }
        }
    }
}
