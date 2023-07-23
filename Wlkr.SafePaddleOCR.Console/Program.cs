// See https://aka.ms/new-console-template for more information
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.LocalV3;
using System.Diagnostics;
using Wlkr.Core.ThreadUtils;
using Wlkr.SafePaddleOCR;

Console.WriteLine("Running Wlkr.SafePaddleOCR.Console...");

var tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp");

int numImages = 10, imageWidth = 160, imageHeight = 80;
//生成测试图片
void CreatePng()
{
    string outputDirectory = tmpDir;
    if (!Directory.Exists(outputDirectory))
        Directory.CreateDirectory(outputDirectory);
    var font = HersheyFonts.HersheySimplex;
    for (int digit = 0; digit < numImages; digit++)
    {
        using (Mat image = new Mat(imageHeight, imageWidth, MatType.CV_8UC3, Scalar.White))
        {
            //单字符识别率很低，改为4位补零
            string digitString = digit.ToString().PadLeft(4, '0');
            var textSize = Cv2.GetTextSize(digitString, font, 1.5, 2, out _);
            int posX = (imageWidth - textSize.Width) / 2;
            int posY = (imageHeight + textSize.Height) / 2;
            Cv2.PutText(image, digitString, new OpenCvSharp.Point(posX, posY), font, 1.5, Scalar.Black, 2);
            string filename = $"{digit}.png";
            string outputPath = System.IO.Path.Combine(outputDirectory, filename);
            Cv2.ImWrite(outputPath, image);
        }
    }
    Console.WriteLine("Image generation completed.");
}
CreatePng();

//Warmup
SafePaddleOCR safePaddleOCR = new SafePaddleOCR();
string imgPath = @"../../../../../vx_images/DimTechStudio-Logo.png";
var res = safePaddleOCR.Run(imgPath);
Console.WriteLine($"res: {res.data.Text}");

List<Task<RestResult<PaddleOcrResult>>> lst = new List<Task<RestResult<PaddleOcrResult>>>();
bool okFlag = true;
int cnt = 0;
Stopwatch sw = new Stopwatch();
sw.Start();
//伪32线程
for (int i = 0; i < 10240; i++)
{
    int idx = i;
    cnt++;
    lst.Add(Task.Run(() =>
    {
        string imgPath = Path.Combine(tmpDir, $"{idx % numImages}.png");
        RestResult<PaddleOcrResult> res = safePaddleOCR.Run(imgPath);
        Console.WriteLine($"{idx} Created.");
        Console.WriteLine($"{idx} Result: " + res.data.Text);
        return res;
    }));
    if (lst.Count >= 32)
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
