using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Timers;
using DynamicData.Binding;
using ReactiveUI;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;
using VkNet.Model.RequestParams;
using VKTalker.Models;

namespace VKTalker.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private const int chatCount = 20;
        private const int messageCount = 100;
        private bool _isEnabled = false;
        private string _chatName, _messageText;
        private DialogModel _selectedModel;
        private object lockDialog = new object();
        private object lockMessage = new object();
        private ConfigModel _configModel;

        public ObservableCollectionExtended<DialogModel> DialogModels { get; } =
            new ObservableCollectionExtended<DialogModel>();

        public ObservableCollectionExtended<MessageModel> MessageModels { get; } =
            new ObservableCollectionExtended<MessageModel>();

        private VkApi api = new VkApi();
        private ReactiveCommand<Unit, Unit> authCommand { get; }
        private ReactiveCommand<Unit, Unit> dialogsDataCommand { get; }
        private ReactiveCommand<Unit, Unit> messagesDataCommand { get; }
        public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }

        private long? ChatId { get; set; }

        public MainWindowViewModel(ConfigModel model)
        {
            _configModel = model;
            authCommand = ReactiveCommand.CreateFromTask(Auth);
            SendMessageCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!string.IsNullOrEmpty(MessageText) && ChatId is not null)
                    await api.Messages.SendAsync(new MessagesSendParams
                    {
                        PeerId = ChatId,
                        Message = MessageText.Trim(),
                        RandomId = 0
                    });
                MessageText = string.Empty;
            });
            dialogsDataCommand = ReactiveCommand.CreateFromTask(SetStartupDialog);
            messagesDataCommand = ReactiveCommand.CreateFromTask(GetMessages);
            authCommand.ThrownExceptions
                .Merge(SendMessageCommand.ThrownExceptions)
                .Merge(dialogsDataCommand.ThrownExceptions)
                .Merge(messagesDataCommand.ThrownExceptions)
                .Subscribe(e => Console.WriteLine(e.Message));
            authCommand.Execute().Subscribe();
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                Observable.Timer(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500))
                    .Select(time => Unit.Default)
                    .InvokeCommand(this, x => x.messagesDataCommand);
                Observable.Timer(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000))
                    .Select(time => Unit.Default)
                    .InvokeCommand(this, x => x.dialogsDataCommand);
                this.RaiseAndSetIfChanged(ref _isEnabled, value);
            }
        }

        public DialogModel SelectedModel
        {
            get => _selectedModel;
            set
            {
                ChatId = value?.ChatId;
                ChatName = value?.Name;
                this.RaiseAndSetIfChanged(ref _selectedModel, value);
            }
        }

        public string MessageText
        {
            get => _messageText;
            set => this.RaiseAndSetIfChanged(ref _messageText, value);
        }

        public string ChatName
        {
            get => _chatName;
            set => this.RaiseAndSetIfChanged(ref _chatName, value);
        }

        private async Task Auth()
        {
           
            await api.AuthorizeAsync(new ApiAuthParams
            {
                Login = _configModel.Login,
                Password = _configModel.Password,
                ApplicationId = _configModel.AppId,
                Settings = Settings.All
            });
            if (api.UserId != default(long))
            {
                IsEnabled = true;
                await SetStartupDialog();
            }
        }

        private async Task SetStartupDialog()
        {
            
            var dialogs = await api.Messages.GetConversationsAsync(new GetConversationsParams
            {
                Filter = GetConversationFilter.All,
                Count = chatCount,
                Offset = 0,
                Extended = true
            });
            var models = dialogs.Items.Select(dialog => new DialogModel
            {
                Message = GetText(dialog.LastMessage),
                Name = GetName(dialog, dialogs),
                ChatId = dialog.Conversation.Peer.Id
            }).ToList();

            if (SelectedModel is not null)
            {
                var newModel = models.FirstOrDefault(m => m.ChatId == SelectedModel.ChatId);
                if (newModel is not null)
                {
                    var id = models.IndexOf(newModel);
                    models[id] = SelectedModel;
                }
            }

            lock (lockDialog)
            {
                if (DialogModels.Count > 0 && DialogModels.Count == models.Count)
                {
                    var i = 0;
                    foreach (var model in DialogModels)
                    {
                        model.Name = models[i].Name;
                        model.ChatId = models[i].ChatId;
                        model.Message = models[i].Message;
                        i++;
                    }
                }
                else
                {
                    DialogModels.AddRange(models);
                }
            }
        }

        private async Task GetMessages()
        {
            if (ChatId is null)
                return;

            var history = await api.Messages.GetHistoryAsync(new MessagesGetHistoryParams
            {
                Count = messageCount,
                Extended = true,
                PeerId = ChatId,
            });
            var messages = history.Messages.Select(m => new MessageModel
            {
                Name = GetName(m, history.Users),
                Message = m?.Text ?? m?.Body,
                ChatId = m.Id,
                Date = m.Date?.ToString()
            }).Reverse().ToList();
            lock (lockMessage)
            {
                if (MessageModels.LastOrDefault()?.ChatId != messages.LastOrDefault()?.ChatId)
                {
                    MessageModels.Clear();
                    MessageModels.AddRange(messages);
                }
            }
        }

        private string GetName(Message message, IEnumerable<User> getConversationsResult)
        {
            var user = getConversationsResult.FirstOrDefault(u => u.Id == message.FromId);
            return user?.FirstName ?? string.Empty;
        }

        private string GetName(ConversationAndLastMessage dialog, GetConversationsResult getConversationsResult)
        {
            var title = dialog.Conversation.ChatSettings?.Title;
            if (!string.IsNullOrEmpty(title))
                return title;
            var user = getConversationsResult.Profiles.FirstOrDefault(u => u.Id == GetPartnerId(dialog.LastMessage));
            return user?.FirstName ?? string.Empty;
        }

        private long? GetPartnerId(Message dialogLastMessage)
        {
            return dialogLastMessage.FromId == api.UserId ? dialogLastMessage.PeerId : dialogLastMessage.FromId;
        }

        private string GetText(Message dialogLastMessage)
        {
            if (dialogLastMessage?.Text == null) return string.Empty;
            return dialogLastMessage.Text.Length > 10
                ? dialogLastMessage.Text.Substring(0, 10) + "..."
                : dialogLastMessage.Text;
        }
    }
}