using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace Lucker.Devices
{
    public partial class BillToBillDevice
    {
        public abstract class CommandHandlerBase
        {
            readonly protected BillToBillDevice m_Device;
            
            public CommandHandlerBase(BillToBillDevice device)
            {
                m_Device = device;
            }

        }
        public abstract class CommandHandler<T> 
            : CommandHandlerBase where T : class
        {
            readonly protected byte[] m_CommandData;
            readonly protected InterfaceCommand m_InterfaceCommand;

            public CommandHandler(
            BillToBillDevice device,InterfaceCommand interfaceCommand,
                byte[] commandData) : base(device)
            {
                m_CommandData = commandData;
                m_InterfaceCommand = interfaceCommand;    
            }

            bool HandleCommandWithRetData(
             InterfaceCommand command, byte[] commandData,
                out byte[] responseData)
            {
                bool commandHandledSuccessfully = false;

                //send command at device
                m_Device.SendCmdMessage(command, commandData);
                //wait timeout response filled
                Thread.Sleep(B2B_RETURN_ACK_TIMEOUT_MSEC);
                if (Helper.GetDeviceCmdResponse(
                    m_Device.m_CommunicationPort, out responseData)
                    && Response.CheckDataPacket(responseData)
                    && NakResponse.CheckData(responseData) == false)
                {
                    commandHandledSuccessfully = true;
                }
                return commandHandledSuccessfully;
            }

            public bool Handle(out T response)
            {
                bool ret = false;
                response = null;
                byte[] responseData;

                lock (m_Device.m_PortLocker)
                {
                    if (HandleCommandWithRetData(
                        m_InterfaceCommand, m_CommandData, out responseData)
                        && Response.CheckDataPacket(responseData)
                        && NakResponse.CheckData(responseData) == false)
                    {
                        if (OnHandle(responseData, out response) == true)
                        {
                            m_Device.SendAckMessage();
                            ret = true;
                        }
                    }
                }
                return ret;
            }
            protected abstract bool OnHandle(byte[] responseData,out T response );
            //protected abstract IEnumerable<PollResponseCode> ApplicableStatesCmdExec();

        }
        public class GetStatusCmdHandler : CommandHandler<GetStatusResponse>
        {
            public GetStatusCmdHandler(BillToBillDevice device) :
                base(device,InterfaceCommand.Get_Status,null)
            {
            }
            protected override bool OnHandle(byte[] responseData,
                out GetStatusResponse response)
            {
                bool ret = false;
                response = null;
                if(GetStatusResponse.CheckData(responseData))
                {
                    response = GetStatusResponse.CreateFromData(
                        responseData);
                    ret = true;
                }
                return ret;
            }
        }
        public class GetBillTableCmdHandler : CommandHandler<GetBillTableResponse>
        {
            public GetBillTableCmdHandler(BillToBillDevice device) :
                base(device,InterfaceCommand.GetBillTable,null)
            {
            }
            protected override bool OnHandle(byte[] responseData,
                out GetBillTableResponse response)
            {
                bool ret = false;
                response = null;
                if(GetBillTableResponse.CheckData(responseData))
                {
                    response = GetBillTableResponse.CreateFromData(
                        responseData);
                    ret = true;
                }
                return ret;
            }
        }
        public class GetRecyclingCasseteStatusCmdHandler : 
            CommandHandler<GetRecyclingCassetteStatusResponse>
        {
            //readonly GetBillTableResponse m_GetBillTableResponse;
            public GetRecyclingCasseteStatusCmdHandler(
                BillToBillDevice device) :
                base(device,InterfaceCommand.Recycling_Cassette_Status,null)
            {
                
            }
            protected override bool OnHandle(byte[] responseData, 
                out GetRecyclingCassetteStatusResponse response)
            {
                bool ret = false;
                response = null;
                if (GetRecyclingCassetteStatusResponse.CheckData(responseData))
                {
                    response = GetRecyclingCassetteStatusResponse.CreateFromData(
                        responseData/*,m_GetBillTableResponse*/);
                    ret = true;
                }
                return ret;
            }
        }
        public class PollCmdHandler : CommandHandler<ResponsePoll>
        {
            public PollCmdHandler(BillToBillDevice device,
                InterfaceCommand command,byte[] commandData) : base(
                device,command,commandData)
            {

            }
            protected override bool OnHandle(byte[] responseData, 
                out ResponsePoll response)
            {
                bool ret = false;
                response = null;
                if (ResponsePoll.CheckDataPacket(responseData) == true)
                {
                    response = ResponsePoll.CreateFromData(responseData);
                    ret = true;
                }
                return ret;
            }
        }

        public abstract class CmdHandlerWithoutRetData : CommandHandlerBase
        {
            InterfaceCommand m_InterfaceCommand;
            public CmdHandlerWithoutRetData(BillToBillDevice device,
                InterfaceCommand interfaceCommand)
                :base(device)
            {
                m_InterfaceCommand = interfaceCommand;
            }
            bool CheckApplicableState(PollResponseCode currentState,
                IEnumerable<PollResponseCode> applicableStates)
            {
                bool ret = false;
                foreach (var state in applicableStates)
                {
                    if (currentState == state)
                        ret = true;
                }
                return ret;
            }
            public bool Handle()
            {
                bool ret = false;

                bool handleCmdRet;
                var commandData = new List<byte>(GetCommandData()).ToArray();
                if (commandData.Length > 0)
                {
                    lock (m_Device.m_PortLocker)
                    {
                        if (CheckApplicableState(m_Device.m_PollState,
                            CmdExecApplicableStates()))
                        {

                            //send command 
                            handleCmdRet = m_Device.HandleCommandWithNoRetData(
                                m_InterfaceCommand, commandData
                                );

                            //if we get ack response
                            if (handleCmdRet == true)
                            {
                                //ok 
                                ret = true;
                            }
                        }
                    }
                }

                return ret;
            }

            protected abstract IEnumerable<byte> GetCommandData();
            protected abstract IEnumerable<PollResponseCode> CmdExecApplicableStates();
        }

        public class SetRecyclingCassetteTypeCmdHandler : CmdHandlerWithoutRetData
        {
            readonly byte m_CassetteNumber, m_BillTypeId;

            public SetRecyclingCassetteTypeCmdHandler(
                BillToBillDevice device,
                byte cassetteNumber,byte billTypeId) :
                base(device,InterfaceCommand.Set_Recycling_Cassette_Type)
            {
                m_CassetteNumber = cassetteNumber;
                m_BillTypeId = billTypeId;
            }
            protected override IEnumerable<PollResponseCode> CmdExecApplicableStates()
            {
                yield return PollResponseCode.Unit_Disabled;
            }
            protected override IEnumerable<byte> GetCommandData()
            {
                yield return m_CassetteNumber;
                yield return m_BillTypeId ;
            }
        }

        public class DispCmdItem
        {
            readonly byte m_BillTypeId,m_BillCount;

            public byte BillTypeId
            {
                get {return m_BillTypeId;}
            }
            public byte BillCount
            {
                get { return m_BillCount;}
            }
            public DispCmdItem(byte billTypeId,byte billCount)
            {
                m_BillTypeId = billTypeId;
                m_BillCount = billCount;
            }
        }
        public class DispenseCmdHandler : CmdHandlerWithoutRetData
        {
            readonly DispCmdItem[] m_DispCmdItems;

            public DispenseCmdHandler(BillToBillDevice device,
                DispCmdItem[] dispCmdItems)
                : base(device,InterfaceCommand.Dispense)
            {
                m_DispCmdItems = dispCmdItems;
            }
            protected override IEnumerable<byte> GetCommandData()
            {
                Debug.Assert(m_DispCmdItems != null, "m_DispCmdItems != null");
                foreach (var dispCmdItem in m_DispCmdItems)
                {
                    Debug.Assert(dispCmdItem != null, "dispCmdItem != null");

                    Debug.WriteLine(
                        string.Format("dispCmdItem.BillTypeId - {0:0X}",
                        dispCmdItem.BillTypeId));
                    yield return dispCmdItem.BillTypeId;
                    yield return dispCmdItem.BillCount;
                }
            }
            protected override IEnumerable<PollResponseCode> CmdExecApplicableStates()
            {
                yield return PollResponseCode.Unit_Disabled;
            }

        }
    }
}
