// See https://aka.ms/new-console-template for more information
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.LocalV3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Wlkr.Core.ThreadUtils;
using Wlkr.SafePaddleOCR;



Console.WriteLine("Running Wlkr.SafePaddleOCR.Console...");

string GetCurrentMethodName()
{
    // 使用 StackTrace 获取当前堆栈信息
    StackTrace stackTrace = new StackTrace();
    // 获取调用此方法的方法（第1个元素为 GetCurrentMethodName 方法本身，所以取第2个元素）
    StackFrame frame = stackTrace.GetFrame(1);
    // 获取方法信息
    MethodBase method = frame.GetMethod();
    // 返回方法名
    return method.Name;
}


var tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp");

int numImages = 10, imageWidth = 160, imageHeight = 80;
//生成测试图片
List<Mat> CreatePng()
{
    string outputDirectory = tmpDir;
    if (!Directory.Exists(outputDirectory))
        Directory.CreateDirectory(outputDirectory);
    var font = HersheyFonts.HersheySimplex;

    List<Mat> lst = new List<Mat>();
    for (int digit = 0; digit < numImages; digit++)
    {
        Mat image = new Mat(imageHeight, imageWidth, MatType.CV_8UC3, Scalar.White);
        //单字符识别率很低，改为4位补零
        string digitString = digit.ToString().PadLeft(4, '0');
        var textSize = Cv2.GetTextSize(digitString, font, 1.5, 2, out _);
        int posX = (imageWidth - textSize.Width) / 2;
        int posY = (imageHeight + textSize.Height) / 2;
        Cv2.PutText(image, digitString, new OpenCvSharp.Point(posX, posY), font, 1.5, Scalar.Black, 2);
        string filename = $"{digit}.png";
        string outputPath = System.IO.Path.Combine(outputDirectory, filename);
        Cv2.ImWrite(outputPath, image);
        lst.Add(image);
    }
    Console.WriteLine("Image generation completed.");
    return lst;
}
List<Mat> images = CreatePng();

int thdNumsOCR = 4,
    maxTimes = 10240,
    waitThread = 32;
string warmupImgPath = @"../../../../vx_images/DimTechStudio-Logo.png";
void SafePaddleOCRTest()
{
    Console.WriteLine($"Running {GetCurrentMethodName()}...");
    //Warmup
    //SafePaddleOCR safePaddleOCR = new SafePaddleOCR();    
    //var res = safePaddleOCR.Run(warmupImgPath);
    //Console.WriteLine($"res: {res.data.Text}");

    // 初始化
    List<SafePaddleOCR> lstSPO = new List<SafePaddleOCR>();
    for (int i = 0; i < thdNumsOCR; i++)
    {
        //Warmup
        SafePaddleOCR safePaddleOCR = new SafePaddleOCR();
        var res = safePaddleOCR.Run(warmupImgPath);
        Console.WriteLine($"res: {res.data.Text}");
        lstSPO.Add(safePaddleOCR);
    }

    List<Task<RestResult<PaddleOcrResult>>> lst = new List<Task<RestResult<PaddleOcrResult>>>();
    bool okFlag = true;
    int cnt = 0;
    Stopwatch sw = new Stopwatch();
    sw.Start();
    //伪32线程
    for (int i = 0; i < maxTimes; i++)
    {
        int idx = i;
        lst.Add(Task.Run(() =>
        {
            //string imgPath = Path.Combine(tmpDir, $"{idx % numImages}.png");
            using var mat = images[idx % numImages].Clone();
            RestResult<PaddleOcrResult> res = lstSPO[idx % thdNumsOCR].Run(mat);
            //Console.WriteLine($"{idx} Created.");
            //Console.WriteLine($"{idx} Result: " + res.data.Text);
            return res;
        }));
        cnt++;
        if (lst.Count >= waitThread)
        {
            Task.WaitAll(lst.ToArray());
            foreach (var t in lst)
            {
                okFlag = t.Result.code == "200";
                if (!okFlag)
                {
                    Console.WriteLine(t.Result.msg);
                    break;
                }
            }
            lst.Clear();

            Console.WriteLine($"idx: {idx}");
        }
    }
    if (lst.Count > 0)
    {
        Task.WaitAll(lst.ToArray());
        okFlag = lst.FindAll(t => t.Result.code != "200").Count > 0;
        lst.Clear();
    }
    sw.Stop();
    Console.WriteLine("Total Timespan(ms):" + sw.ElapsedMilliseconds);
    Console.WriteLine("Avg Timespan(ms):" + (sw.ElapsedMilliseconds * 1.0 / cnt));
    Console.WriteLine($"okFlag: {okFlag}");

    lstSPO.ForEach(t => t.Dispose());
}

