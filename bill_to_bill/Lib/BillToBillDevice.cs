using System;
using System.IO.Ports;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;

namespace Lucker.Devices
{
    public partial class BillToBillDevice
    {
        #region nested classes

        public class PollStatus : EventArgs
        {
            readonly PollResponseCode m_PollResponseCode;

            public PollResponseCode PollResponseCode
            {
                get
                {
                    return m_PollResponseCode;
                }
            }

            public PollStatus(PollResponseCode pollResponseCode)
            {
                m_PollResponseCode = pollResponseCode;
            }
        }

        public class DeviceInfoEventArgs : EventArgs
        {
            readonly string m_DevicePortNumber;

            public string DevicePortNumber
            {
                get
                {
                    return m_DevicePortNumber;
                }
            }

            public DeviceInfoEventArgs(string serialPortNumber)
            {
                m_DevicePortNumber = serialPortNumber;
            }

        }

        public class BillIncomeEventArgs : EventArgs
        {
            readonly int m_BillNominal;

            public int BillNominal
            {
                get
                {
                    return m_BillNominal;
                }
            }


            public BillIncomeEventArgs(int billNominal)
            {
                m_BillNominal = billNominal;
            }
        }

        public class BillType
        {
            readonly byte m_TableIndex;
            byte m_FirstDenominationCode, m_SecondDenominationCode;
            string m_CountryCode;

            public byte TableIndex
            {
                get
                {
                    return m_TableIndex;
                }
            }
            

            public BillType(byte tableIndex,
                byte firstDenominationCode, byte secondDenominationCode,
                    string countryCode)
            {
                m_TableIndex = tableIndex;
                m_FirstDenominationCode = firstDenominationCode;
                m_SecondDenominationCode = secondDenominationCode;
                m_CountryCode = countryCode;
            }
            public int Nominal()
            {
                return m_FirstDenominationCode * (int)Math.Pow(10,m_SecondDenominationCode);
            }
            public override string ToString()
            {
                return this.Nominal().ToString();
            }

        }



        #endregion

        #region enums
        public enum InterfaceAddress : byte
        {
            None,
            B2B = 0x01,
            FL = 0x03
        }
        public enum InterfaceCommand : byte
        {
            None,
            Reset = 0x30,
            Get_Status = 0x31,
            SetSecurity = 0x32,
            Poll = 0x33,
            Enable_Bill_Types = 0x34,
            Recycling_Cassette_Status = 0x3B,
            Dispense = 0x3C,
            EmptyCassete = 0x3D,
            Set_Recycling_Cassette_Type = 0x40,
            GetBillTable = 0x41,
            EmptyDispenser = 0x67

        }

        public enum PollResponseCode : byte
		{
			None = 0,
			PowerUp = 0x10,
			Initialize = 0x13,
			Idle = 0x14,
			Accepting = 0x15,
			Unit_Disabled = 0x19,
            Dispensing = 0x1D,
            Dispensed = 0x25,
            Jam_In_Acceptor = 0x43,
            Jam_In_Stacker = 0x44,
			Drop_Cassete_Out_Of_Position = 0x42,
			Bill_Stacked = 0x81
		}
        #endregion



        #region variables

        #region consts

        const byte SYNC = 0x02;

        const int BAUD_RATE = 9600;
        const int DATA_BITS = 8;
        const Parity PARITY = Parity.None;
        const StopBits STOP_BITS = StopBits.One;

        const int ATTEMPTS_COUNT_WAIT_DEVICE_TRANSMIT_IN_DISABLED_STATE_AFTER_RESET = 10;
        const int CHECK_DEVICE_TRANSMIT_IN_DISABLED_STATE_INTERVAL_MSEC = 5000;

        const int POLLING_THREAD_ENDING_TIMEOUT = 5000;

        const int B2B_RETURN_ACK_TIMEOUT_MSEC = 20;
        const int BV_BETWEEN_CMD_SEND_DELAY_MSEC = 150; 
        const int BV_RESPONSE_TIMEOUT_MSEC = 5000; // 5 sec
        const int BV_COMMAND_MIN_LENGTH = 3;

        const int NAK_PACKET_LENGTH = 0x06;
        const byte NAK_RESPONSE_DATA = 0xFF;
        const int ACK_PACKET_LENGTH = 0x06;
        const byte ACK_RESPONSE_DATA = 0x00;
        const byte UNKNOWN_ACK_RESPONSE_DATA = 0x30;

