﻿namespace SimpleDICOMToolkit.ViewModels
{
    using Stylet;
    using StyletIoC;
    using System;
    using Client;
    using Logging;
    using Models;
    using Services;
    using Newtonsoft.Json;
    using System.Configuration;
    using System.IO;

    public class ServerConfigViewModel : Screen, IHandle<BusyStateEvent>, IHandle<ServerStateEvent>, IDisposable

    {

        private readonly IEventAggregator _eventAggregator;

        [Inject(Key = "filelogger")]
        private ILoggerService _logger;

        [JsonIgnore]
        public Action RequestAction { get; set; }


        [Inject]
        private II18nService i18NService;

        [Inject]
        private INotificationService notificationService;

        [Inject]
        private IVerifySCU _verifySCU;

        private string _serverIP = "localhost";
        private string _serverPort = "104";
        private string _serverAET = "P5001";
        private string _localAET = "ANY-SCU";
        private string _modality = "US";

        private bool _isServerIPEnabled = true;
        private bool _isServerPortEnabled = true;
        private bool _isServerAETEnabled = true;
        private bool _isLocalAETEnabled = true;
        private bool _isModalityEnabled = true;

        private (bool isServerIPEnabled, bool isServerPortEnabled, bool isServerAETEnabled, bool isLocalAETEnabled, bool isModalityEnabled) _backupStatus;

        public class Configuracion
        {
            public string ServerIP { get; set; }
            public string ServerPort { get; set; }
            public string ServerAET { get; set; }
        }

        private Configuracion LoadConfig()
        {
            string filePath = "configuracion.json";
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Configuracion>(json);
            }

            return new Configuracion();
        }

        private void SaveConfig(Configuracion configuracion)
        {
            string filePath = "configuracion.json";
            string json = JsonConvert.SerializeObject(configuracion, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public string ServerIP
        {
            get => _serverIP;
            set
            {
                SetAndNotify(ref _serverIP, value);
                NotifyOfPropertyChange(() => CanDoEcho);
                NotifyOfPropertyChange(() => CanDoRequest);
                
            }
        }

        public string ServerPort
        {
            get => _serverPort;
            set
            {
                SetAndNotify(ref _serverPort, value);
                NotifyOfPropertyChange(() => CanDoEcho);
                NotifyOfPropertyChange(() => CanDoRequest);
            }
        }

        public string ServerAET
        {
            get => _serverAET;
            set
            {
                SetAndNotify(ref _serverAET, value);
                NotifyOfPropertyChange(() => CanDoEcho);
                NotifyOfPropertyChange(() => CanDoRequest);
            }
        }

        public string LocalAET
        {
            get => _localAET;
            set
            {
                SetAndNotify(ref _localAET, value);
                NotifyOfPropertyChange(() => CanDoEcho);
                NotifyOfPropertyChange(() => CanDoRequest);
            }
        }

        public string Modality
        {
            get => _modality;
            set => SetAndNotify(ref _modality, value);
        }

        public bool IsServerIPEnabled
        {
            get => _isServerIPEnabled;
            set => SetAndNotify(ref _isServerIPEnabled, value);
        }

        public bool IsServerPortEnabled
        {
            get => _isServerPortEnabled;
            set => SetAndNotify(ref _isServerPortEnabled, value);
        }

        public bool IsServerAETEnabled
        {
            get => _isServerAETEnabled;
            set => SetAndNotify(ref _isServerAETEnabled, value);
        }

        public bool IsLocalAETEnabled
        {
            get => _isLocalAETEnabled;
            set => SetAndNotify(ref _isLocalAETEnabled, value);
        }

        public bool IsModalityEnabled
        {
            get => _isModalityEnabled;
            set => SetAndNotify(ref _isModalityEnabled, value);
        }

        private bool _isBusy = false;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetAndNotify(ref _isBusy, value))
                {
                    NotifyOfPropertyChange(() => CanDoEcho);
                    NotifyOfPropertyChange(() => CanDoRequest);
                }
            }
        }

        private int _busyIndicatorColumn = 0;

        public int BusyIndicatorColumn
        {
            get => _busyIndicatorColumn;
            private set => SetAndNotify(ref _busyIndicatorColumn, value);
        }

        public ServerConfigViewModel(IEventAggregator eventAggregator, IModelValidator<ServerConfigViewModel> validator) : base(validator)
        {
            _eventAggregator = eventAggregator;

            Configuracion configuracion = LoadConfig();
            ServerIP = configuracion.ServerIP;
            ServerPort = configuracion.ServerPort;
            ServerAET = configuracion.ServerAET;
        }

        public bool CanDoRequest =>
            !string.IsNullOrEmpty(ServerIP) &&
            !string.IsNullOrEmpty(ServerPort) &&
            !string.IsNullOrEmpty(ServerAET) &&
            !string.IsNullOrEmpty(LocalAET) &&
            !IsBusy;

        public void DoRequest()
        {
            RequestAction?.Invoke();
        }

        public bool CanDoEcho =>
            !string.IsNullOrEmpty(ServerIP) &&
            !string.IsNullOrEmpty(ServerPort) &&
            !string.IsNullOrEmpty(ServerAET) &&
            !string.IsNullOrEmpty(LocalAET) &&
            !IsBusy;

        public async void DoEcho()
        {
            int port = ParseServerPort();
            if (port == 0)
                return;

            _backupStatus = (IsServerIPEnabled, IsServerPortEnabled, IsServerAETEnabled, IsLocalAETEnabled, IsModalityEnabled);
            IsServerIPEnabled = IsServerPortEnabled = IsServerAETEnabled = IsLocalAETEnabled = IsModalityEnabled = false;

            BusyIndicatorColumn = 0;

            IsBusy = true;

            bool result = await _verifySCU.VerifyAsync(_serverIP, port, _serverAET, _localAET);

            IsBusy = false;

            (IsServerIPEnabled, IsServerPortEnabled, IsServerAETEnabled, IsLocalAETEnabled, IsModalityEnabled) = _backupStatus;

            if (result)
            {
                await notificationService.ShowToastAsync(i18NService.GetXmlStringByKey("VerifySuccess"), new TimeSpan(0, 0, 3), Controls.ToastType.Info);
            }
            else
            {
                await notificationService.ShowToastAsync(i18NService.GetXmlStringByKey("VerifyFailed"), new TimeSpan(0, 0, 3), Controls.ToastType.Error);
            }
        }

        public void PublishClientRequest(string channel)
        {
            int port = ParseServerPort();
            if (port == 0)
                return;

            _eventAggregator.Publish(new DicomRequestEvent(_serverIP, port, _serverAET, _localAET, _modality), channel);
        }

        public void PublishServerRequest(string channel)
        {
            int port = ParseServerPort();
            if (port == 0)
                return;

            _eventAggregator.Publish(new DicomServiceEvent(port, _localAET), channel);
        }

        public int ParseServerPort()
        {
            if (!int.TryParse(_serverPort, out int port))
            {
                _logger.Warn("Puerto ilegal：{0}，El puerto debe ser un número。", _serverPort);
                ServerPort = "";
            }

            return port;
        }

        public void Handle(BusyStateEvent message)
        {
            BusyIndicatorColumn = 1;

            IsBusy = message.IsBusy;

            if (message.IsBusy)
            {
                _backupStatus = (IsServerIPEnabled, IsServerPortEnabled, IsServerAETEnabled, IsLocalAETEnabled, IsModalityEnabled);
                IsServerIPEnabled = IsServerPortEnabled = IsServerAETEnabled = IsLocalAETEnabled = IsModalityEnabled = false;
            }
            else
            {
                (IsServerIPEnabled, IsServerPortEnabled, IsServerAETEnabled, IsLocalAETEnabled, IsModalityEnabled) = _backupStatus;
            }
        }

        public void Handle(ServerStateEvent message)
        {
            if (message.IsRuning)
            {
                _backupStatus = (IsServerIPEnabled, IsServerPortEnabled, IsServerAETEnabled, IsLocalAETEnabled, IsModalityEnabled);
                IsServerIPEnabled = IsServerPortEnabled = IsServerAETEnabled = IsLocalAETEnabled = IsModalityEnabled = false;
            }
            else
            {
                (IsServerIPEnabled, IsServerPortEnabled, IsServerAETEnabled, IsLocalAETEnabled, IsModalityEnabled) = _backupStatus;
            }
        }

        public void Dispose()
        {
            // Guardar la configuración antes de cerrar el ViewModel
            Configuracion configuracion = new Configuracion
            {
                ServerIP = ServerIP,
                ServerPort = ServerPort,
                ServerAET = ServerAET
            };

            SaveConfig(configuracion);

            _eventAggregator.Unsubscribe(this);
        }
    }
}
