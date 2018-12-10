using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace ImageProcessingBot
{
    public class Startup
    {
        private ILoggerFactory _loggerFactory;

        private bool _isProduction = false;

        public IConfiguration Configuration { get; }

        public Startup(IHostingEnvironment env)
        {
            _isProduction = env.IsProduction();

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional:false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional:true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public void  ConfigureServices(IServiceCollection services)
        {
            services.AddBot<ImageProcessingBot>(options =>
            {
                var secretKey = Configuration.GetSection("botFileSecret")?.Value;
                var botFilePath = Configuration.GetSection("botFilePath")?.Value;

                // Get the Boty Config file and add it as a singleton
                var botConfig = BotConfiguration.Load(botFilePath ?? @".\ImageProcessingBot.bot", secretKey);

                services.AddSingleton(singleton => botConfig ?? throw new InvalidOperationException($"The .bot config file could not be loaded. ({botConfig})"));
                
                //Set up Bot End point 

                var environment = _isProduction ? "production" : "development";
                var service = botConfig.Services.Where(x => x.Type == ServiceTypes.Endpoint && x.Name == environment).FirstOrDefault();

                if(!(service is EndpointService endpointService))
                {
                    throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
                }
                options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

                //Create a Logger

                ILogger logger = _loggerFactory.CreateLogger<ImageProcessingBot>();

                options.OnTurnError = async (context, exception) =>
                {
                    logger.LogError($"Exception caught : {exception}");

                    await context.SendActivityAsync("broken bot");
                };

                IStorage storage = new MemoryStorage();
                
                var conversationState = new ConversationState(storage);
                options.State.Add(conversationState);

                var userState = new UserState(storage);
                options.State.Add(userState);

            });

            services.AddSingleton<ImageProcessingBotAccessors>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
                if (options == null)
                {
                    throw new InvalidOperationException("BotFrameworkOptions must be configured prior to setting up the state accessors");
                }

                var conversationState = options.State.OfType<ConversationState>().FirstOrDefault();
                if (conversationState == null)
                {
                    throw new InvalidOperationException("ConversationState must be defined and added before adding conversation-scoped state accessors.");
                }

                var userState = options.State.OfType<UserState>().FirstOrDefault();

                if (userState == null)
                {
                    throw new InvalidOperationException("User State mjust be defined and added befor the conversation scoping");
                }

                // Create the custom state accessor.
                // State accessors enable other components to read and write individual properties of state.
                var accessors = new ImageProcessingBotAccessors(conversationState, userState)
                {
                    ConversationDialogState = userState.CreateProperty<DialogState>(ImageProcessingBotAccessors.DialogStateName),
                    CommandState = userState.CreateProperty<string>(ImageProcessingBotAccessors.CommandStateName)
                    
                    
                };

                return accessors;
            });

            services.AddSingleton<IConfiguration>(Configuration);
        }

        public void Configure(IApplicationBuilder application, IHostingEnvironment environment, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;

            application.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
