using dalleframecon.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCoreAudio;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace dalleframecon.HostedServices
{
    internal class ScreenRenderService : IHostedService
    {
        private Process slideshowProcess;
        private readonly ILogger<ScreenRenderService> _logger;
        private Task _task;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private readonly AzCognitiveServicesWakeWordListener _wakeWordListener;
        private readonly AzCognitiveServicesListener _listener;
        private readonly Dalle2Handler _dalle2Handler;

        private readonly string _notificationSoundFilePath;
        private readonly Player _player;

        public ScreenRenderService(
            ILogger<ScreenRenderService> logger,
            AzCognitiveServicesWakeWordListener wakeWordListener,
            AzCognitiveServicesListener listener,
            Dalle2Handler dalle2Handler)
        {
            _logger = logger;
            _notificationSoundFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Handlers", "bing.mp3");
            _player = new Player();

            _wakeWordListener = wakeWordListener;
            _listener = listener;
            _dalle2Handler = dalle2Handler;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _task = ExecuteAsync(_cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            return _task;
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Executing...");
            
            // Create slideshow txt file
            string directory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "images");
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            var images = new List<string>(Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly));
            images = images.OrderBy(x => Guid.NewGuid()).ToList(); // randomize order of images

            string content = string.Join(Environment.NewLine, images);
            string slideshowFilePath = Path.Combine(directory, "slideshow.txt");
            if (File.Exists(slideshowFilePath))
            {
                File.Delete(slideshowFilePath);
            }
            File.WriteAllText(slideshowFilePath, content);
            StartSlideShow(slideshowFilePath);
           
            Console.WriteLine("Hello.");
            while (true)
            {
                // Wait for wake word/phrase
                _logger.LogInformation("Waiting for wake word...");
                if (!await _wakeWordListener.WaitForWakeWordAsync(cancellationToken))
                {
                    continue;
                }

                _logger.LogInformation("Listening...");
                await _player.Play(_notificationSoundFilePath);
                string userMessage = await _listener.ListenAsync(cancellationToken);
                
                Console.WriteLine($"Drawing: \"{userMessage}\"");
                
                string filePath = await _dalle2Handler.ProcessAsync(userMessage, cancellationToken);
                Console.WriteLine(filePath);
                
                // Update slideshow txt file
                directory = Path.GetDirectoryName(filePath);
                images = new List<string>(Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly));
                images = images.OrderBy(x => Guid.NewGuid()).ToList(); // randomize order of images
                slideshowFilePath = Path.Combine(directory, "slideshow.txt");
                content = filePath + Environment.NewLine + string.Join(Environment.NewLine, images);
                if (File.Exists(slideshowFilePath))
                {
                    File.Delete(slideshowFilePath);
                }

                Console.WriteLine("===");
                Console.WriteLine(content);
                File.WriteAllText(slideshowFilePath, content);

                StopSlideShow();
                StartSlideShow(slideshowFilePath);
            }
        }

        private void StopSlideShow() {
            slideshowProcess.Kill(true);
        }

        private void StartSlideShow(string slideshowPath) {
            slideshowProcess = Process.Start(new ProcessStartInfo()
            {
                FileName = "feh",
                Arguments = $"-Y -x -q -D 30 -B black -F -Z -r -f {slideshowPath}",
                UseShellExecute = true
            });
        }
    }
}