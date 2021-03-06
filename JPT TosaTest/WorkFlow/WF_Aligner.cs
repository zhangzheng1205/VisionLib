﻿
using JPT_TosaTest.Config.SoftwareManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JPT_TosaTest.Vision;
using JPT_TosaTest.MotionCards;
using JPT_TosaTest.IOCards;
using JPT_TosaTest.Model;
using M12.Definitions;
using M12.Base;
using M12.Commands.Alignment;
using JPT_TosaTest.WorkFlow.CmdArgs;
using JPT_TosaTest.Config.ProcessParaManager;
using JPT_TosaTest.Classes.WatchDog;

namespace JPT_TosaTest.WorkFlow
{
    public class WF_Aligner : WorkFlowBase
    {
        public enum STEP : int
        {
            Init,
            HomeAll,
            MoveToPreAlignPos,
            DoBlindSearchAlign,
            DoFastAlign1D,

            DO_NOTHING,
            EXIT,
        }


        private Motion_IrixiEE0017 motion = null;
        private IO_IrixiEE0017 io = null;
        private const int AXIS_X = 0, AXIS_Y = 1, AXIS_Z = 2, AXIS_R = 3, AXIS_CX = 4;

        #region PointDefine
        WFPointModel PtInitPostion;
        WFPointModel PtCameraLef;
        WFPointModel PtCameraRight;
        #endregion

