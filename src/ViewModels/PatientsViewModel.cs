﻿namespace SimpleDICOMToolkit.ViewModels
{
#if FellowOakDicom5
    using FellowOakDicom;
#else
    using Dicom;
#endif
    using Stylet;
    using StyletIoC;
    using System;
    using System.Text;
    using Helpers;
    using Infrastructure;
    using Models;
    using Server;
    using Services;

    public class PatientsViewModel : Screen, IHandle<DicomServiceEvent>, IHandle<IWorklistItem>, IDisposable
    {
        [Inject]
        private IWindowManager _windowManager;

        [Inject]
        private IConfigurationService configurationService;

        [Inject]
        private II18nService i18NService;

        [Inject]
        private IViewModelFactory viewModelFactory;

        [Inject]
        private INotificationService notificationService;

        [Inject]
        private IDataService dataService;

        private readonly IEventAggregator _eventAggregator;

        public BindableCollection<IWorklistItem> WorklistItems { get; private set; }

        private bool _isServerStarted = false;

        public bool IsServerStarted
        {
            get => _isServerStarted;
            set => SetAndNotify(ref _isServerStarted, value);
        }

        public PatientsViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this, nameof(PatientsViewModel));
        }

        public void UpdateData()
        {
            WorklistItems = new BindableCollection<IWorklistItem>();
            WorklistServer.Default.WorklistItems = WorklistItems;
            WorklistItems.AddRange(dataService.GetWorklistItems());
        }

        public void Handle(DicomServiceEvent message)
        {
            if (IsServerStarted)
            {
                Encoding fallbackEncoding = DicomEncoding.Default;  // 不要移除这行代码，.NET Core 平台会在这里注册 CodePagesEncodingProvider
                fallbackEncoding = Encoding.GetEncoding(configurationService.GetConfiguration<AppConfiguration>().DicomEncoding);

                WorklistServer.Default.CreateServer(message.ServerPort, message.LocalAET, fallbackEncoding);
                _eventAggregator.Publish(new ServerStateEvent(true), nameof(PatientsViewModel));
                notificationService.ShowNotification(
                    string.Format(i18NService.GetXmlStringByKey("ServerIsRunning"), "Worklist", SystemHelper.LocalIPAddress, message.ServerPort), 
                    message.LocalAET);
                WorklistServer.Default.IsListening();
            }
            else
            {
                WorklistServer.Default.StopServer();
                _eventAggregator.Publish(new ServerStateEvent(false), nameof(PatientsViewModel));
            }
        }

        public void ShowRegistrationWindow()
        {
            var register = viewModelFactory.GetRegistrationViewModel();

            _windowManager.ShowDialog(register, this);
        }

        public void Handle(IWorklistItem message)
        {
            WorklistItems.Add(message);
            dataService.AddWorklistItem(message);
        }

        public void RemoveItem(IWorklistItem item)
        {
            WorklistItems.Remove(item);
            dataService.RemoveWorklistItem(item);
        }

        public void ViewDetails(IWorklistItem item)
        {
            var register = viewModelFactory.GetRegistrationViewModel();

            register.CanEdit = false;
            register.HospitalName = item.HospitalName;
            register.ExamRoom = item.ExamRoom;
            register.ReferringPhysicianName = item.ReferringPhysician;
            register.PatientName = item.PatientName;
            register.PatientID = item.PatientID;
            register.AccessionNumber = item.AccessionNumber;
            register.Sex = item.Sex;
            register.Age = item.Age;
            register.BirthDate = item.DateOfBirth.ToString("yyyyMMdd");
            register.Modality = item.Modality;
            register.ScheduledAET = item.ScheduledAET;
            register.ScheduledDate = item.ExamDateAndTime.ToString("yyyyMMdd");
            register.PerformingPhysicianName = item.PerformingPhysician;
            register.Description = item.ExamDescription;

            _windowManager.ShowDialog(register, this);
        }

        public void Dispose()
        {
            _eventAggregator.Unsubscribe(this);

            WorklistServer.Default.StopServer();
        }
    }
}
