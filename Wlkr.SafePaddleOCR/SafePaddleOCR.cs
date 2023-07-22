using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.LocalV3;
using Wlkr.Core.ThreadUtils;

namespace Wlkr.SafePaddleOCR
{
    public class SafePaddleOCR : IDisposable
    {
        private SafeThreadRunner<PaddleOcrAll, Mat, PaddleOcrResult> safeThreadRunner;
        public SafePaddleOCR()
        {
            safeThreadRunner = new SafeThreadRunner<PaddleOcrAll, Mat, PaddleOcrResult>(OCRFactory.BuildAllWithMkldnn, OCRFactory.RunAll);
        }

        public RestResult<PaddleOcrResult> Run(Mat mat)
        {
            var res = safeThreadRunner.Run(mat);
            return res;
        }

        public Task<RestResult<PaddleOcrResult>> RunAsync(Mat mat)
        {
            Task<RestResult<PaddleOcrResult>> task = Task.Run(() =>
            {
                return Run(mat);
            });
            return task;
        }

        public RestResult<PaddleOcrResult> Run(string filePath)
        {
            var mat = Cv2.ImRead(filePath, ImreadModes.AnyColor);
            var res = safeThreadRunner.Run(mat);
            return res;
        }

        public Task<RestResult<PaddleOcrResult>> RunAsync(string imgPath)
        {
            Task<RestResult<PaddleOcrResult>> task = Task.Run(() =>
            {
                return Run(imgPath);
            });
            return task;
        }

        public void Dispose()
        {
            safeThreadRunner.Dispose();
        }
    }
}