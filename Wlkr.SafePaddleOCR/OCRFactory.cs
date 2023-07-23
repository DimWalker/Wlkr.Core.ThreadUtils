using Sdcb.PaddleInference;
using Sdcb.PaddleOCR.Models.LocalV3;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using Wlkr.Core.ThreadUtils;

namespace Wlkr.SafePaddleOCR
{
    public class OCRFactory
    {
        public static PaddleOcrAll BuildAllWithMkldnn(FullOcrModel fullOcrModel)
        {
            Action<PaddleConfig> device = PaddleDevice.Mkldnn();

            var poa = new PaddleOcrAll(fullOcrModel, device)
            {
                Enable180Classification = true,
                AllowRotateDetection = true,
            };
            return poa;
        }

        public static PaddleOcrAll BuildAllWithMkldnn()
        {
            return BuildAllWithMkldnn(LocalFullModels.ChineseV3);
        }

        public static RestResult<PaddleOcrResult> RunAll(PaddleOcrAll cls, Mat source)
        {
            var res = cls.Run(source);
            return new RestResult<PaddleOcrResult>(res);
        }
    }
}
