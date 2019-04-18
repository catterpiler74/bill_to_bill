using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Lucker.Devices
{
    public partial class BillToBillDevice
    {
        public class Cassette
        {
            readonly byte m_CassetteNumber;
            readonly BillToBillDevice m_Device;

            #region props
            public byte CassetteNumber
            {
                get { return m_CassetteNumber; }
            }
            #endregion

            public Cassette(byte cassetteNumber, BillToBillDevice device)
            {
                m_CassetteNumber = (byte)(cassetteNumber);
                m_Device = device;
            }
            //helper method
            bool GetCasseteStatus(out CassetteStatus cassetteStatus)
            {
                bool ret = false;
                cassetteStatus = null;

                //create command handler 
                var getCasseteStatusCmdHandler = new 
                    GetRecyclingCasseteStatusCmdHandler(m_Device);

                GetRecyclingCassetteStatusResponse recycleCasseteStatusResponse;

                //send command
                if (getCasseteStatusCmdHandler.Handle(
                    out recycleCasseteStatusResponse) == true)
                {
                    Debug.Assert(recycleCasseteStatusResponse != null,
                        "recycleCasseteStatusResponse != null");
                    Debug.Assert(
                        recycleCasseteStatusResponse.CassetteStatuses.Length >
                        m_CassetteNumber,
                        "recycleCasseteStatusResponse.CassetteStatuses.Length > " +
                        "m_CassetteNumber");

                    //assign result
                    cassetteStatus = recycleCasseteStatusResponse.CassetteStatuses[
                        m_CassetteNumber];
                    ret = true;
                            
                }

                return ret ;

            }

            public bool SetBillType(BillType billType)
            {
                bool ret = false;
                Debug.Assert(billType != null, "billType != null");

                //bool deviceDisabled = false;
                //if device uninitialized , throw exception
                if(m_Device.m_Initialized == false)
                    throw new InvalidOperationException("Initialization requed");
                
                //if device in iding (running / polling) state
                if (m_Device.m_PollState == PollResponseCode.Idle)
                {
                    //stop it
                    m_Device.DisableDevice();
                    var waitOk = m_Device.WaitState(PollResponseCode.Unit_Disabled);
                    Debug.Assert(waitOk == true,"waitOk == true");
                    //deviceDisabled = true;
                }
                //send command to device ( set cassette bill type )
                var cmdHandler = new SetRecyclingCassetteTypeCmdHandler(m_Device,
                    (byte)(this.CassetteNumber + 1), billType.TableIndex);
                if (cmdHandler.Handle() == true)
                {
                    
                    
                    ret = true;
                }
                /*if(deviceDisabled == true)
                    m_Device.EnableDevice();*/

                return ret;
            }
            public bool GetBillType(out BillType billType)
            {
                bool ret = false;
                billType = null;

                GetRecyclingCassetteStatusResponse recyclingCassetteStatusResponse;
                //
                new GetRecyclingCasseteStatusCmdHandler(m_Device).Handle(
                    out recyclingCassetteStatusResponse);

                if (new GetRecyclingCasseteStatusCmdHandler(m_Device).Handle(
                    out recyclingCassetteStatusResponse) == true)
                {
                    Debug.Assert(
                        recyclingCassetteStatusResponse.CassetteStatuses.Length > 
                        m_CassetteNumber,
                        "recyclingCassetteStatusResponse.CassetteStatuses.Length > " +
                        "m_CassetteNumber");

                    var billTypeId = 
                        recyclingCassetteStatusResponse.CassetteStatuses[
                        m_CassetteNumber].BillType;
                    Debug.Assert(m_Device.m_BillTable != null,
                        "m_Device.m_BillTable != null");
                    Helper.GetBillTypeByBillTypeId(
                        billTypeId, m_Device.m_BillTable, out billType);
                    ret = true;
                }
                return ret;
            }

            public bool GetNumberOfBills(out int numberOfBills)
            {
                bool ret = false;
                numberOfBills = 0;

                CassetteStatus cassetteStatus;
                if (this.GetCasseteStatus(out cassetteStatus) == true)
                {
                    Debug.Assert(cassetteStatus != null,
                        "cassetteStatus != null");
                    numberOfBills = cassetteStatus.NumberOfBillsInCassette;
                    ret = true;

                }
                return ret;
            }
            public bool IsFull(out bool isFull)
            {
                bool ret = false;
                isFull = false;

                CassetteStatus cassetteStatus;
                if (this.GetCasseteStatus(out cassetteStatus) == true)
                {
                    Debug.Assert(cassetteStatus != null, "cassetteStatus != null");
                    isFull = cassetteStatus.CassetteFull;
                    ret = true;
                }
                return ret;
            }
        }
    }
}
