namespace AdcreativeAI.ImageDownloader
{
    public record InputModel
    {
        public int Count { get; set; }
        public int MaximumConcurrency { get; set; }
        public string SavePath { get; set; }
        public string DownloadUrl { get; set; }
    }
}
