using dalleframecon.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Drawing;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace dalleframecon.Handlers
{
    internal class Dalle2Handler
    {
        private readonly ILogger<Dalle2Handler> _logger;
        private readonly OpenAiServiceOptions _options;

        public Dalle2Handler(
            ILogger<Dalle2Handler> logger,
            IOptions<OpenAiServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task<string> ProcessAsync(string input, CancellationToken cancellationToken)
        {
            OpenAiImageRequestContent openAiRequest = new OpenAiImageRequestContent()
            {
                prompt = input,
                n = 1,
                size = "1024x1024",
                response_format = "b64_json",
                user = "adribona"
            };

            using HttpRequestMessage request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.openai.com/v1/images/generations"),
                Content = JsonContent.Create(openAiRequest, MediaTypeHeaderValue.Parse("application/json"), JsonSerializerOptions.Default)            
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Key);


            _logger.LogDebug("Generating image...");
            using HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"{response.StatusCode}: {response.ReasonPhrase}");
                return string.Empty;
            }

            OpenAiImageRequestResponse content = await response.Content.ReadFromJsonAsync<OpenAiImageRequestResponse>(JsonSerializerOptions.Default, cancellationToken);

            
            string b64 = content.data[0].b64_json;
            string fileName = $"{DateTime.Now.ToString("yyyyMMddTHHmmss")}.png";
            string filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);

            _logger.LogDebug($"Saving image to {filePath}");
            File.WriteAllBytes(filePath, Convert.FromBase64String(b64));

            // we need to add 796 pixels in total width = 398 pixels to each side.

            using Image image = Image.Load(filePath);
            int width = 1820;
            int height = 1024;
            image.Mutate(context => context.Resize(width, height, false));
            image.Save(filePath);

            return filePath;
        }
        
        private class OpenAiImageRequestResponse
        {
            public int created { get; set; }
            public DataItem[] data { get; set; }

            public class DataItem 
            { 
                public string b64_json { get; set; }
                public string url { get; set; }
            }
            
        }

        private class OpenAiImageRequestContent
        {
            /// <summary>
            /// A text description of the desired image(s). The maximum length is 1000 characters.
            /// </summary>
            public string prompt { get; set; }
            
            /// <summary>
            /// The number of images to generate. Must be between 1 and 10.
            /// </summary>
            public int n { get; set; }

            /// <summary>
            /// The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024
            /// </summary>
            public string size { get; set; }

            /// <summary>
            /// The format in which the generated images are returned. Must be one of url or b64_json
            /// </summary>
            public string response_format { get; set; }

            /// <summary>
            /// A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse
            /// </summary>
            public string user { get; set; }
        }
    }
}
