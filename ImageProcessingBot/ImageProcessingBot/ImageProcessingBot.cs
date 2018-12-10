#region  References
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.IO;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
#endregion

namespace ImageProcessingBot
{
    public class ImageProcessingBot : IBot
    {

        private readonly ImageProcessingBotAccessors _accessors;

        private readonly IConfiguration _configuration;

        private readonly DialogSet _dialogs;

        public ImageProcessingBot(ImageProcessingBotAccessors accessors, IConfiguration configuration)
        {
            _accessors = accessors ?? throw new ArgumentNullException(nameof(accessors));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dialogs = new DialogSet(_accessors.ConversationDialogState);
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            Activity reply = null;
            HeroCard card ;
            StringBuilder sb;

            switch(turnContext.Activity.Type)
            {
                case ActivityTypes.ConversationUpdate:
                    foreach(var member in turnContext.Activity.MembersAdded)
                    {
                        if(member.Id != turnContext.Activity.Recipient.Id)
                        {
                            
                            reply = await CreateReplyAsync(turnContext, "Welcome. Please select and operation");
                            await turnContext.SendActivityAsync(reply, cancellationToken:cancellationToken);
                        }
                    }
                    break;
                
                case ActivityTypes.Message:
                    
                    int attachmentCount =  turnContext.Activity.Attachments != null ?  turnContext.Activity.Attachments.Count() : 0;

                    var command =  !string.IsNullOrEmpty(turnContext.Activity.Text) ? turnContext.Activity.Text : await _accessors.CommandState.GetAsync(turnContext, () => string.Empty, cancellationToken);
                    command = command.ToLowerInvariant();

                    if(attachmentCount == 0)
                    {
                        if(string.IsNullOrEmpty(command))
                        {
                            
                            reply = await CreateReplyAsync(turnContext, "Please select operation before uploading the image");
                            await turnContext.SendActivityAsync(reply, cancellationToken:cancellationToken);

                        }
                        else
                        {
                            await _accessors.CommandState.SetAsync(turnContext, turnContext.Activity.Text, cancellationToken);
                            await _accessors.UserState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
                            await turnContext.SendActivityAsync("Please upload the image using upload button", cancellationToken: cancellationToken);
                        }

                    }
                    else
                    {
                        

                        HttpClient client = new HttpClient();
                        Attachment attachment = turnContext.Activity.Attachments[0];

                        if(attachment.ContentType == "image/jpeg" || attachment.ContentType == "image/png")
                        {
                            Stream image = await client.GetStreamAsync(attachment.ContentUrl);
                            if(image != null)
                            {
                                ComputerVisionHelper helper = new ComputerVisionHelper(_configuration);
                                IList<Line> detectedLines;
                                switch(command)
                                {
                                    case "processimage":
                                        
                                        ImageAnalysis analysis = await helper.AnalyzeImageAsync(image);
                                        await turnContext.SendActivityAsync($"I think the Image you uploaded is a {analysis.Tags[0].Name.ToUpperInvariant()} and it is {analysis.Description.Captions[0].Text.ToUpperInvariant()} ", cancellationToken: cancellationToken);
                                        break;
                                    
                                    case "getthumbnail":
                                        string thumbnail = await helper.GenerateThumbnailAsync(image);
                                        reply = turnContext.Activity.CreateReply();
                                        reply.Text = "Here is your thumbnail.";
                                        reply.Attachments = new List<Attachment>()
                                        {
                                            new Attachment()
                                            {
                                                ContentType = "image/jpeg",
                                                Name="thumbnail.jpg",
                                                ContentUrl = string.Format("data:image/jpeg;base64,{0}", thumbnail)
                                            }

                                        };
                                        await turnContext.SendActivityAsync(reply, cancellationToken: cancellationToken);
                                        break;
                                    
                                    case "printedtext":
                                        detectedLines = await helper.ExtractTextAsync(image, TextRecognitionMode.Printed);
                                        sb = new StringBuilder("I was able to extract following text. \n");
                                        foreach(Line line in detectedLines)
                                        {
                                            sb.AppendFormat("{0}.\n", line.Text);
                                        }
                                        await turnContext.SendActivityAsync(sb.ToString(), cancellationToken: cancellationToken);
                                        
                                        break;
                                    case "handwrittentext":
                                        detectedLines = await helper.ExtractTextAsync(image, TextRecognitionMode.Printed);
                                        sb = new StringBuilder("I was able to extract following text. \n");
                                        foreach(Line line in detectedLines)
                                        {
                                            sb.AppendFormat("{0}.\n", line.Text);
                                        }
                                        await turnContext.SendActivityAsync(sb.ToString(), cancellationToken: cancellationToken);
                                        
                                        break;

                                }
                                
                                await _accessors.CommandState.DeleteAsync(turnContext, cancellationToken: cancellationToken);
                                await _accessors.UserState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);

                                reply = await CreateReplyAsync(turnContext, "Please select an operation and Upload the image");
                                await turnContext.SendActivityAsync(reply, cancellationToken:cancellationToken);

                                //Clear out the command as the task for this command is finished.

                            }
                            else
                            {
                                reply = await CreateReplyAsync(turnContext, "Incorrect Image. /n Please select an operation and Upload the image");
                                await turnContext.SendActivityAsync(reply, cancellationToken:cancellationToken);
                            }
                        }
                        else
                        {
                            reply = await CreateReplyAsync(turnContext, "Only Image Attachments(.jpeg or .png) are supported. /n Please select an operation and Upload the image");
                            await turnContext.SendActivityAsync(reply, cancellationToken:cancellationToken);

                        }

                    }


                    break;

            }


            
        }

        public async Task<Activity> CreateReplyAsync(ITurnContext context, string message)
        {
            var reply = context.Activity.CreateReply();
            var card = new HeroCard()
            {
                Text = message,
                Buttons = new List<CardAction>()
                {
                    new CardAction {Text = "Process Image", Value = "ProcessImage", Title = "ProcessImage", DisplayText = "Process Image", Type = ActionTypes.ImBack},
                    new CardAction {Text = "Get Thumbnail", Value = "GetThumbnail", Title = "GetThumbnail", DisplayText = "Get Thumbnail", Type = ActionTypes.ImBack},
                    new CardAction {Text = "Extract Printed Text", Value = "printedtext", Title = "Extract Printed Text", DisplayText = "Extract Printed Text", Type = ActionTypes.ImBack},
                    new CardAction {Text = "Extract Hand Written Text", Value = "handwrittentext", Title = "Extract Hand Written Text", DisplayText = "Extract Hand Written Text", Type = ActionTypes.ImBack}
                    
                }
            };
            reply.Attachments = new List<Attachment>(){card.ToAttachment()};

            return reply;

            
        }

        

        

    }
}