        const int RESPONSE_PACKET_MIN_LEN = 0x06;
        const int SYNC_CODE_OFFSET = 0x0;
        const int INTERFACE_CODE_OFFSET = 0x01;
        const int PACKET_LENGTH_OFFSET = 0x02;
        const int DATA_OFFSET = 0x03;
        const int CRC_LENGTH = 0x02;



        #endregion


        public static event EventHandler<DeviceInfoEventArgs> OnSearchResult;
        public event EventHandler<PollStatus> OnPollStatus;
        public event EventHandler<BillIncomeEventArgs> OnBillIncome;
        public event EventHandler OnDispensing;
        public event EventHandler OnDispenseEnd;
        public event EventHandler OnJam;


        bool m_Initialized = false;
        //bool m_DeviceInIdlingState = false;

        AutoResetEvent m_DevicePollingEndedEvent = new AutoResetEvent(false);
        volatile PollResponseCode m_PollState = PollResponseCode.None;
        //Device m_Device;
        BackgroundWorker m_ResponseHandler;

        readonly SerialPort m_CommunicationPort;
        readonly InterfaceAddress m_InterfaceAddress;
        GetBillTableResponse m_BillTable;
        Cassette[] m_ResyclingCassettes;
        bool m_Opened = false;
        object m_PortLocker;
        
        #endregion

        #region props
        public Cassette[] ResyclingCassettes
        {
            get { return m_ResyclingCassettes; }
        }
        public PollResponseCode PollState
        {
            get { return m_PollState; }
        }
        public BillType[] BillTypes
        {
            get { return m_BillTable.BillTypes; }
        }
        #endregion

        public BillToBillDevice(
            string communicationPortName
                )
        {
            //configure serial port -------------
            var serialPort = CreateSerialPort();
            serialPort.PortName = communicationPortName;
            m_CommunicationPort = serialPort;
            //----------------------------------

            m_InterfaceAddress = InterfaceAddress.B2B;
            //object used for synchronize port read \ write requests
            m_PortLocker = new object();
            // device responses handler
            m_ResponseHandler = CreateResponseHandler();
        }

        #region static methods

        static bool CheckDeviceAvalibility(string portName)
        {
            bool deviceAvailable = false;
            Debug.Assert(portName != null, "portName != null");
            var device = new BillToBillDevice(portName);
            //try initialize device
            var deviceOpened = device.Open();
            if (deviceOpened == true)
            {
                //poll device
                //device successfully polled

                if (device.PollDevice() == true)
                {
                    deviceAvailable = true;
                }

                //close device
                device.Close();
            }
            return deviceAvailable;

        }
        public static bool SearchDevice(out string portName)
        {
            var deviceFound = false;
            portName = null;
            foreach (var _portName in SerialPort.GetPortNames())
            {
                if (CheckDeviceAvalibility(_portName) == true)
                {
                    deviceFound = true;
                    portName = _portName;
                    break;
                }
            }
            return deviceFound;
        }

        #endregion

        #region private methods

        #region initialize methods

        SerialPort CreateSerialPort()
        {
            var serialPort = new SerialPort();
            
            serialPort.BaudRate = BAUD_RATE;
            serialPort.Parity = PARITY;
            serialPort.StopBits = STOP_BITS;
            serialPort.DataBits = DATA_BITS;

            return serialPort;
        }

