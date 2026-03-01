using System;

#if ENABLE_WINMD_SUPPORT
using Windows.AI.MachineLearning;
using Windows.Storage;
#endif

namespace EgoBlur
{
    public sealed class EgoBlurWinMLMultiBackend : IEgoBlurMultiBackend
    {
#if ENABLE_WINMD_SUPPORT
        private LearningModelSession _faceSession, _lpSession;
        private LearningModelBinding _faceBind, _lpBind;
        private string _faceInput, _lpInput;
#endif

        public void Load(string faceModelPathOnDisk, string lpModelPathOnDisk)
        {
#if ENABLE_WINMD_SUPPORT
            Dispose();
            LoadAsync(faceModelPathOnDisk, lpModelPathOnDisk).AsTask().Wait();
#else
            throw new NotSupportedException("WinML backend requires UWP / ENABLE_WINMD_SUPPORT.");
#endif
        }

#if ENABLE_WINMD_SUPPORT
        private async Windows.Foundation.IAsyncAction LoadAsync(string facePath, string lpPath)
        {
            var faceFile = await StorageFile.GetFileFromPathAsync(facePath);
            var lpFile   = await StorageFile.GetFileFromPathAsync(lpPath);

            var faceModel = await LearningModel.LoadFromStorageFileAsync(faceFile);
            var lpModel   = await LearningModel.LoadFromStorageFileAsync(lpFile);

            _faceSession = new LearningModelSession(faceModel);
            _lpSession   = new LearningModelSession(lpModel);

            _faceBind = new LearningModelBinding(_faceSession);
            _lpBind   = new LearningModelBinding(_lpSession);

            foreach (var f in faceModel.InputFeatures) { _faceInput = f.Name; break; }
            foreach (var f in lpModel.InputFeatures)   { _lpInput   = f.Name; break; }
        }
#endif

        public (EgoBlurResult face, EgoBlurResult lp) RunBoth(float[] inputCHW, float scoreThreshold)
        {
#if ENABLE_WINMD_SUPPORT
            var shape = new long[] { 3, EgoBlurPreprocessorExact.H, EgoBlurPreprocessorExact.W };
            var input = TensorFloat.CreateFromArray(shape, inputCHW);

            var faceRes = RunOne(_faceSession, _faceBind, _faceInput, input, scoreThreshold);
            var lpRes   = RunOne(_lpSession,   _lpBind,   _lpInput,   input, scoreThreshold);
            return (faceRes, lpRes);
#else
            throw new NotSupportedException("WinML backend requires UWP / ENABLE_WINMD_SUPPORT.");
#endif
        }

#if ENABLE_WINMD_SUPPORT
        private static EgoBlurResult RunOne(LearningModelSession sess, LearningModelBinding bind, string inputName, TensorFloat input, float thr)
        {
            bind.Clear();
            bind.Bind(inputName, input);

            var eval = sess.EvaluateAsync(bind, "egoblur").AsTask();
            eval.Wait();
            var outputs = eval.Result.Outputs;

            var boxes = (TensorFloat)outputs.Lookup("boxes");
            var scores = (TensorFloat)outputs.Lookup("scores");
            var labels = (TensorInt64Bit)outputs.Lookup("labels");
            var size = (TensorInt64Bit)outputs.Lookup("image_size");

            var boxesV = boxes.GetAsVectorView();
            var scoresV = scores.GetAsVectorView();
            var labelsV = labels.GetAsVectorView();
            var sizeV = size.GetAsVectorView();

            var res = new EgoBlurResult { imgH = (int)sizeV[0], imgW = (int)sizeV[1] };

            for (int i = 0; i < scoresV.Count; i++)
            {
                float s = scoresV[i];
                if (s < thr) continue;

                int bi = i * 4;
                float x1 = boxesV[bi + 0];
                float y1 = boxesV[bi + 1];
                float x2 = boxesV[bi + 2];
                float y2 = boxesV[bi + 3];

                res.dets.Add(new EgoBlurDet
                {
                    score = s,
                    label = labelsV[i],
                    rect = new UnityEngine.Rect(x1, y1, x2 - x1, y2 - y1)
                });
            }
            return res;
        }
#endif

        public void Dispose()
        {
#if ENABLE_WINMD_SUPPORT
            _faceBind = null; _lpBind = null;
            _faceSession = null; _lpSession = null;
#endif
        }
    }
}
