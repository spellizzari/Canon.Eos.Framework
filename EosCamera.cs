﻿using System;
using System.IO;
using System.Runtime.InteropServices;

using EDSDKLib;

namespace Canon.Eos.Framework
{
    public sealed partial class EosCamera : EosDisposable
    {
        private IntPtr _camera;
        private EDSDK.EdsDeviceInfo _deviceInfo;
        private bool _sessionOpened;
        private string _picturePath;
        private EDSDK.EdsObjectEventHandler _edsObjectEventHandler;
        private EDSDK.EdsPropertyEventHandler _edsPropertyEventHandler;
        private EDSDK.EdsStateEventHandler _edsStateEventHandler;

        public event EventHandler Shutdown;

        internal EosCamera(IntPtr camera)
        {
            _camera = camera;

            EosAssert.NotOk(EDSDK.EdsGetDeviceInfo(_camera, out _deviceInfo), "Failed to get device info.");                        
            this.SubscribeEvents();
            this.EnsureOpenSession();
        }

        private void SubscribeEvents()
        {   
            _edsStateEventHandler = this.HandleStateEvent;
            EosAssert.NotOk(EDSDK.EdsSetCameraStateEventHandler(_camera, EDSDK.StateEvent_All, _edsStateEventHandler, IntPtr.Zero), "Failed to set state handler.");                     

            _edsObjectEventHandler = this.HandleObjectEvent;            
            EosAssert.NotOk(EDSDK.EdsSetObjectEventHandler(_camera, EDSDK.ObjectEvent_All, _edsObjectEventHandler, IntPtr.Zero), "Failed to set object handler.");

            _edsPropertyEventHandler = this.HandlePropertyEvent;
            EosAssert.NotOk(EDSDK.EdsSetPropertyEventHandler(_camera, EDSDK.PropertyEvent_All, _edsPropertyEventHandler, IntPtr.Zero), "Failed to set object handler.");            
        }

        public string Artist
        {
            get { return this.GetPropertyStringData(EDSDK.PropID_Artist, 0); }
        }
        
        public string CopyRight
        {
            get { return this.GetPropertyStringData(EDSDK.PropID_Copyright, 0); }
        }
        
        public string DeviceDescription
        {
            get { return _deviceInfo.szDeviceDescription; }
        }

        public bool IsLegacy
        {
            get { return _deviceInfo.DeviceSubType == 0; }
        }               
                
        public string OwnerName
        {
            get { return this.GetPropertyStringData(EDSDK.PropID_OwnerName, 0); }
        }

        public string PortName
        {
            get { return _deviceInfo.szPortName; }
        }

        public string ProductName
        {
            get { return this.GetPropertyStringData(EDSDK.PropID_ProductName, 0); }
        }        

        public string SerialNumber
        {
            get { return this.GetPropertyStringData(EDSDK.PropID_BodyIDEx, 0); }
        }

        public EosCameraSavePicturesTo SavePicturesTo
        {
            set
            {
                this.CheckDisposed();

                this.EnsureOpenSession();

                EosAssert.NotOk(EDSDK.EdsSetPropertyData(_camera, EDSDK.PropID_SaveTo, 0, Marshal.SizeOf(typeof(int)), (int)value), "Failed to set SaveTo location.");
                this.RunSynced(() =>
                {
                    var capacity = new EDSDK.EdsCapacity { NumberOfFreeClusters = 0x7FFFFFFF, BytesPerSector = 0x1000, Reset = 1 };
                    EosAssert.NotOk(EDSDK.EdsSetCapacity(_camera, capacity), "Failed to set capacity.");
                });
            }
        }

        public void SavePicturesToHostLocation(string path)
        {
            this.CheckDisposed();

            _picturePath = path;
            if (!Directory.Exists(_picturePath))
                Directory.CreateDirectory(_picturePath);
            this.SavePicturesTo = EosCameraSavePicturesTo.Host;                        
        }

        public void TakePicture()
        {
            this.SendCommand(EDSDK.CameraCommand_TakePicture, 0);            
        }

        private void SendCommand(uint command, int parameter)
        {
            this.EnsureOpenSession();            
            EosAssert.NotOk(EDSDK.EdsSendCommand(_camera, command, parameter), string.Format("Failed to send command: {0} with parameter {1}", command, parameter));            
        }

        private string GetPropertyStringData(uint propertyId, int parameter)
        {
            string data;
            EosAssert.NotOk(EDSDK.EdsGetPropertyData(_camera, propertyId, parameter, out data), 
                string.Format("Failed to get property string data: {0} with parameter {1}", propertyId, parameter));
            return data;
        }

        private void RunSynced(Action action)
        {
            this.CheckDisposed();

            EosAssert.NotOk(EDSDK.EdsSendStatusCommand(_camera, EDSDK.CameraState_UILock, 0), "Failed to lock camera.");
            try
            {
                action();
            }
            finally
            {
                EDSDK.EdsSendStatusCommand(_camera, EDSDK.CameraState_UIUnLock, 0);
            }
        }        

        private void EnsureOpenSession()
        {
            this.CheckDisposed();
            if (!_sessionOpened)
            {
                EosAssert.NotOk(EDSDK.EdsOpenSession(_camera), "Failed to open session.");
                _sessionOpened = true;
            }
        }

        protected internal override void DisposeUnmanaged()
        {            
            if (_sessionOpened)
                EDSDK.EdsCloseSession(_camera);

            EDSDK.EdsRelease(_camera);
            base.DisposeUnmanaged();
        }

        public override string ToString()
        {
            return this.DeviceDescription;
        }
    }
}
