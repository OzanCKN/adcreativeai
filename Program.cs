using System.Net;
using Newtonsoft.Json;

namespace AdcreativeAI.ImageDownloader
{
    class Program
    {
        private static InputModel _inputModel;
        delegate void DelegateProgressBar();
        private static CancellationTokenSource cancellationTokenSource;

        private static int totalDownloaded;
        private static object lockObject = new object();

        static void Main()
        {
            DelegateProgressBar progressBarEventHandler = null;
            progressBarEventHandler += UpdateProgressBar;
            Console.CancelKeyPress += Console_CancelKeyPress;

            _inputModel = ReadInputFromJson();
            string savePath = $"{Environment.CurrentDirectory}\\{_inputModel.SavePath}";

            totalDownloaded = 0;
            cancellationTokenSource = new CancellationTokenSource();

            Console.WriteLine($"Downloading {_inputModel.Count} images ({_inputModel.MaximumConcurrency}) parallel downloads at most");

            CheckDirectoryRequirement(savePath);

            DownloadImages(savePath, progressBarEventHandler);

            progressBarEventHandler -= UpdateProgressBar;
            Console.CancelKeyPress -= Console_CancelKeyPress;

            Console.ReadKey();
        }

        private static void CheckDirectoryRequirement(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
            Console.WriteLine("Operation cancelled");

            Environment.Exit(0);
        }

        private static InputModel ReadInputFromJson()
        {
            try
            {
                string json = File.ReadAllText("Input.json");
                return JsonConvert.DeserializeObject<InputModel>(json);
            }
            catch (Exception ex)
            {
                _inputModel = new InputModel();
                Console.WriteLine($"Enter the number of images to download:");
                return ReadInputFromConsole();
            }
        }

        private static InputModel ReadInputFromConsole()
        {
            _inputModel = new InputModel();
            Console.WriteLine($"Enter the number of images to download:");
            _inputModel.Count = Convert.ToInt32(Console.ReadLine());

            Console.WriteLine($"Enter the maximum parallel download limit:");
            _inputModel.MaximumConcurrency = Convert.ToInt32(Console.ReadLine());


            return _inputModel;

        }

        private static void DownloadImages(string savePath, DelegateProgressBar progressBarEventHandler)
        {

            using (var throttler = new SemaphoreSlim(_inputModel.MaximumConcurrency))
            {
                for (int i = 0; i < _inputModel.Count; i++)
                {
                    throttler.Wait(cancellationTokenSource.Token);
                    int currentIndex = i + 1;
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            using (WebClient client = new WebClient())
                            {
                                client.DownloadFile(_inputModel.DownloadUrl,
                                    Path.Combine(savePath, $"{currentIndex}.png"));
                            }

                            lock (lockObject)
                            {
                                totalDownloaded++;
                                progressBarEventHandler();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error downloading image {currentIndex}: {ex.Message}");
                        }
                        finally
                        {
                            if (throttler.CurrentCount == 0)
                                throttler.Release();
                        }
                    }, null);

                    if (cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        break;
                    }
                }

                throttler.Wait(1000);

                throttler.Release(_inputModel.MaximumConcurrency);

            }
        }

        static void UpdateProgressBar()
        {
            lock (lockObject)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"Progress: {totalDownloaded}\\{_inputModel.Count}");
            }
        }

    }
}