        public override bool UserInit()
        {
#if FAKEMOTION
            return true;
#else
            motion = MotionMgr.Instance.FindMotionCardByAxisIndex(1) as Motion_IrixiEE0017;
            io = IOCardMgr.Instance.FindIOCardByCardName("IO_IrixiEE0017[0]") as IO_IrixiEE0017;
            bool bRet = motion != null && io != null && LoadPoint();
            if (!bRet)
                ShowInfo($"初始化失败");
            return bRet;
#endif

        }
        public WF_Aligner(WorkFlowConfig cfg) : base(cfg)
        {

        }
        protected override int WorkFlow()
        {
            try
            {
                ClearAllStep();
                PushStep(STEP.Init);
                while (!cts.IsCancellationRequested)
                {
                    Step = PeekStep();
                    Thread.Sleep(10);
                    if (bPause || Step==null)
                        continue;
                    
                    switch (Step)
                    {
                        case STEP.Init: //初始化
                            HomeAll();
                            MoveToInitPos();
                            PopStep();
                            break;

                        case STEP.HomeAll:  //回原点
                            HomeAll();
                            PopStep();
                            break;
                        case STEP.DoBlindSearchAlign:   //耦合
                            {
                                var para = CmdParaQueue.Dequeue() as CmdAlignArgs;
                                DoBlindSearchAlignment(para);
                                PopStep();
                            }
                            break;
                        case STEP.DoFastAlign1D:

                            break;

                        case STEP.MoveToPreAlignPos:    //预对位
                            {
                                var para = CmdParaQueue.Dequeue() as CmdPreAlignmentArgs;
                                MoveToProAlignPosition(para.AxisNoBaseZero);
                                PopStep();
                            }
                            break;
                        case STEP.EXIT:
                            return 0;
                        default:
                            break;
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                ShowInfo(ex.Message);
                ShowError(ex.Message);
                return -1;
            }
        }

#region Private Method
        /// <summary>
        /// 回原点
        /// </summary>
        private void HomeAll()
        {
            
            nSubStep = 1;
            while (!cts.IsCancellationRequested)
            {
                switch (nSubStep)
                {
                    case 1:
                        ShowInfo("Z轴回原点");
                        motion.Home(AXIS_Z, 0, 500, 2, 5);
                        nSubStep = 2;
                        break;
                    case 2:
                        if (motion.IsHomeStop(AXIS_Z))
                        {
                            ShowInfo("Y轴回原点");
                            motion.Home(AXIS_Y, 0, 500, 2, 5);
                            nSubStep = 3;
                        }
                        break;
                    case 3:
                        if (motion.IsHomeStop(AXIS_Y))
                        {
                            ShowInfo("X,R,CX轴回原点");
                            motion.Home(AXIS_X,0,500,2,5);
                            motion.Home(AXIS_R, 0, 500, 2, 5);
                            motion.Home(AXIS_CX, 0, 500, 10, 30);
                            nSubStep = 4;
                        }
                        break;
                    case 4:
                        if (motion.IsHomeStop(AXIS_X) && motion.IsHomeStop(AXIS_R) && motion.IsHomeStop(AXIS_CX))
                        {
                            ShowInfo("回原点完成");
                            return;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 移动到初始位置
        /// </summary>
        private void MoveToInitPos()
        {
            var dog = new Dog(30000);
            nSubStep = 1;
            while (!cts.IsCancellationRequested)
            {
                dog.CheckTimeOut("移动到初始位置超时");
                switch (nSubStep)
                {
                    case 1:
                        motion.MoveAbs(AXIS_X, 1000, 5, PtInitPostion.X);
                        motion.MoveAbs(AXIS_Y, 1000, 5, PtInitPostion.Y);
                        motion.MoveAbs(AXIS_Z, 1000, 5, PtInitPostion.Z);
                        motion.MoveAbs(AXIS_R, 1000, 5, PtInitPostion.R);
                        motion.MoveAbs(AXIS_CX, 500, 20, PtInitPostion.CX);
                        nSubStep = 2;
                        break;
                    case 2:
                        if (motion.IsNormalStop(AXIS_X) && motion.IsNormalStop(AXIS_Y) &&
                            motion.IsNormalStop(AXIS_Z) && motion.IsNormalStop(AXIS_R) &&
                            motion.IsNormalStop(AXIS_CX))
                            return;
                        break;
                }
            }
        }

        /// <summary>
        /// 耦合
        /// </summary>
        private void DoBlindSearchAlignment(CmdAlignArgs CmdPara)
        {
            var HArgsF = CmdPara.HArgs;
            var VArgsF = CmdPara.VArgs;
            ShowInfo("开始耦合......");
            motion.DoBlindSearch(HArgsF,VArgsF, ADCChannels.CH2, out List<Point3D> Value);
            CmdPara.QResult=Value;
            CmdPara.FireFinishAlimentEvent();
            ShowInfo("耦合完成......");
        }

        /// <summary>
        /// 接触传感器预对位
        /// </summary>
        /// <param name="HAxis"></param>
        private void MoveToProAlignPosition(int HAxisNo)
        {
            var dog = new Dog(60000);
            nSubStep = 1;
            motion.SetCssThreshold(CSSCH.CH1, 500, 1000);
            motion.SetCssEnable(CSSCH.CH1, true);
            ShowInfo("正在预对位");
            while (!cts.IsCancellationRequested)
            {
                dog.CheckTimeOut("预对位超时");
                switch (nSubStep)
                {
                    case 1:
                        ShowInfo("寻找TouchSensor......");
                        motion.MoveAbs(HAxisNo, 1000, 5, 2);
                        nSubStep = 2;
                        break;
                    case 2:
                        if (motion.IsNormalStop(HAxisNo))
                        {
                            ShowInfo("寻找TouchSensor Ok");
                            nSubStep = 3;
                        }
                        break;
                    case 3:
                        ShowInfo("反向中......");
                        motion.MoveRel(HAxisNo, 1000, 5, -0.01);
                        nSubStep = 4;
                        break;
                    case 4:
                        if (motion.IsNormalStop(HAxisNo))
                        {
                            ShowInfo("预对位完成");
                            return;
                        }
                        break;
                    default:
                        break;
                }
            }          
        }


        #endregion

        #region LoadPoint
        protected bool LoadPoint()
        {
            PtInitPostion = WorkFlowMgr.Instance.GetPoint("初始位置");

            return PtInitPostion!=null;
        }
        #endregion

    }
}
