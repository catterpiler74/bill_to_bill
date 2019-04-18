using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Lucker.Devices
{
    public partial class BillToBillDevice
    {
        public class Response
        {
            public static bool CheckDataPacket(byte[] packetData)
            {
                bool ret = false;
                Debug.Assert(packetData != null,"packetData != null");
                var packetDataLength = packetData.Length;

                if (packetDataLength >= RESPONSE_PACKET_MIN_LEN)
                {
                    /*byte[] packetDataWithoutCrc
                        Array.Copy(*/
                    var crc = Helper.CalculateCrcFullPacket(packetData);
                    var crcFirstByte = (byte)(crc & 0xFF);
                    var crcSecondByte = (byte)(crc >> 8);

                    if (packetData[SYNC_CODE_OFFSET] == SYNC &&
                        packetData[INTERFACE_CODE_OFFSET] == (byte)InterfaceAddress.B2B &&
                        packetData[PACKET_LENGTH_OFFSET] == packetData.Length &&
                        packetData[packetDataLength - 2] == crcFirstByte &&
                        packetData[packetDataLength - 1] == crcSecondByte)
                    {
                        ret = true;
                    }   
                }

                return ret;
            }
            /*const int PACKET_MIN_LENGTH = 6;

            readonly int m_PacketLength;
            readonly byte[] m_PacketData;

            public byte[] PacketData
            {
                get
                {
                    return m_PacketData;
                }
            }
            public Response(int packetLength, byte[] data)
            {
                m_PacketLength = packetLength;
                m_PacketData = data;
            }
            public Response(byte[] packetByteArr)
            {
                Debug.Assert(packetByteArr != null, "packetByteArr != null");
                Debug.Assert(packetByteArr.Length >= PACKET_MIN_LENGTH,
                             "packetByteArr.Length >= PACKET_MIN_LENGTH");

                m_PacketLength = packetByteArr[2];
                m_PacketData = new byte[packetByteArr.Length - (1 + 1 + 1 + 2)];
                Array.Copy(packetByteArr,1 + 1 + 1,m_PacketData,0,m_PacketData.Length);

            }*/

        }
        public class NakResponse 
        {
            public static bool CheckData(byte[] packetData)
            {
                bool ret = false;
                
                if (//check length
                    packetData.Length == NAK_PACKET_LENGTH &&
                    //check data
                    packetData[DATA_OFFSET] == NAK_RESPONSE_DATA)
                {
                    ret = true;
                }
                return ret;
            }
        }

        public class AckResponse 
        {
            public static bool CheckData(byte[] packetData)
            {
                bool ret = false;

                if (//check length
                    packetData.Length == ACK_PACKET_LENGTH &&
                    //check data
                    packetData[DATA_OFFSET] == ACK_RESPONSE_DATA
                    /*||
                    packetData[DATA_OFFSET] == UNKNOWN_ACK_RESPONSE_DATA*/)
                {
                    ret = true;
                }
                return ret;
            }
        }
        public class GetStatusResponse
        {
            const int GET_STATUS_PACKET_LENGTH = 14; //5 + 9 . packet len(5) + data(9)

            const int DATA_LENGTH = 9;

            const int BILL_TYPE_DATA_LENGTH = 3;
            const int BILL_TYPE_DATA_OFFSET = 0;
            const int BILLS_SECURITY_LEVELS_DATA_LEN = 3;
            const int BILLS_SECURITY_LEVELS_OFFSET = 3;
            const int BILLS_TYPE_ROUTING_DATA_LEN = 3;
            const int BILLS_TYPE_ROUTING_OFFSET = 6;

            byte[] m_BillType, m_BillSecurityLevels, m_BillTypeRouting;

            #region props

            public byte[] BillType
            {
                get { return m_BillType; }
            }
            public byte[] BillSecurityLevels
            {
                get { return m_BillSecurityLevels; }
            }
            public byte[] BillTypeRouting
            {
                get { return m_BillTypeRouting; }
            }

            #endregion

            public static bool CheckData(byte[] packet)
            {
                bool ret = false;
                var packetData = Helper.GetPacketData(packet);
                if (packetData.Length == GET_STATUS_PACKET_LENGTH)
                {
                    ret = true;
                }
                return ret;
            }

            public GetStatusResponse(byte[] billType, byte[] billSecurityLevels,
                byte[] billTypeRouting)
            {
                m_BillType = billType;
                m_BillSecurityLevels = billSecurityLevels;
                m_BillTypeRouting = billTypeRouting;
            }

            public static GetStatusResponse CreateFromData(byte[] packetData)
            {
                var data = Helper.GetPacketData(packetData);
                Debug.Assert(data.Length == DATA_LENGTH,"data.Length == DATA_LENGTH");

                var billType = new byte[BILL_TYPE_DATA_LENGTH];
                Array.Copy(data, billType, BILL_TYPE_DATA_LENGTH);
                var billSecurityLevels = new byte[BILLS_SECURITY_LEVELS_DATA_LEN];
                Array.Copy(data, BILLS_SECURITY_LEVELS_OFFSET, billSecurityLevels, 0,
                    BILLS_SECURITY_LEVELS_DATA_LEN);
                var billTypeRouting = new byte[BILLS_TYPE_ROUTING_DATA_LEN];
                Array.Copy(data, BILLS_TYPE_ROUTING_OFFSET, billTypeRouting, 0,
                    BILLS_TYPE_ROUTING_DATA_LEN);
                return new GetStatusResponse(billType, billSecurityLevels, 
                    billTypeRouting);
            }

        }

        public class ResponsePoll
        {


            const int POLL_PACKET_MIN_LENGTH = 8;
            const int FIRST_CODE_INDEX_WITHIN_POLL_PACKET = 3;
            const int SECOND_CODE_INDEX_WITHIN_POLL_PACKET = 4;
            const int THIRD_CODE_INDEX_WITHIN_POLL_PACKET = 5;

            const int PACKET_HEADER_LENGTH = 3; //synch + interface + packet length
            const int FIRST_CODE_LENGTH = 1;
            const int SECOND_CODE_LENGTH = 1;
            const int PACKET_CRC_LENGTH = 2;


            readonly PollResponseCode m_FirstCode;
            readonly byte m_SecondCode;
            readonly byte[] m_Z3;

            #region props
            public byte SecondCode
            {
                get
                {
                    return m_SecondCode;
                }
            }
            public PollResponseCode FirstCode
            {
                get
                {
                    return m_FirstCode;
                }
            }
            #endregion

            public ResponsePoll(
                PollResponseCode firstCode, byte secondCode,
                byte[] z3)
            {
                m_FirstCode = firstCode;
                m_SecondCode = secondCode;
                m_Z3 = z3;
            }
            public static bool CheckDataPacket(
                byte[] dataPacket)
            {
                bool ret = false;
                //call base class method ( base check )
                if (Response.CheckDataPacket(dataPacket))
                {
                    if (dataPacket.Length >= POLL_PACKET_MIN_LENGTH)
                    {
                        ret = true;
                    }
                }
                return ret;
            }

            //constructs from data
            public static ResponsePoll CreateFromData(byte[] packetData)
            {
                Debug.Assert(packetData != null,"packetData != null");
                Debug.Assert(packetData.Length >= POLL_PACKET_MIN_LENGTH,
                    "packetData.Length >= POLL_PACKET_MIN_LENGTH");
                
                var z3Length = packetData.Length - (
                    PACKET_HEADER_LENGTH + FIRST_CODE_LENGTH + SECOND_CODE_LENGTH + 
                    PACKET_CRC_LENGTH);
                Debug.Assert(z3Length > 0,"z3Length > 0");
                var z3 = new byte[z3Length];
                Array.Copy(packetData,
                    PACKET_HEADER_LENGTH + FIRST_CODE_LENGTH + SECOND_CODE_LENGTH,
                    z3,0,z3Length);

                return new ResponsePoll(
                    (PollResponseCode)packetData[FIRST_CODE_INDEX_WITHIN_POLL_PACKET],
                    packetData[SECOND_CODE_INDEX_WITHIN_POLL_PACKET], z3);

            }
            public override string ToString()
            {
                return string.Format("First - {0:X}, Second - {1:X}",
                                      m_FirstCode, m_SecondCode);
            }
            /*public static bool CheckResponse(byte[] data)
            {
                if(data[3] == 6)
            }*/

        }
        public class GetBillTableResponse
        {
            const int FIRST_DENOMINATION_CODE_OFFSET = 0;
            const int SECOND_DENOMINATION_CODE_OFFSET = 4;
            const int COUNTRY_CODE_OFFSET = 1;
            const int COUNTRY_CODE_SIZE = 3;
            //const int BILL_TYPES_COUNT_IN_RESPONSE_DATA = 24;
            const int ONE_BILL_TYPE_SIZE = 5;

            const int PACKET_DATA_LENGTH = 120;

            readonly BillType[] m_BillTypes;

            public BillType[] BillTypes
            {
                get
                {
                    return m_BillTypes;
                }
            }

            public static bool CheckData(byte[] packet)
            {
                bool ret = false;
                var packetData = Helper.GetPacketData(packet);
                if (packetData.Length == PACKET_DATA_LENGTH)
                {
                    ret = true;
                }
                return ret;
            }

            public static GetBillTableResponse CreateFromData(byte[] billTableData)
            {
                return new GetBillTableResponse(
                    new List<BillType>(ParseBillTableData(
                        Helper.GetPacketData(billTableData))).ToArray());
            }

            static IEnumerable<BillType> ParseBillTableData(byte[] billTableData)
            {
                Debug.Assert(billTableData != null, "billTableData != null");
                Debug.Assert(
                    billTableData.Length % ONE_BILL_TYPE_SIZE == 0,
                    "billTableData.Length % ONE_BILL_TYPE_SIZE == 0");
                var billTypesCountInResponseData =
                    (byte)(billTableData.Length / ONE_BILL_TYPE_SIZE);
                /*Debug.Assert(billTableData.Length ==
                    BILL_TYPES_COUNT_IN_RESPONSE_DATA * ONE_BILL_TYPE_SIZE,
                    "billTableData.Length == " + 
                    "BILL_TYPES_COUNT_IN_RESPONSE_DATA * ONE_BILL_TYPE_SIZE");*/

                for (byte i = 0; i < billTypesCountInResponseData; i++)
                {
                    var second = billTableData[i * 5 + SECOND_DENOMINATION_CODE_OFFSET];
                    //else decimal
                    if (second >> 7 == 0)
                    {
                        //remove 7-th byte
                        var secondCode = (byte)(second & 0x7f);
                        //country code byte array
                        byte[] country = new byte[COUNTRY_CODE_SIZE];
                        //copy country code in temp byte array
                        Array.Copy(billTableData, i * 5 + COUNTRY_CODE_OFFSET,
                            country, 0, country.Length);
                        var firstCode = billTableData[i * 5];

                        if (firstCode != 0 && secondCode != 0)
                        {
                            //create bill type
                            yield return new BillType(
                                i,
                                //first denomination code
                                firstCode,
                                //second denomination code
                                secondCode,
                                //country code
                                Encoding.ASCII.GetString(country));
                        }
                    }
                }
            }
            public GetBillTableResponse(BillType[] billTypes)
            {
                m_BillTypes = billTypes;
            }
        }

        public class CassetteStatus
        {
            readonly bool m_CassettePresent;
            readonly bool m_CassetteFull;
            readonly byte m_BillType;
            readonly byte m_NumberOfBillsInCassette;

            public bool CassettePresent
            {
                get { return m_CassettePresent; }
            }
            public bool CassetteFull
            {
                get { return m_CassetteFull; }
            }
            public byte NumberOfBillsInCassette
            {
                get { return m_NumberOfBillsInCassette; }
            }
            public byte BillType
            {
                get { return m_BillType; } 
            }
            


            public CassetteStatus(
                bool cassettePresent, bool cassetteFull, byte billType,
                byte numberOfBillsInCassette)
            {
                m_CassettePresent = cassettePresent;
                m_CassetteFull = cassetteFull;
                m_BillType = billType;
                m_NumberOfBillsInCassette = numberOfBillsInCassette;
            }
            /*public BillType GetBillType()
            {
                return m_BillType;
            }
            public void SetBillType(BillType billType)
            {

            }*/
        }

        public class GetRecyclingCassetteStatusResponse
        {
            const int DATA_BYTE_MIN_LEN = 32;
            readonly CassetteStatus[] m_CassetteStatuses;

            public CassetteStatus[] CassetteStatuses
            {
                get { return m_CassetteStatuses; }
            }

            public static bool CheckData(byte[] packet)
            {
                bool ret = false;
                var packetData = Helper.GetPacketData(packet);
                if(packetData.Length <= DATA_BYTE_MIN_LEN)
                {
                    ret = true;
                }
                return ret;
            }
            public static GetRecyclingCassetteStatusResponse CreateFromData(
                byte[] packet/*,GetBillTableResponse getBillTableResponse*/)
            {
                var packetData = Helper.GetPacketData(packet);
                var cassetteStatuses = new List<CassetteStatus>(
                    GetCassetteStatusFromData(packetData/*,getBillTableResponse*/)).ToArray();
                return new GetRecyclingCassetteStatusResponse(
                    cassetteStatuses);
            }

            static IEnumerable<CassetteStatus> GetCassetteStatusFromData(
                byte[] packetData/*,GetBillTableResponse getBillTableResponse*/)
            {
                Debug.Assert(packetData != null,"packetData != null");
                Debug.Assert(packetData.Length % 2 == 0,
                    "packetData.Length % 2 == 0");
                //Debug.Assert(getBillTableResponse != null,"response != null");


                for (int i = 0; i < packetData.Length; i+= 2)
                {
                    byte first = packetData[i];
                    byte second = packetData[i + 1];
                    //cassete present (hight bit check);
                    bool cassettePresent = ((first & 0x80) != 0);
                    bool cassetteFull = ((first & 0x40) != 0);
                    byte billTypeId = (byte)(first & 0x0f);
                    byte billCount = second;

                    /*BillType billType;
                    var billTypeFound = 
                        Helper.GetBillTypeByBillTypeId(billTypeId,
                        getBillTableResponse,out billType);*/

                    yield return new CassetteStatus(
                        cassettePresent, cassetteFull, billTypeId, billCount);
                }
            }
            public GetRecyclingCassetteStatusResponse(
                CassetteStatus[] cassetteStatuses)
            {
                m_CassetteStatuses = cassetteStatuses;
            }
        }

        /*public class SetRecyclingCassetteTypeResponse
        {
            public SetRecyclingCassetteTypeResponse()
            {

            }
        }*/

        /*public class BVResponseGetStatus : BVResponse
        {
            readonly int PACKET_LENGTH = 11;

            readonly int m_BillType, m_BillSecurityLevel;

            #region props
            public int BillSecurityLevel
            {
                get
                {
                    return m_BillSecurityLevel;
                }
            }
            public int BillType
            {
                get
                {
                    return m_BillType;
                }
            }
            #endregion

            public BVResponseGetStatus(int packetLength,
                int billType, int billSecurityLevel)
                : base(packetLength, new byte[] {
					(byte)(billType >> 16),(byte)(billType >> 8),
					(byte)billType,(byte)(billSecurityLevel >> 16),
					(byte)(billSecurityLevel >> 8),(byte)billSecurityLevel
				})
            {
                m_BillType = billType;
                m_BillSecurityLevel = billSecurityLevel;
            }
            public BVResponseGetStatus(byte[] packetByteArr) :
                base(packetByteArr)
            {
                Debug.Assert(packetByteArr != null, "packetByteArr != null");
                Debug.Assert(packetByteArr.Length == PACKET_LENGTH,
                             "packetByteArr.Length == PACKET_LENGTH");
                var billTypeByte1 = packetByteArr[3];
                var billTypeByte2 = packetByteArr[4];
                var billTypeByte3 = packetByteArr[5];

                var billSecurityLevel1 = packetByteArr[6];
                var billSecurityLevel2 = packetByteArr[7];
                var billSecurityLevel3 = packetByteArr[8];

                m_BillType = (int)(billTypeByte1 << 16) +
                    (int)(billTypeByte2 << 8) + (int)billTypeByte3;
                m_BillSecurityLevel = (int)(billSecurityLevel1 << 16) +
                    (int)(billSecurityLevel2 << 8) + (int)billSecurityLevel3;
            }
        }*/
    }

}
