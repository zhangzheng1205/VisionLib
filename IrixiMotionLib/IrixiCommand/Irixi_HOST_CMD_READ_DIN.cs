﻿using Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.MotionCards.IrixiCommand
{
    public class Irixi_HOST_CMD_READ_DIN : ZigBeePackage
    {
        public Irixi_HOST_CMD_READ_DIN()
        {
            FrameLength = 0x04;
        }
        protected override void WriteData()
        {
            writer.Write((byte)Enumcmd.HOST_CMD_READ_DIN);
        }
        public override ZigBeePackage GetDataFromRowByteArr(byte[] RawData)
        {
            base.GetDataFromRowByteArr(RawData);
            ReturnObject = RawData[RawData.Length - 5];
            return this;
        }
    }
}
