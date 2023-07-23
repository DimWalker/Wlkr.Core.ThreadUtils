[![DimTechStudio.Com](https://raw.githubusercontent.com/DimWalker/Wlkr.Core.ThreadUtils/master/vx_images/DimTechStudio-Logo.png)](https://www.dimtechstudio.com/)

# Wlkr.Core.ThreadUtils

## 项目背景

早在PaddleOCR 2.2版本时期，认识了周杰大佬的PaddleSharp项目，试用其中PaddleOCR时，发现它在改为web api调用时会报错，大概意思是OCR实例的内存只能由其创建的线程才具有访问权限，于是就有了本项目的雏形。 
 
潜伏于大佬Q群中很长时间，这个问题更是老生常谈。虽然后来大佬实现了基于`BlockingCollection`的线程安全示例，不过估计因为README全是英文，还是出现了很多星际玩家。

## 食用方式

项目中的SafeThreadRunner，为了实现更直观的调用方式（`var res = ocr.run(mat)`），使用了3个信号量`SemaphoreSlim`实现了线程安全的轮询方法，它们的作用分别是否空闲，唤醒线程，返回结果。

`SafeThreadRunner<Cls, In, Out>`，可以从泛型的名字猜测，Cls对应OCR的实例（如All、Rec、Det等任务），`In`为输入即Mat，`Out`为输出即Restful规范的返回结果`RestResult<Out>`。

nuget安装包参考命令

```powershell
# 新建一个console项目
dotnet new console
# 添加nuget包
dotnet add package Wlkr.SafePaddleOCR
```

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
* 单实例测试：性能几乎一样，没有明显差异  
* 多实例测试：报错！！！  
> 测试用的机器是笔记本 CPU  R7 5800H，内存32G。  
两种方式均会报错，实例数越多，报错概率越高，错误提示依然内存错误的问题。  
System.AccessViolationException: Attempted to read or write protected memory. This is often an indication that other memory is corrupt.  
另外SemaphoreSlim的方式比BlockingCollection的方式出现的更频繁，尤其在4实例时基本无法完成10240次OCR测试。  
两者在出现代码位置也不经相同，Det、Cls、Rec三种模型预测时均可能错误。  
* 周杰大佬的项目优势：实现生产者消费者模式  
* 本项目优势：单实例Dispose  
> 由于我也重构过周杰大佬的[QueuedPaddleOcrAll.cs](https://github.com/sdcb/PaddleSharp/blob/master/src/Sdcb.PaddleOCR/QueuedPaddleOcrAll.cs)，其Dispose方式只能释放所有实例。虽然我增加了动态添加/删除实例的功能，但其使用了Task作为轮询的载体，Task不能像Thread那样有真正意义上的取消动作。`CancellationToken`实现的取消最大缺陷是在阻塞时是无效的。即便我实现了取消，它也必须从blockingCollection.GetConsumingEnumerable()获取到消息执行一次OCR识别，才能释放OCR实例，极端情况下等于无法是释放。  
而本项目使用了`SemaphoreSlim`，执行Dispose时只要线程是空闲即可触发OCR实例的释放。  

## 测试数据
* 硬件配置： CPU  R7 5800H（8核16线程主频3.2GHz），内存32G
* 测试图片：10张，数字0000~0009，宽高160*80，类型png
* 风扇转速常开最大，排除CPU温度影响
* OCR实例参数：All，CPU，Mkldnn
> 由于10240次大概率报错，无法完成测试，这里改为256次。  

|                    |     平均毫秒 |    平均毫秒 |    平均毫秒 |
| :----------------- | ----------: | ---------: | ---------: |
| OCR次数             |         256 |        256 |        256 |
| 实例数              |           1 |          2 |          4 |
| SemaphoreSlim      | 27.36328125 |  17.640625 | 12.6328125 |
| BlockingCollection | 27.74609375 | 17.8984375 |  12.640625 |

* 思路转换  
> 从单进程4实例，改为4进程1实例测试，测试了1次没有报错，每个进程10240次，平均50ms。  
* 测试缺陷：
    * 居然没用web api来测试而是用了console测试，与项目背景背道而驰……
    * 由于用的图片较少，没法作为内存压力测试的参考

## 总结
基于思路转换的测试，虽然测试次数少了点，不过目前来看，当作为web api时，1进程1实例，多开进程，利用负载均衡来提高并发和服务器利用率，为最优方案。

## Author Info
DimWalker
©2023 广州市增城区黯影信息科技部
https://www.dimtechstudio.com/