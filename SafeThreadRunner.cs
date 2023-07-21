namespace Wlkr.Core.ThreadUtils
{
    public class SafeThreadRunner<Cls, In, Out, T> where Cls : IDisposable
        where Out : RestResult<T>, new()
    {
        private SemaphoreSlim safeSrcSlim = new SemaphoreSlim(1, 1);
        private SemaphoreSlim safeRunSlim = new SemaphoreSlim(0, 1);
        private SemaphoreSlim safeResSlim = new SemaphoreSlim(0, 1);

        private In Source;
        private RestResult<T> Result;

        private Thread safeThread;
        private Func<Cls> initFunc;
        private Func<Cls, In, RestResult<T>> runFunc;

        public bool IsDisposed { get; private set; } = false;
        public SafeThreadRunner(Func<Cls> _initFunc, Func<Cls, In, RestResult<T>> _runFunc)
        {
            if (_initFunc == null)
                throw new ArgumentNullException(nameof(initFunc));
            if (_runFunc == null)
                throw new ArgumentNullException(nameof(_runFunc));
            initFunc = _initFunc;
            runFunc = _runFunc;
            safeThread = new Thread(new ThreadStart(RunByThread));
            safeThread.IsBackground = true;
            safeThread.Start();
        }
        private void RunByThread()
        {
            using Cls cls = initFunc();
            while (true)
            {
                safeRunSlim.Wait();
                if (IsDisposed)
                    return;
                try
                {
                    Result = runFunc(cls, Source);
                }
                catch (Exception ex)
                {
                    Result = new RestResult<T>()
                    {
                        code = "500",
                        msg = ex.Message
                    };
                }
                finally
                {
                    safeResSlim.Release();
                }
            }
        }

        public Task<RestResult<T>> RunAsync(In src)
        {
            Task<RestResult<T>> task = Task.Run(() =>
            {
                return Run(src);
            });
            return task;
        }
        public RestResult<T> Run(In src)
        {
            //是否空闲
            safeSrcSlim.Wait();
            //设置Source
            Source = src;
            //恢复线程，运行runFunc
            safeRunSlim.Release();
            //等待runFunc结果
            safeResSlim.Wait();
            //释放信号量，设为空闲
            safeSrcSlim.Release();
            return Result;
        }


        public void Dispose()
        {
            IsDisposed = true;
            //是否空闲
            safeSrcSlim.Wait();
            //恢复线程，释放runFunc实例
            safeRunSlim.Release();
        }
    }

    public class RestResult
    {
        public string code { get; set; }
        public string msg { get; set; }

        public RestResult()
        {
            code = "200";
        }
    }

    public class RestResult<T> : RestResult
    {
        public T data { get; set; }

        public RestResult(T _data) : base()
        {
            data = _data;
        }
        public RestResult() : base()
        {
        }
    }

}