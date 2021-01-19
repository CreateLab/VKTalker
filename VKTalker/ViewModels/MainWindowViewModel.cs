using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Timers;
using DynamicData;
using DynamicData.Binding;
using Flurl.Http;
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
        private const int chatCount = 5;
        private const int messageCount = 100;
        public const string PhotoFolder = "Photos";
        private bool _isEnabled = false;
        private string _chatName, _messageText;
        private DialogModel _selectedModel;
        private ConfigModel _configModel;
        private ConcurrentQueue<(string,string)> _photosQueue = new ConcurrentQueue<(string,string)>();

        /*public ObservableCollectionExtended<DialogModel> DialogModel { get; } =
            new ObservableCollectionExtended<DialogModel>();*/
        private SourceList<DialogModel> dialogList = new SourceList<DialogModel>();
        private readonly ReadOnlyObservableCollection<DialogModel> _dialogModels;
        public ReadOnlyObservableCollection<DialogModel> DialogModels => _dialogModels;

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
            dialogList.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _dialogModels)
                .Subscribe();
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
            this.WhenAnyValue(x => x.SelectedModel)
                .Subscribe(value =>
                {
                    ChatId = value?.ChatId;
                    ChatName = value?.Name;
                });
            Task.Run(async () =>
            {
                while (true)
                {
                    await PhotoLoad();
                }
            });
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
            set => this.RaiseAndSetIfChanged(ref _selectedModel, value);
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
                Extended = true,
            });
            var models = dialogs.Items.Select(dialog => new DialogModel
            {
                Message = GetText(dialog.LastMessage),
                Name = GetName(dialog, dialogs),
                ChatId = dialog.Conversation.Peer.Id,
                Image = AddPhoto(dialog.Conversation.ChatSettings?.Photo?.Photo50?.AbsoluteUri ??
                                 GetUserPhoto(dialog, dialogs),GetUserId(dialog,dialogs))
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


            if (dialogList.Count > 0 && dialogList.Count == models.Count)
            {
                var i = 0;
                foreach (var model in dialogList.Items)
                {
                    model.Name = models[i].Name;
                    model.ChatId = models[i].ChatId;
                    model.Message = models[i].Message;
                    model.Image = models[i].Image;
                    i++;
                }
            }
            else
            {
                dialogList.Clear();
                dialogList.AddRange(models);
            }
        }

        private string GetUserPhoto(ConversationAndLastMessage dialogLastMessage,
            GetConversationsResult getConversationsResult)
        {
            var user = getConversationsResult.Profiles.FirstOrDefault(u =>
                u.Id == GetPartnerId(dialogLastMessage.LastMessage));
            if (user == null)
            {
              var group =  getConversationsResult.Groups.FirstOrDefault(u =>
                  u.Id == GetPartnerId(dialogLastMessage.LastMessage));
              var photourl = group?.Photo50?.AbsoluteUri;
              return photourl;
            }
            return user?.Photo50?.AbsoluteUri;
        }
        private string GetUserId(ConversationAndLastMessage dialogLastMessage,
        GetConversationsResult getConversationsResult)
        {
            var user = getConversationsResult.Profiles.FirstOrDefault(u =>
                u.Id == GetPartnerId(dialogLastMessage.LastMessage));
            if (user == null)
            {
                var group = getConversationsResult.Groups.FirstOrDefault(u =>
                    u.Id == GetPartnerId(dialogLastMessage.LastMessage));
                return group?.Id.ToString();   
            }
            return user?.Id.ToString();
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
                Name = GetName(m, history),
                Image = AddPhoto(GetPhotoByName(m, history),GetUserId(m,history)),
                Message = m?.Text ?? m?.Body,
                ChatId = m.Id,
                Date = m.Date?.ToString()
            }).Reverse().ToList();

            if (MessageModels.LastOrDefault()?.ChatId != messages.LastOrDefault()?.ChatId)
            {
                MessageModels.Clear();
                MessageModels.AddRange(messages);
            }
        }

      
        private string GetPhotoByName(Message message, MessageGetHistoryObject getConversationsResult)
        {
            var user = getConversationsResult?.Users.FirstOrDefault(u => u.Id == message.FromId);
            if (user is null)
            {
                var g = getConversationsResult?.Groups.FirstOrDefault(u => u.Id == -1*message.FromId);
                return g?.Photo50?.AbsoluteUri;
            }
            return user?.Photo50?.AbsoluteUri;
        }
        private string GetUserId(Message message, MessageGetHistoryObject getConversationsResult)
        {
            var user = getConversationsResult.Users.FirstOrDefault(u => u.Id == message.FromId);
            if (user is null)
            {
                var g = getConversationsResult?.Groups.FirstOrDefault(u => u.Id == -1*message.FromId);
                return g?.Id.ToString();
            }
            return user?.Id.ToString();
        }

        private string GetName(Message message, MessageGetHistoryObject getConversationsResult)
        {
            var user = getConversationsResult.Users.FirstOrDefault(u => u.Id == message.FromId);
            if (user is null)
            {
                var g = getConversationsResult?.Groups.FirstOrDefault(u => u.Id == -1*message.FromId);
                return g?.Name ?? string.Empty;
            }
            return user?.FirstName ?? string.Empty;
        }

        private string GetName(ConversationAndLastMessage dialog, GetConversationsResult getConversationsResult)
        {
            var title = dialog.Conversation.ChatSettings?.Title;
            if (!string.IsNullOrEmpty(title))
                return title;
            var user = getConversationsResult.Profiles.FirstOrDefault(u => u.Id == GetPartnerId(dialog.LastMessage));
            if (user == null)
            {
                var g =getConversationsResult.Groups.FirstOrDefault(u => u.Id ==  GetPartnerId(dialog.LastMessage));
                return g?.Name ?? string.Empty;
            }
            return user?.FirstName ?? string.Empty;
        }

        private long? GetPartnerId(Message dialogLastMessage)
        {
            var id = dialogLastMessage.FromId == api.UserId ? dialogLastMessage.PeerId : dialogLastMessage.FromId;
            return id < 0 ? -1 * id : id;
        }

        private string GetText(Message dialogLastMessage)
        {
            if (dialogLastMessage?.Text == null) return string.Empty;
            return dialogLastMessage.Text.Length > 10
                ? dialogLastMessage.Text.Substring(0, 10) + "..."
                : dialogLastMessage.Text;
        }

        private string AddPhoto(string url, string id)
        {
            if (url is null) return null;
            _photosQueue.Enqueue((url,id));
            return id + ".jpg";
        }

        private async Task PhotoLoad()
        {
            if (!_photosQueue.TryDequeue(out var data)) return;
            var name = data.Item2 + ".jpg";
            if (!File.Exists(Path.Combine(PhotoFolder, name)))
            {
                await data.Item1.DownloadFileAsync(PhotoFolder, name);
            }
        }
    }
}