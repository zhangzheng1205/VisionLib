﻿using Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.MotionCards.IrixiCommand
{
    public class Irixi_Home : ZigBeePackage
    {
        protected override void WriteData()
        {
            writer.Write((byte)Enumcmd.Home);
            writer.Write(AxisNo);
            writer.Write(AccStep);
            writer.Write(LSpeed);
            writer.Write(HSpeed);

        }
        public override ZigBeePackage ByteArrToPackage(byte[] RawData)
        {
            return base.ByteArrToPackage(RawData);
        }
        public byte AxisNo { get; set; }
        public UInt16 AccStep { get; set; }
        public byte LSpeed { get; set; }
        public byte HSpeed { get; set; }

    }
}