        BackgroundWorker CreateResponseHandler()
        {
            var responseHandler = new BackgroundWorker();
            responseHandler.WorkerSupportsCancellation = true;
            responseHandler.DoWork +=
                delegate(object sender, DoWorkEventArgs e)
                {
                    do
                    {
                        System.Threading.Thread.Sleep(150);
                        ResponsePoll pollResponse;
                        PollDevice(out pollResponse);
                        HandlePollResponse(pollResponse);

                        System.Threading.Thread.Sleep(B2B_RETURN_ACK_TIMEOUT_MSEC);
                    } while (((BackgroundWorker)sender).CancellationPending == false);
                    //signal polling thread finished
                    m_DevicePollingEndedEvent.Set();
                };
            responseHandler.RunWorkerCompleted +=
                delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    if (e.Error != null)
                        throw e.Error;
                    
                };
            return responseHandler;
        }
        /*bool StartupDevice()
        {
            bool deviceStarted = false;

            //some time need device reset
            PollResponseCode pollState;
            var cycleCount = 0;
            do
            { 
                Thread.Sleep(CHECK_DEVICE_TRANSMIT_IN_DISABLED_STATE_INTERVAL_MSEC);
                //send poll command, check is device trmansmit in disabled state
                byte[] pollResponseData;
                var pollDeviceCmdExecStatus = 
                    this.HandleCommand(InterfaceCommand.Poll,null,out pollResponseData);
                Debug.Assert(pollDeviceCmdExecStatus == true,
                    "pollDeviceCmdExecStatus == true");
                pollState = ResponsePoll.CreateFromData(pollResponseData).FirstCode;
                cycleCount++;
            } while (cycleCount <= 
                ATTEMPTS_COUNT_WAIT_DEVICE_TRANSMIT_IN_DISABLED_STATE_AFTER_RESET
                && pollState != PollResponseCode.Unit_Disabled);

            if (pollState == PollResponseCode.Unit_Disabled)
            {
                byte[] enableBillTypesResponseData;
                var enableBillTypesCmdExecStatus  = this.HandleCommand(
                    InterfaceCommand.Enable_Bill_Types,
                    new byte[] { 0xff, 0xff, 0xff, 0x00, 0x00, 0x00 },
                    out enableBillTypesResponseData);
                Debug.Assert(enableBillTypesCmdExecStatus == true,
                    "enableBillTypesCmdExecStatus == true");
                deviceStarted = true;

            }
            return deviceStarted;

        }*/
        bool EnableDevice()
        {
            
            return this.HandleCommandWithNoRetData(
                InterfaceCommand.Enable_Bill_Types,
                new byte[] { 0xff, 0xff, 0xff, 0x00, 0x00, 0x00 });
        }
        bool DisableDevice()
        {
            
            return this.HandleCommandWithNoRetData(
                InterfaceCommand.Enable_Bill_Types,
                new byte[] {0x00,0x00,0x00,0x00,0x00,0x00});
        }
        #endregion

