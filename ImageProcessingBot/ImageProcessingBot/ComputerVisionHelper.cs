using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;


namespace ImageProcessingBot
{
    public class ComputerVisionHelper
    {
        private readonly IConfiguration _configuration;

        private ComputerVisionClient _client;

        private static readonly List<VisualFeatureTypes> features =
            new List<VisualFeatureTypes>()
        {
            VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
            VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
            VisualFeatureTypes.Tags
        };

        public ComputerVisionHelper(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _client = new ComputerVisionClient(
                new ApiKeyServiceClientCredentials(_configuration["computerVisionKey"].ToString()), new System.Net.Http.DelegatingHandler[] {}
            );
            _client.Endpoint = _configuration["computerVisionEndpoint"].ToString();
        }

        public async Task<ImageAnalysis> AnalyzeImageAsync(Stream image)
        {
            ImageAnalysis analysis = await _client.AnalyzeImageInStreamAsync(image, features);
            return analysis;

        }

        public async Task<string> GenerateThumbnailAsync(Stream image)
        {
            Stream thumbnail = await _client.GenerateThumbnailInStreamAsync(100, 100, image, smartCropping: true);
            
            byte[] thumbnailArray;
            using (var ms = new MemoryStream())
            {
                thumbnail.CopyTo(ms);
                thumbnailArray = ms.ToArray();
            }

            return System.Convert.ToBase64String(thumbnailArray);
        }

        public async Task<IList<Line>> ExtractTextAsync(Stream image, TextRecognitionMode recognitionMode)
        {
            RecognizeTextInStreamHeaders headers = await _client.RecognizeTextInStreamAsync(image, recognitionMode);
            IList<Line> detectedLines = await GetTextAsync(_client, headers.OperationLocation);
            return detectedLines;

        }

        private  async Task<IList<Line>> GetTextAsync(ComputerVisionClient client,string operationLocation)
        {
            _client = client;
            
            string operationId = operationLocation.Substring(operationLocation.Length - 36);

            TextOperationResult result = await _client.GetTextOperationResultAsync(operationId);

            // Wait for the operation to complete
            int i = 0;
            int maxRetries = 5;
            while ((result.Status == TextOperationStatusCodes.Running ||
                    result.Status == TextOperationStatusCodes.NotStarted) && i++ < maxRetries)
            {
                await Task.Delay(1000);
                result = await _client.GetTextOperationResultAsync(operationId);
            }
            var lines = result.RecognitionResult.Lines;
            return lines;
        }
            
    }
}