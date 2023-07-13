using System.Net;
using Newtonsoft.Json;

namespace AdcreativeAI.ImageDownloader
{
    class Program
    {
        delegate void DelegateProgressBar();

        private static InputModel _inputModel;
        private static CancellationTokenSource _cancellationToken;

        private static object _lockObject = new object();

        private static string SavePath = "";

        private static int _totalDownloaded;

        static void Main()
        {
            DelegateProgressBar progressBarEventHandler = null;

            progressBarEventHandler += UpdateProgressBar;
            Console.CancelKeyPress += Console_CancelKeyPress;


            _inputModel = ReadInputFromJson();

            SavePath = $"{Environment.CurrentDirectory}\\{_inputModel.SavePath}";

            _totalDownloaded = 0;
            _cancellationToken = new CancellationTokenSource();

            Console.WriteLine($"Downloading {_inputModel.Count} images ({_inputModel.MaximumConcurrency}) parallel downloads at most");

            CheckDirectoryRequirement(SavePath);

            DownloadImages(progressBarEventHandler);

            progressBarEventHandler -= UpdateProgressBar;


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
            _cancellationToken.Cancel();
            CleanupDownloadedImages(SavePath);

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

            _inputModel.DownloadUrl = "https://picsum.photos/200/300?random=1";

            _inputModel.SavePath = "outputs";

            return _inputModel;

        }

        private static async Task DownloadImages(DelegateProgressBar progressBarEventHandler)
        {

            using (var throttler = new SemaphoreSlim(_inputModel.MaximumConcurrency))
            {
                for (int i = 0; i < _inputModel.Count; i++)
                {
                   await throttler.WaitAsync(_cancellationToken.Token);

                    int currentIndex = i + 1;

                    if (_cancellationToken.Token.IsCancellationRequested)
                    {
                        break;
                    }
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            using (WebClient client = new WebClient())
                            {
                                client.DownloadFile(_inputModel.DownloadUrl,
                                    Path.Combine(SavePath, $"{currentIndex}.png"));
                            }

                            lock (_lockObject)
                            {
                                _totalDownloaded++;
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

                   
                }

                throttler.Wait(1000);

                throttler.Release(_inputModel.MaximumConcurrency);

            }
        }

        static void UpdateProgressBar()
        {
            lock (_lockObject)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"Progress: {_totalDownloaded}\\{_inputModel.Count}");
            }
        }

        private static void CleanupDownloadedImages(string path)
        {
            lock (_lockObject)
            {
                Thread.Sleep(1000);
                string[] files = Directory.GetFiles(path);

                foreach (string file in files)
                {
                    File.Delete(file);
                }
            }
        }

    }
}
