#region References
using System;
using System.Collections.Generic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
#endregion

namespace ImageProcessingBot
{
    public class ImageProcessingBotAccessors
    {
        public ImageProcessingBotAccessors(ConversationState conversationState, UserState userState)
        {
            ConversationState = conversationState ?? throw new ArgumentNullException(nameof(ConversationState));
            UserState = userState ?? throw new ArgumentNullException(nameof(UserState));
        }

        public static readonly string CommandStateName = $"{nameof(ImageProcessingBotAccessors)}.CommandState";

        public static readonly string DialogStateName = $"{nameof(ImageProcessingBotAccessors)}.DialogState";

        public IStatePropertyAccessor<string> CommandState { get; set; }

        public IStatePropertyAccessor<DialogState> ConversationDialogState { get; set; }
        public ConversationState ConversationState { get; }

        public UserState UserState { get; }
    }
}