// See https://aka.ms/new-console-template for more information
using Sdcb.PaddleOCR;
using System.Diagnostics;
using Wlkr.Core.ThreadUtils;
using Wlkr.SafePaddleOCR;

Console.WriteLine("Running Wlkr.SafePaddleOCR.Console...");
//Warmup
SafePaddleOCR safePaddleOCR = new SafePaddleOCR();
string imgPath = @"../../../../../vx_images/DimTechStudio-Logo.png";
var res = safePaddleOCR.Run(imgPath);
Console.WriteLine($"res: {res.data.Text}");

List<Task<RestResult<PaddleOcrResult>>> lst = new List<Task<RestResult<PaddleOcrResult>>>();
bool okFlag = true;
//伪32线程
Stopwatch sw = new Stopwatch();
sw.Start();

for (int i = 0; i < 10240; i++)
{
    //int idx = i;
    lst.Add(safePaddleOCR.RunAsync(imgPath));
    if (lst.Count >= 32)
    {
        Console.WriteLine($"i: {i}");
        Task.WaitAll(lst.ToArray());
        sw.Stop();
        Console.WriteLine("Timespan(ms):" + sw.ElapsedMilliseconds);
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
Console.WriteLine($"okFlag: {okFlag}");
