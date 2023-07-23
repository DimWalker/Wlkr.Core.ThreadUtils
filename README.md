# Wlkr.Core.ThreadUtils
![DimTechStudio.Com](https://raw.githubusercontent.com/DimWalker/Wlkr.Core.ThreadUtils/e791fcf4d8e50a728675440aa5f89afd63b2b3b5/vx_images/DimTechStudio-Logo.png)
## 项目背景

早在PaddleOCR 2.2版本时期，认识了周杰大佬的PaddleSharp项目，试用其中PaddleOCR时，发现它在改为web api调用时会报错，大概意思是OCR实例的内存只能由其创建的线程才具有访问权限，于是就有了本项目的雏形。 
 
潜伏于大佬Q群中很长时间，这个问题更是老生常谈。虽然后来大佬实现了基于`BlockingCollection`的线程安全示例，不过估计因为README全是英文，还是出现了很多星际玩家。

## 食用方式

项目中的SafeThreadRunner，为了实现更直观的调用方式（`var res = ocr.run(mat)`），使用了3个信号量`SemaphoreSlim`实现了线程安全的轮询方法，它们的作用分别是否空闲，唤醒线程，返回结果。

`SafeThreadRunner<Cls, In, Out>`，可以从泛型的名字猜测，Cls对应OCR的实例（如All、Rec、Det等任务），`In`为输入即Mat，`Out`为输出即Restful规范的返回结果`RestResult<Out>`

本项目实现的SafePaddleOCR为PaddleOcrAll开启Mkldnn的实例，使用方式如下：
```C#
//Warmup
SafePaddleOCR safePaddleOCR = new SafePaddleOCR();
string imgPath = @"../../../../vx_images/DimTechStudio-Logo.png";
var res = safePaddleOCR.Run(imgPath);
Console.Write(@"res: {res.data.Text}");
```

如需要定制自己的线程安全实例，可参考：
```C#
//实例的初始化方法
Func<PaddleOcrAll> initFuc = () =>
{
    Action<PaddleConfig> device = PaddleDevice.Mkldnn();
    var poa = new PaddleOcrAll(LocalFullModels.ChineseV3, device)
    {
        Enable180Classification = true,
        AllowRotateDetection = true,
    };
    return poa;
};
//实例的执行方法
Func<PaddleOcrAll, Mat, RestResult<PaddleOcrResult>> mthdFunc = (cls, source) =>
{
    var res = cls.Run(source);
    return new RestResult<PaddleOcrResult>(res);
};
//声明
SafeThreadRunner<PaddleOcrAll, Mat, PaddleOcrResult> safeThreadRunner = new SafeThreadRunner<PaddleOcrAll, Mat, PaddleOcrResult>(OCRFactory.BuildAllWithMkldnn, OCRFactory.RunAll);
//运行
string imgPath = @"../../../../vx_images/DimTechStudio-Logo.png";
using var mat = Cv2.ImRead(filePath, ImreadModes.AnyColor);
var res = safeThreadRunner.Run(mat);
```

## `SemaphoreSlim`与`BlockingCollection`对比
 * 两边均是单实例测试，性能几乎一样，没有明显差异  
* 多实例测试暂缺  
* 硬捧本项目存在的优势  
> 由于我也重构过周杰的[QueuedPaddleOcrAll.cs](https://github.com/sdcb/PaddleSharp/blob/master/src/Sdcb.PaddleOCR/QueuedPaddleOcrAll.cs)，增加了动态添加/删除实例的功能，但是其使用了Task作为轮询，Task不能像Thread那样，有真正意义上的取消动作。`CancellationToken`实现的取消，最大缺陷是在阻塞时是无效的。故即便我实现了取消，它也必须从blockingCollection.GetConsumingEnumerable()获取到消息执行一次OCR识别，才能释放OCR实例。  
而本项目使用了`SemaphoreSlim`，执行Dispose时只要线程是空闲即可触发释放OCR实例。  

## Author Info
DimWalker
©2022 广州市增城区黯影信息科技部
https://www.dimtechstudio.com/

