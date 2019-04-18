using System;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

namespace Lucker.Devices
{
    public partial class BillToBillDevice
    {
        public static class Helper
        {
            const int PACKET_MIN_LENGTH = 6;
            const int CRC_BYTE_COUNT = 2;

            const int PACKET_HEADER_LENGTH = 3;

            public static ushort GetByteCrc(byte inByte, ushort crc)
            {
                ushort a = 0x8408;
                ushort d = crc, i;
                d ^= inByte;
                for (i = 0; i < 8; i++)
                {
                    if ((d & 0x0001) != 0)
                    {
                        d >>= 1;
                        d ^= a;
                    }
                    else d >>= 1;
                }
                return d;
            }
            public static ushort CalculateCrc(byte[] data)
            {
                ushort crc = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    crc = Helper.GetByteCrc(data[i], crc);
                }
                return crc;
            }

            public static ushort CalculateCrcFullPacket(byte[] dataPacket)
            {
                Debug.Assert(dataPacket.Length >= CRC_BYTE_COUNT,
                    "dataPacket.Length >= CRC_BYTE_COUNT");
                byte[] packetDataWithoutCRC = 
                    new byte[dataPacket.Length - CRC_BYTE_COUNT];
                Array.Copy(dataPacket, packetDataWithoutCRC, 
                    packetDataWithoutCRC.Length);
                return CalculateCrc(packetDataWithoutCRC);
            }
            public static bool CheckResponseData(
                byte[] responseData,InterfaceAddress deviceInterfaceAddr)
            {
                bool ret = false;
                if (responseData.Length >= PACKET_MIN_LENGTH)
                {
                    var synch = responseData[0];
                    var ifaceaAddr = responseData[1];
                    var packetLen = responseData[2];
                    var calcData = new byte[responseData.Length - 2];
                    Array.Copy(responseData, calcData, calcData.Length);
                    var checkCrc = CalculateCrc(calcData);
                    //var preCrcData = new byte[CRC_BYTE_COUNT];
                    var preCrc = new byte[CRC_BYTE_COUNT];
                    Array.Copy(
                        responseData,
                        responseData.Length - CRC_BYTE_COUNT,
                        preCrc, 0, CRC_BYTE_COUNT);
                    var crc = ((short)preCrc[1] << 8) + preCrc[0];

                    //Debug.WriteLine("CheckCrc " + checkCrc);
                    //Debug.WriteLine("My Crc " + crc);

                    if (synch == 0x2 && ifaceaAddr == (byte)deviceInterfaceAddr &&
                        checkCrc == crc && packetLen == responseData.Length)
                        ret = true;

                }
                    //somtimes after reset command
                    //device return wrong ack response, handle it
                else if (responseData.Length == 4)
                {
                    if (responseData[0] == SYNC &&
                        responseData[1] == (byte)deviceInterfaceAddr &&
                        responseData[2] == PACKET_MIN_LENGTH &&
                        responseData[3] == 0x00)
                    {
                        ret = true;
                    }
                }
                return ret;
            }

            public static bool GetAvailableDataFromPort(SerialPort communicationPort, 
                out byte[] data)
            {
                Debug.Assert(communicationPort != null, "communicationPort != null");
                Debug.Assert(communicationPort.IsOpen == true,
                    "serialPort.IsOpen == true");

                bool ret = false;
                data = null;

                var bytesToRead = communicationPort.BytesToRead;
                if (bytesToRead > 0)
                {
                    var buffer = new byte[bytesToRead];
                    //todo
                    var readedBytes = communicationPort.Read(buffer, 0, bytesToRead);
                    Debug.Assert(readedBytes == bytesToRead,
                                 "readedBytes == bytesToRead");
                    data = buffer;
                    ret = true;
                }
                return ret;
            }

            public static bool GetDeviceCmdResponse(SerialPort communicationPort,
                out byte[] responseData)
            {
                bool ret = false;

                responseData = null;
                List<byte> data = new List<byte>();
                //int dataLen = BV_COMMAND_MIN_LENGTH;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                bool stopFlag = false;
                //int emptyCounter = 0;
                do
                {
                    byte[] buffer;
                    bool dataExist = Helper.GetAvailableDataFromPort(
                        communicationPort, out buffer);
                    if (dataExist == true)
                    {
                        data.AddRange(buffer);
                        if (data.Count >= BV_COMMAND_MIN_LENGTH)
                        {
                            //Debug.Assert(data[2] != null,"data[2] != null");
                            if (data.Count >= data[2])
                            {
                                stopFlag = true;
                                responseData = data.ToArray();
                            }
                            else
                            {
                                Thread.Sleep(10);
                                //Console.WriteLine("Inconsistence");
                            }
                        }
                        else
                        {
                            //Console.WriteLine("Buffer unfull ... Sleep");
                            Thread.Sleep(10);
                        }
                    }
                    else
                    {
                        //Debug.WriteLine("Serial port data empty");
                        Thread.Sleep(10);
                        //emptyCounter++;
                    }

                } while (stopFlag == false &&
                           sw.ElapsedMilliseconds < BV_RESPONSE_TIMEOUT_MSEC);
                if (stopFlag == true)
                {
                    ret = true;
                    /*if (Helper.CheckResponseData(data.ToArray(), m_InterfaceAddress) == true)
                    {
                        ret = true;
                    }*/
                }
                return ret;
            }

            public static byte[] GetPacketData(byte[] packet)
            {
                Debug.Assert(packet != null, "packet != null");
                Debug.Assert(
                    packet.Length > PACKET_HEADER_LENGTH + CRC_BYTE_COUNT,
                    "packet.Length > PACKET_HEADER_LENGTH + CRC_BYTE_COUNT");
                var packetDataLength = packet.Length - (PACKET_HEADER_LENGTH + CRC_BYTE_COUNT);
                byte[] packetData = new byte[packetDataLength];
                Array.Copy(packet, PACKET_HEADER_LENGTH, packetData, 0, packetData.Length);
                return packetData;
            }

            public static bool GetBillTypeByBillTypeId(
                byte billTypeId,GetBillTableResponse response,out BillType billType)
            {
                bool ret = false;
                billType = null;
                Debug.Assert(response != null, "response != null");
                Debug.Assert(response.BillTypes != null, "response.BillTypes != null");
                Debug.Assert(billTypeId >= 0,"billTypeId >= 0");

                //find in bill table bill type by id
                billType = Array.Find<BillType>(response.BillTypes,
                    delegate(BillType bt)
                        {
                            return bt.TableIndex == billTypeId;
                        });
                if (billType != null)
                {
                    ret = true;
                }
                return ret;
                
            }
        }
    }
}

