using dalleframecon.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
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
        private readonly AzCognitiveServicesSpeaker _speaker;

        public Dalle2Handler(
            ILogger<Dalle2Handler> logger,
            AzCognitiveServicesSpeaker speaker,
            IOptions<OpenAiServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _speaker = speaker;
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
            
            _speaker.SpeakAsync("Sure thing, one moment.", cancellationToken); // don't wait, just talk.
            
            using HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"{response.StatusCode}: {response.ReasonPhrase}");
                return string.Empty;
            }

            OpenAiImageRequestResponse content = await response.Content.ReadFromJsonAsync<OpenAiImageRequestResponse>(JsonSerializerOptions.Default, cancellationToken);

            string timestamp = DateTime.Now.ToString("yyyyMMddTHHmmss");
            string fileNameOriginal = $"{timestamp}_orig.png";
            string fileNameLeft = $"{timestamp}_left.png";
            string fileNameLeftFinal = $"{timestamp}_left_final.png";
            string fileNameRight = $"{timestamp}_right.png";
            string fileNameRightFinal = $"{timestamp}_right_final.png";
            string fileNameFinal = $"{timestamp}_final.png";
            string fileDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string filePathOriginal = Path.Combine(fileDirectory, fileNameOriginal);
            string filePathLeft = Path.Combine(fileDirectory, fileNameLeft);
            string filePathLeftFinal = Path.Combine(fileDirectory, fileNameLeftFinal);
            string filePathRight = Path.Combine(fileDirectory, fileNameRight);
            string filePathRightFinal = Path.Combine(fileDirectory, fileNameRightFinal);
            string filePathFinal = Path.Combine(fileDirectory, fileNameFinal);

            PngEncoder pngEncoder = new PngEncoder() { ColorType = PngColorType.RgbWithAlpha };

            _logger.LogDebug($"Saving first image to {filePathOriginal}");
            File.WriteAllBytes(filePathOriginal, Convert.FromBase64String(content.data[0].b64_json));

            {
                // Re-encode to RGBA
                using Image imageOriginal = Image.Load(filePathOriginal);
                await imageOriginal.SaveAsPngAsync(filePathOriginal, pngEncoder);

                int width = 1820;
                int height = 1024;

                using Image imageLeftPad = Image.Load(filePathOriginal);
                imageLeftPad.Mutate(c => c.Pad(width, height, Color.Transparent).Crop(new Rectangle(0, 0, 1024, 1024)));
                await imageLeftPad.SaveAsPngAsync(filePathLeft, pngEncoder);

                using Image imageRightPad = Image.Load(filePathOriginal);
                imageRightPad.Mutate(c => c.Pad(width, height, Color.Transparent).Crop(new Rectangle(1820 - 1024, 0, 1024, 1024)));
                await imageRightPad.SaveAsPngAsync(filePathRight, pngEncoder);
            }

            //////////////////
            ///


            using MultipartFormDataContent leftFormContent = new MultipartFormDataContent();
            StreamContent leftFileStream = new StreamContent(File.OpenRead(filePathLeft));
            leftFileStream.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            leftFormContent.Add(leftFileStream, name: "image", fileName: fileNameLeft);
            leftFormContent.Add(new StringContent(input), "prompt");
            leftFormContent.Add(new StringContent("1"), "n");
            leftFormContent.Add(new StringContent("1024x1024"), "size");
            leftFormContent.Add(new StringContent("b64_json"), "response_format");
            leftFormContent.Add(new StringContent("adribona"), "user");

            using HttpRequestMessage leftRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.openai.com/v1/images/edits"),
                Content = leftFormContent
            };
            leftRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Key);

            



            ///////////////
            ///

            using MultipartFormDataContent rightFormContent = new MultipartFormDataContent();
            StreamContent rightFileStream = new StreamContent(File.OpenRead(filePathRight));
            rightFileStream.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            rightFormContent.Add(rightFileStream, name: "image", fileName: fileNameRight);
            rightFormContent.Add(new StringContent(input), "prompt");
            rightFormContent.Add(new StringContent("1"), "n");
            rightFormContent.Add(new StringContent("1024x1024"), "size");
            rightFormContent.Add(new StringContent("b64_json"), "response_format");
            rightFormContent.Add(new StringContent("adribona"), "user");

            using HttpRequestMessage rightRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.openai.com/v1/images/edits"),
                Content = rightFormContent
            };
            rightRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Key);

            using HttpClient leftHttpClient = new HttpClient();
            using HttpClient rightHttpClient = new HttpClient();
            Task<HttpResponseMessage> leftResponseTask = leftHttpClient.SendAsync(leftRequest);
            Task<HttpResponseMessage> rightResponseTask = rightHttpClient.SendAsync(rightRequest);
            Task speakTask = _speaker.SpeakAsync("Just adding a few more details.", cancellationToken);
            Task.WaitAll(speakTask, leftResponseTask, rightResponseTask);
            
            HttpResponseMessage leftResponse = await leftResponseTask;
            HttpResponseMessage rightResponse = await rightResponseTask;

            if (!leftResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"{leftResponse.StatusCode}: {leftResponse.ReasonPhrase}");
                string error = await leftResponse.Content.ReadAsStringAsync();
                return string.Empty;
            }

            OpenAiImageRequestResponse leftContent = await leftResponse.Content.ReadFromJsonAsync<OpenAiImageRequestResponse>(JsonSerializerOptions.Default, cancellationToken);
            _logger.LogDebug($"Saving left image to {filePathLeftFinal}");
            File.WriteAllBytes(filePathLeftFinal, Convert.FromBase64String(leftContent.data[0].b64_json));

            if (!rightResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"{rightResponse.StatusCode}: {rightResponse.ReasonPhrase}");
                string error = await rightResponse.Content.ReadAsStringAsync();
                return string.Empty;
            }

            OpenAiImageRequestResponse rightContent = await rightResponse.Content.ReadFromJsonAsync<OpenAiImageRequestResponse>(JsonSerializerOptions.Default, cancellationToken);
            _logger.LogDebug($"Saving right image to {fileNameRightFinal}");
            File.WriteAllBytes(filePathRightFinal, Convert.FromBase64String(rightContent.data[0].b64_json));


            //////////////
            ///
            using Image leftFinal = Image.Load(filePathLeftFinal);
            using Image rightFinal = Image.Load(filePathRightFinal);
            using Image final = Image.Load(filePathOriginal);

            final.Mutate(c => c.Pad(1820, 1024, Color.Transparent).DrawImage(leftFinal, new Point(0, 0), 1).DrawImage(rightFinal, new Point(1820 - 1024, 0), 1));
            _logger.LogDebug($"Saving final image to {fileNameRightFinal}");
            await final.SaveAsPngAsync(filePathFinal, pngEncoder);

            _speaker.SpeakAsync("Here you go!", cancellationToken);
            return filePathFinal;
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
