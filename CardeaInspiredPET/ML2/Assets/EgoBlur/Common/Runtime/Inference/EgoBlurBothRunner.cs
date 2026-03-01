using System.Collections;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using System;

namespace EgoBlur
{
    public class EgoBlurBothRunner : MonoBehaviour
    {
        //public string faceModelFile = "/data/data/com.UnityTechnologies.com.unity.template.urpblank/EgoBlurModels/egoblur_face_gen1.onnx";
        //public string lpModelFile = "/data/data/com.UnityTechnologies.com.unity.template.urpblank/EgoBlurModels/egoblur_lp_gen1.onnx";

        public bool ready { get; private set; }

        private IEgoBlurMultiBackend _backend;
        private float[] _inputCHW;

        private string _facePathOnDisk;
        private string _lpPathOnDisk;

        //IEnumerator Start()
        //{
        //    _backend = EgoBlurMultiBackendFactory.Create();
        //    _inputCHW = new float[3 * EgoBlurPreprocessorExact.H * EgoBlurPreprocessorExact.W];

        //    //yield return StreamingAssetsUtil.EnsureStreamingAssetOnDisk(faceModelFile, p => _facePathOnDisk = p);
        //    //yield return StreamingAssetsUtil.EnsureStreamingAssetOnDisk(lpModelFile, p => _lpPathOnDisk = p);
        //    _facePathOnDisk = "/data/data/com.UnityTechnologies.com.unity.template.urpblank/EgoBlurModels/egoblur_face_gen1.onnx";
        //    _lpPathOnDisk = "/data/data/com.UnityTechnologies.com.unity.template.urpblank/EgoBlurModels/egoblur_lp_gen1.onnx";

        //    _backend.Load(_facePathOnDisk, _lpPathOnDisk);
        //    ready = true;
        //    yield break;
        //}

        IEnumerator Start()
        {
            _backend = EgoBlurMultiBackendFactory.Create();
            _inputCHW = new float[3 * EgoBlurPreprocessorExact.H * EgoBlurPreprocessorExact.W];

            yield return SetPath("/data/data/com.UnityTechnologies.com.unity.template.urpblank/EgoBlurModels/egoblur_face_gen1.onnx", p => _facePathOnDisk = p);
            yield return SetPath("/data/data/com.UnityTechnologies.com.unity.template.urpblank/EgoBlurModels/egoblur_lp_gen1.onnx", p => _lpPathOnDisk = p);

            _backend.Load(_facePathOnDisk, _lpPathOnDisk);
            ready = true;
        }

        private IEnumerator SetPath(string path, Action<string> callback)
        {
            callback(path);
            yield return null;
        }

        void OnDestroy() => _backend?.Dispose();

        public (EgoBlurResult face, EgoBlurResult lp) RunBothOnBgrMat(Mat bgr8u, float scoreThreshold)
        {
            if (!ready) return (null, null);

            EgoBlurPreprocessorExact.ToInputCHW_FromBgrMat(bgr8u, _inputCHW);
            Debug.Log("About to call _backend.RunBoth");
            return _backend.RunBoth(_inputCHW, scoreThreshold);
        }
    }
}
