﻿namespace SimpleDICOMToolkit.Models
{
    public class DicomRequestEvent
    {
        public string ServerIP { get; private set; }

        public int ServerPort { get; private set; }

        public string ServerAET { get; private set; }

        public string LocalAET { get; private set; }

        public string Modality { get; private set; }

        public DicomRequestEvent(
            string ip,
            int port,
            string serverAET,
            string localAET,
            string modality = null)
        {
            ServerIP = ip;
            ServerPort = port;
            ServerAET = serverAET;
            LocalAET = localAET;
            Modality = modality;
        }
    }
}