void QueuedPaddleOcrAllTest()
{
    Console.WriteLine($"Running {GetCurrentMethodName()}...");
    //初始化
    Action<PaddleConfig> device = PaddleDevice.Mkldnn();
    QueuedPaddleOcrAll queuedPaddleOcrAll = new QueuedPaddleOcrAll(() => new PaddleOcrAll(LocalFullModels.ChineseV3, device)
    {
        Enable180Classification = true,
        AllowRotateDetection = true,
    }, consumerCount: thdNumsOCR);

    //无法指定实例Warmup
    List<Task<PaddleOcrResult>> lstRes = new List<Task<PaddleOcrResult>>();
    List<Mat> lstTmpMat = new List<Mat>();
    for (int i = 0; i < (thdNumsOCR * 10); i++)
    {
        var mat = Cv2.ImRead(warmupImgPath, ImreadModes.AnyColor);
        lstTmpMat.Add(mat);
        lstRes.Add(queuedPaddleOcrAll.Run(mat));
    }
    Task.WaitAll(lstRes.ToArray());
    lstTmpMat.ForEach(m => m.Dispose());
    lstRes.Clear();
    lstTmpMat.Clear();

    List<Task<PaddleOcrResult>> lst = new List<Task<PaddleOcrResult>>();
    bool okFlag = true;
    int cnt = 0;
    Stopwatch sw = new Stopwatch();
    sw.Start();
    //伪32线程
    for (int i = 0; i < maxTimes; i++)
    {
        int idx = i;
        lst.Add(Task.Run(async () =>
        {
            //string imgPath = Path.Combine(tmpDir, $"{idx % numImages}.png");
            //using var mat = Cv2.ImRead(warmupImgPath, ImreadModes.AnyColor);
            var mat = images[idx % numImages].Clone();
            lstTmpMat.Add(mat);
            PaddleOcrResult res = await queuedPaddleOcrAll.Run(mat);
            //Console.WriteLine($"{idx} Created.");
            //Console.WriteLine($"{idx} Result: " + res.Text);
            return res;
        }));
        cnt++;
        if (lst.Count >= waitThread)
        {
            Task.WaitAll(lst.ToArray());
            //foreach (var t in lst)
            //{
            //    okFlag = t.Result.code == "200";
            //    if (!okFlag)
            //    {
            //        Console.WriteLine(t.Result.msg);
            //        break;
            //    }
            //}
            lst.Clear();
            lstTmpMat.ForEach(m => m.Dispose());
            lstTmpMat.Clear();

            Console.WriteLine($"idx: {idx}");
        }
    }
    if (lst.Count > 0)
    {
        Task.WaitAll(lst.ToArray());
        //okFlag = lst.FindAll(t => t.Result.code != "200").Count > 0;
        lst.Clear();
    }
    sw.Stop();
    Console.WriteLine("Total Timespan(ms):" + sw.ElapsedMilliseconds);
    Console.WriteLine("Avg Timespan(ms):" + (sw.ElapsedMilliseconds * 1.0 / cnt));
    Console.WriteLine($"okFlag: {okFlag}");

    queuedPaddleOcrAll.Dispose();
}


//SafePaddleOCRTest();
//Console.WriteLine("Press Enter To Continues...");
//Console.ReadLine();
//GC.Collect();
QueuedPaddleOcrAllTest();
Console.WriteLine("Press Enter To Continues...");
Console.ReadLine();
GC.Collect();

