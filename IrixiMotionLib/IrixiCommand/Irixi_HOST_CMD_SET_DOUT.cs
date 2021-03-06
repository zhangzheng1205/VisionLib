﻿using Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.MotionCards.IrixiCommand
{
    public class Irixi_HOST_CMD_SET_DOUT : ZigBeePackage
    {
        public Irixi_HOST_CMD_SET_DOUT()
        {
            FrameLength = 0x06;
        }
        protected override void WriteData()
        {
            writer.Write((byte)Enumcmd.HOST_CMD_SET_DOUT);
            writer.Write(GPIOChannel);
            writer.Write(GPIOState);
        }
        public byte GPIOChannel { get; set; }
        public byte GPIOState { get; set; }


    }
}
