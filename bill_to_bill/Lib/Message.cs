using System;
using System.IO.Ports;
using System.Diagnostics;
using System.Collections.Generic;

namespace Lucker.Devices
{
    public partial class BillToBillDevice
    {
        public abstract class MessageBase
        {
            readonly InterfaceAddress m_InterfaceAddr;

            public InterfaceAddress InterfaceAddr
            {
                get
                {
                    return m_InterfaceAddr;
                }
            }
            
            public MessageBase(InterfaceAddress interfaceAddr)
            {
                m_InterfaceAddr = interfaceAddr;
            }

            protected virtual ushort CalculateCRC(byte[] data)
            {
                ushort crc = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    crc = Helper.GetByteCrc(data[i], crc);
                }
                return crc;
            }

            public abstract byte GetMessageLength();
            public abstract byte[] GetSendingData();

        }


        public class MessageCmd : MessageBase
        {
            readonly InterfaceCommand m_InterfaceCmd;
            readonly byte DATA_LENGTH = 0x06;
            readonly byte[] m_Data;

            #region props

            public InterfaceCommand InterfaceCmd
            {
                get
                {
                    return m_InterfaceCmd;
                }
            }
            #endregion

            public MessageCmd(InterfaceAddress interfaceAddr,
                InterfaceCommand interfaceCommand, byte[] data)
                : base(interfaceAddr)
            {
                m_InterfaceCmd = interfaceCommand;
                m_Data = data;
            }



            public override byte GetMessageLength()
            {
                return m_Data == null ?
                    DATA_LENGTH : (byte)(DATA_LENGTH + m_Data.Length);
            }
            public override byte[] GetSendingData()
            {
                List<byte> ret = new List<byte>();
                //main data
                ret.AddRange(new byte[] { 
					SYNC,(byte)base.InterfaceAddr,GetMessageLength(),
					(byte)m_InterfaceCmd});
                //if data exists, add it
                if (m_Data != null)
                    ret.AddRange(m_Data);
                //calculate crc
                var crc = CalculateCRC(ret.ToArray());
                //add crc at data
                ret.Add((byte)crc);
                ret.Add((byte)(crc >> 8));

                return ret.ToArray();

            }

        }
        /*public class BVMessagePoll : BVMessageBase()
        {
        }*/
        public class MessageAck : MessageBase
        {
            readonly byte DATA_LENGTH = 6;


            public MessageAck(InterfaceAddress interfaceAddr)
                : base(interfaceAddr)
            {
            }

            public override byte GetMessageLength()
            {
                return DATA_LENGTH;
            }

            public override byte[] GetSendingData()
            {
                List<byte> data = new List<byte>();
                //get data
                data.AddRange(
                    new byte[] { SYNC, (byte)base.InterfaceAddr, GetMessageLength() });
                data.Add(0x00);

                //get crc
                var crc = base.CalculateCRC(data.ToArray());
                //store in data
                data.Add((byte)crc);
                data.Add((byte)(crc >> 8));

                //Debug.WriteLine("Ack {0:X},{1:X},{2:X},{3:X},{4:X},{5:X}",
                //data[0],data[1],data[2],data[3],data[4],data[5]);

                return data.ToArray();
            }

        }
    }
}