        public bool Open()
        {
            bool successfullyOpened = true;
            try
            {
                m_CommunicationPort.Open();
                m_Opened = true;

                m_CommunicationPort.DiscardInBuffer();
                m_CommunicationPort.DiscardOutBuffer();

            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException || e is IOException)
                {
                    successfullyOpened = false;
                }
                else
                    throw;
            }
            return successfullyOpened;
        }

        public void Close()
        {
            if (m_Opened == true)
            {
                m_CommunicationPort.Close();
                m_Opened = !m_Opened;
            }
        }

        void SendMessageLow(byte[] sendingData)
        {
            Debug.Assert(sendingData != null, "sendingData != null");
            Debug.Assert(m_CommunicationPort.IsOpen == true,
                "m_CommunicationPort.IsOpen == true");

            //put message bytes in port
            m_CommunicationPort.Write(sendingData, 0, sendingData.Length);
            //TODO : Check timeout
        }

        void SendCmdMessage(
            InterfaceCommand interfaceCommand, byte[] data)
        {
            var cmdMessage = new MessageCmd(
                m_InterfaceAddress, interfaceCommand, data);

            //get message data bytes
            var sendData = cmdMessage.GetSendingData();
            //send data bytes at port
            SendMessageLow(sendData);
        }

        void SendAckMessage()
        {
            var ackMsg = new MessageAck(
                m_InterfaceAddress);
            SendMessageLow(ackMsg.GetSendingData());
        }


        bool HandleCommandWithNoRetData(
            InterfaceCommand command,byte[] commandData)
        {
            bool commandHandledSuccessfully = false;
            lock (m_PortLocker)
            {
                byte[] responseData;
                SendCmdMessage(command, commandData);
                Thread.Sleep(B2B_RETURN_ACK_TIMEOUT_MSEC);
                if (Helper.GetDeviceCmdResponse(m_CommunicationPort,out responseData) &&
                    Response.CheckDataPacket(responseData) &&
                    AckResponse.CheckData(responseData)
                    )
                {
                    
                    //SendAckMessage();
                    //GetAckMessage();
                    commandHandledSuccessfully = true;
                }
            }
            return commandHandledSuccessfully;
        }



        /*bool HandleCommandWithRetData(
            InterfaceCommand command, byte[] commandData)
        {
            byte[] responseData;
            return this.HandleCommandWithRetData(
                command, commandData, out responseData);
        }*/



        bool PollDevice(out ResponsePoll pollResponse)
        {
            bool ret = false;
            pollResponse = null;

            var pollCmdHandler = new PollCmdHandler(
                this, InterfaceCommand.Poll, null);

            if (pollCmdHandler.Handle(out pollResponse) == true)
            {
                ret = true;
            }
            return ret;

        }
        bool PollDevice()
        {
            ResponsePoll pollResponse;
            return PollDevice(out pollResponse);
        }

        void GetBillTable(out GetBillTableResponse getBillTableResponse)
        {
            getBillTableResponse = null;

            var getBillTableCmdHandler = new GetBillTableCmdHandler(this);
            GetBillTableResponse response;
            if (getBillTableCmdHandler.Handle(out response) == true)
            {
                getBillTableResponse = response;
            }
            
        }
        void ResetDevice()
        {
            var resetCmdExecStatus = this.HandleCommandWithNoRetData(
                InterfaceCommand.Reset, null);
        }
        
        /*public void SendEnableBillTypesCommand()
        {
            byte[] response;
            HandleCommand(InterfaceCommand.Enable_Bill_Types,
                new byte[] { 0xff,0xff,0xff,0x00,0x00,0x00},out response);
        }*/
        #endregion

        void HandleBillStacked(byte billTypeIndex)
        {
            Debug.Assert(m_BillTable != null, "m_BillTable != null");
            Debug.Assert(m_BillTable.BillTypes != null,
                "m_BillTable.BillTypes != null");

            var targetBillType = Array.Find<BillType>(
                m_BillTable.BillTypes,
                delegate(BillType billType)
                {
                    Debug.Assert(billType != null, "billType != null");
                    return billType.TableIndex == billTypeIndex;
                });
            Debug.Assert(targetBillType != null, "targetBillType != null");

            if (OnBillIncome != null)
                OnBillIncome(this, new BillIncomeEventArgs(targetBillType.Nominal()));
            
        }

        void HandlePollResponse(ResponsePoll pollResponse)
        {
            Debug.WriteLine(
                string.Format("Poll - {0:X} , {1:X}", 
                pollResponse.FirstCode,pollResponse.SecondCode));
            if (m_PollState != pollResponse.FirstCode)
            {
                switch (pollResponse.FirstCode)
                {
                    case PollResponseCode.Bill_Stacked:
                        HandleBillStacked(pollResponse.SecondCode);

                        break;
                    case PollResponseCode.Idle:
                        break;
                    case PollResponseCode.Drop_Cassete_Out_Of_Position:
                        if (m_PollState != PollResponseCode.Drop_Cassete_Out_Of_Position)
                        {
                            Debug.WriteLine("Casste dropped");
                        }
                        break;
                    case PollResponseCode.Unit_Disabled:
                        if (m_PollState == PollResponseCode.Dispensed)
                        {
                            if (OnDispenseEnd != null)
                                OnDispenseEnd(this, EventArgs.Empty);
                        }
                        break;
                    case PollResponseCode.Dispensing:
                        {
                            if (OnDispensing != null)
                                OnDispensing(this, EventArgs.Empty);
                        }
                        break;
                    case PollResponseCode.Jam_In_Acceptor:
                    case PollResponseCode.Jam_In_Stacker:
                        {
                            if (OnJam != null)
                                OnJam(this, EventArgs.Empty);
                            break;
                        }
                            
                    default:
                        Debug.WriteLine("BV Poll Response " + pollResponse.FirstCode);
                        break;

                }

                m_PollState = pollResponse.FirstCode;
                if (OnPollStatus != null)
                    OnPollStatus(this, new PollStatus(pollResponse.FirstCode));
            }

        }




        #region public methods

        /*public void Test()
        {
            
        }*/

        public void LoadSettingsAndStart()
        {
            if (m_Initialized == true)
            {
                throw new InvalidOperationException("Already initialized");
            }

            string portName;
            var deviceFound = BillToBillDevice.SearchDevice(out portName);
            if (deviceFound == true)
            {
                //var device = new BillToBillDevice(portName);
                if (OnSearchResult != null)
                    OnSearchResult(this, new DeviceInfoEventArgs(portName));
                Debug.Assert(portName != null, "portName != null");
                var deviceOpened = this.Open();
                Debug.Assert(deviceOpened == true, "deviceOpened == true");
                m_Initialized = true;

                PollDevice();
                //send reset command to device (transmit in initial state it)
                ResetDevice();
            }
            PollResponseCode pollState = PollResponseCode.None;
            var cycleCount = 0;
            do
            {
                Thread.Sleep(CHECK_DEVICE_TRANSMIT_IN_DISABLED_STATE_INTERVAL_MSEC);
                //send poll command, check is device trmansmit in disabled state
                ResponsePoll pollResponse;
                PollDevice(out pollResponse);
                if (pollResponse.FirstCode == PollResponseCode.PowerUp)
                {
                    ResetDevice();
                    continue;
                }
                
                pollState = pollResponse.FirstCode;
                Debug.WriteLine(
                    string.Format(" Cycle - {0:X} ", cycleCount));
                cycleCount++;
            } while (cycleCount <=
                ATTEMPTS_COUNT_WAIT_DEVICE_TRANSMIT_IN_DISABLED_STATE_AFTER_RESET
                && pollState != PollResponseCode.Unit_Disabled);
            if (cycleCount >
                ATTEMPTS_COUNT_WAIT_DEVICE_TRANSMIT_IN_DISABLED_STATE_AFTER_RESET)
            {
                throw new Exception("Device startup failed");
            }
            Debug.Assert(pollState == PollResponseCode.Unit_Disabled,
                "pollState == PollResponseCode.Unit_Disabled");

            GetBillTable(out m_BillTable);
            m_ResyclingCassettes = new List<Cassette>(CreateCassettes()).ToArray();
            StartDevicePolling();
        }
        IEnumerable<Cassette> CreateCassettes()
        {
            GetRecyclingCassetteStatusResponse recyclingCassetteStatusResponse;
            //send device command  - get recycling cassette status
            var getRecyclingCassetteStatusOk =
                new GetRecyclingCasseteStatusCmdHandler(this).Handle(
               out recyclingCassetteStatusResponse);
            //debug checks
            Debug.Assert(getRecyclingCassetteStatusOk == true,
                "getRecyclingCassetteStatusOk == true");
            Debug.Assert(recyclingCassetteStatusResponse.CassetteStatuses != null,
                "recyclingCassetteStatusResponse.CassetteStatuses != null");

            
            for (byte i = 0; 
                i < recyclingCassetteStatusResponse.CassetteStatuses.Length; i++)
            {
                if (recyclingCassetteStatusResponse.CassetteStatuses[i].CassettePresent == true)
                {
                    yield return new Cassette(i, this);
                }
            }

        }
        void StartDevicePolling()
        {
            Debug.Assert(m_ResponseHandler != null, "m_ResponseHandler != null");
            Debug.Assert(m_ResponseHandler.IsBusy == false,
                         "m_ResponseHandler.IsBusy == false");
            m_ResponseHandler.RunWorkerAsync();
        }
        void StopDevicePolling()
        {
            Debug.Assert(m_ResponseHandler != null, "m_ResponseHandler != null");
            Debug.Assert(m_ResponseHandler.IsBusy == true,
                         "m_ResponseHandler.IsBusy == true");
            //request polling thread ended
            m_ResponseHandler.CancelAsync();
            //wait polling thread ended
            var endedSuccessfully = m_DevicePollingEndedEvent.WaitOne(
                POLLING_THREAD_ENDING_TIMEOUT, false);
            Debug.Assert(endedSuccessfully == true, "endedSuccessfully == true");
        }
        public void ManualEnable()
        {
            if (m_Initialized == false)
                throw new InvalidOperationException("Initialization requed");
            //if (m_DeviceInIdlingState == true)
                //throw new InvalidOperationException("Device already started");

            
            //start device
            var enableDeviceOk = EnableDevice();
            Debug.Assert(enableDeviceOk == true,"enableDeviceOk == true");
            //m_DeviceInIdlingState = true;
        }

        public void ManualDisable()
        {
            if (m_Initialized == false)
                throw new InvalidOperationException("Initialization requed");
            //if (m_DeviceInIdlingState == false)
                //throw new InvalidOperationException("Device already stopped");
            //stop device polling thread
            //disable device
            var disableDeviceOk = DisableDevice();
            Debug.Assert(disableDeviceOk == true,"disableDeviceOk == true");
            //m_DeviceInIdlingState = false;
        }

        IEnumerable< Cassette > GetCassettesWithBills()
        {
            foreach (var cassette in this.ResyclingCassettes)
            {
                Debug.Assert(cassette != null, "cassette != null");
                int numberOfBills;
                var getNumberOfBillsOk = cassette.GetNumberOfBills(out numberOfBills);
                Debug.Assert(getNumberOfBillsOk == true, "getNumberOfBillsOk == true");
                if (numberOfBills > 0)
                    yield return cassette;
            }
        }
        SortedList<int, Cassette> GetSortedByNominalCassettes()
        {
            var cassettesByNominal = new SortedList<int, Cassette>();
            var cassettesWithBills = GetCassettesWithBills();
            foreach (var cassetteWithBills in cassettesWithBills)
            {
                Debug.Assert(cassetteWithBills != null, "cassetteWithBills != null");
                BillType cassetteBillType;
                //get cassette bill type
                var cassetteBillTypeGetOk = cassetteWithBills.GetBillType(
                    out cassetteBillType);
                Debug.Assert(cassetteBillTypeGetOk == true, "cassetteBillTypeGetOk == true");
                //add cassette at list
                var nominal = cassetteBillType.Nominal();
                cassettesByNominal.Add(nominal, cassetteWithBills);
            }
            return cassettesByNominal;
        }
        IEnumerable<DispCmdItem> GetDispCmdItems(int amount)
        {
            Debug.Assert(this.ResyclingCassettes != null,
                "this.ResyclingCassettes != null");
            var cassettesSortedByNominal = GetSortedByNominalCassettes();
            //loop via cassettes


            for (int i = cassettesSortedByNominal.Count - 1; i >= 0; i--)
            {
                var cassetteBillNominal = cassettesSortedByNominal.Keys[i];
                Debug.Assert(cassetteBillNominal != 0, "cassetteBillNominal != 0");
                int piece = (int)(amount / cassetteBillNominal);
                if (piece > 0)
                {
                    int numberOfBills;
                    cassettesSortedByNominal.Values[i].GetNumberOfBills(out numberOfBills);
                    var min = Math.Min(piece, numberOfBills);
                    Debug.Assert(min > 0, "min > 0");
                    Debug.Assert(amount >= cassetteBillNominal * min,
                        "cassetteBillNominal * min");
                    amount -= (cassetteBillNominal * min);

                    BillType billType;
                    cassettesSortedByNominal.Values[i].GetBillType(out billType);

                    yield return new DispCmdItem(
                        billType.TableIndex, (byte)min);
                }
            }
        }

        void DispenseDevice(int amount)
        {
            var dispCmdItems = GetDispCmdItems(amount);
            var dispenseCmdHandledOk =
                new DispenseCmdHandler(this,
                    new List<DispCmdItem>(dispCmdItems).ToArray()).Handle();

            if (OnDispensing != null)
                OnDispensing(this, EventArgs.Empty);
            /*Debug.Assert(dispenseCmdHandledOk == true,
                "dispenseCmdHandledOk == true");*/
            
        }

        public void Dispense(int amount)
        {
            
            if(m_Initialized == false)
                throw new InvalidOperationException("Initialization requed");

            //if device in idling state
            if (m_PollState == PollResponseCode.Idle)
            {
                //send disable command ( disabled state need for dispensing)
                DisableDevice();
                //wait while device disabled
                bool waitOk = WaitState(PollResponseCode.Unit_Disabled);
                Debug.Assert(waitOk == true, "waitOk == true");
                Debug.Assert(m_PollState == PollResponseCode.Unit_Disabled,
                    "m_PollState == PollResponseCode.Unit_Disabled");
                //deviceDisabled = true;
            }
            
            DispenseDevice(amount);
        }
        bool WaitState(PollResponseCode waitingState)
        {
            bool ret = false;

            int sleepTime = 500;
            int maxCycleCount = 20; //10 sec;

            for (int i = 0; i < maxCycleCount; i++)
            {
                if (m_PollState == waitingState)
                {
                    Debug.WriteLine("Wait : last cycle - " + i);
                    ret = true;
                    break;
                }
                Thread.Sleep(sleepTime);
            }

            return ret;

        }

        public void GetStatus()
        {
            var getStatusCmdHandler = new GetStatusCmdHandler(this);
            GetStatusResponse getStatusResponse;
            getStatusCmdHandler.Handle(out getStatusResponse);
        }

        /*public void SetRecyclingCassetteStatus()
        {
            HandleCommandWithNoRetData(InterfaceCommand.Set_Recycling_Cassette_Type,
                    new byte[] { 0x1,0x1});
        }*/
        public BillType[] GetBillTypes()
        {
            if (m_Initialized == false)
                throw new InvalidOperationException("Initialization requed");
            Debug.Assert(m_BillTable != null, "m_BillTable != null");
            return m_BillTable.BillTypes;
        }
        
        #endregion

    }
}

