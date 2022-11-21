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
using System.Text.Json;

namespace dalleframecon.HostedServices
{
    internal class ScreenRenderService : IHostedService
    {
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
            while (true)
            {
                //Wait for wake word or phrase

                _logger.LogDebug("Waiting for wake word...");
                if (!await _wakeWordListener.WaitForWakeWordAsync(cancellationToken))
                {
                    continue;
                }

                _logger.LogDebug("Listening...");
                await _player.Play(_notificationSoundFilePath);
                string userMessage = await _listener.ListenAsync(cancellationToken);

                string filePath = await _dalle2Handler.ProcessAsync(userMessage, cancellationToken);
                //string filePath = await _dalle2Handler.ProcessAsync("A 3D render of two astronauts shaking hands in a mid-century modern living room.", cancellationToken);

                Process.Start(new ProcessStartInfo()
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }

            
        }
    }
}