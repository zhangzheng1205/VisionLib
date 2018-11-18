﻿
using JPT_TosaTest.Classes;
using JPT_TosaTest.Config.SoftwareManager;
using JPT_TosaTest.Model.ToolData;
using JPT_TosaTest.Vision;
using JPT_TosaTest.Vision.ProcessStep;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JPT_TosaTest.WorkFlow
{
    public enum STEP : int
    {
        Init,
        CmdGetProductPLC,
        CmdGetProductSupport,
        CmdFindLine,
        DO_NOTHING,
       
        EXIT,
    }
    public enum EnumProductType
    {
        SUPPORT,
        PLC,
    }
    public class WorkService : WorkFlowBase
    {
        #region Private
        //start from 0
        private const int AXIS_X = 3, AXIS_Y1 = 1, AXIS_Y2 = 2, AXIS_Z = 0, AXIS_CY = 5, AXIS_CZ = 4, AXIS_R = 6;
        private const int PT_X = 0, PT_Y1 = 1, PT_Y2 = 2, PT_Z = 3, PT_R = 4, PT_CY = 5, PT_CZ = 6;
        private const int VAC_PLC = 0,VAC_HSG=2,TouchSensor=6;
        private MotionCards.IMotion  MotionCard=null;
        private IOCards.IIO IOCard = null;
        private object Hom_2D_Hsg=null, ModelPos_Hsg=null, Hom_2D_Tia = null, ModelPos_Tia = null;
        private List<object> TopLines = null;
        private List<object> BottomLines = null;
        private object TiaFlag = null;
        private string File_ToolParaPath = $"{FileHelper.GetCurFilePathString()}VisionData\\ToolData\\";
        private string File_ModelFilePath = $"VisionData\\Model\\";    //Model
       
        private int[] ProductIndex = { 0, 0 };

        private List<double> PTCamTop_Support = null;
        List<double> PtCamBottom_Support = null;
        List<double> PtCamTop_PLC = null;
        List<double> PtCamBottom_PLC = null;
        List<double> PtLeftTop_PLC = null;
        List<double> PtRightDown_PLC = null;
        List<double> PtDropDown_PLC = null;

        List<double> PtLeftTop_Support = null;
        List<double> PtRightDown_Support = null;
        List<double> PtDropDown_Support = null;
        Task GrabTask = null;
        private object MonitorLock = new object();


        //Tool
        private StepFindModel Tool_StepFindHsgModel = null;
        private StepFindeLineByModel Tool_StepFindLineBottomByModel=null;
        private StepFindeLineByModel Tool_StepFindLineTopByModel = null;
        private StepShowLineTop Tool_ShowLineTop = null;
        private StepShowLineBottom Tool_ShowLineBottom = null;
        private StepCalibImage Tool_CalibImage = null;

        #endregion

        protected override bool UserInit()
        {
            MotionCard = MotionCards.MotionMgr.Instance.FindMotionCardByCardName("Motion_IrixiEE0017[0]");
            IOCard = IOCards.IOCardMgr.Instance.FindIOCardByCardName("IO_IrixiEE0017[0]");
           
            return GetAllPoint() && MotionCard !=null  &&  IOCard!=null;
        }
        public WorkService(WorkFlowConfig cfg) : base(cfg)
        {

        }
        protected override int WorkFlow()
        {
            ClearAllStep();
            PushStep(STEP.Init);
            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(10);
                Step = PeekStep();
                if (bPause || Step==null)
                    continue;             
             
                switch (Step)
                {
                    case STEP.Init:     //初始化
                        ShowInfo();
                        HomeAll();
                        ClearAllStep();
                        break;
                    case STEP.CmdGetProductSupport: //抓取Support
                        {
                            Int32 Flag = ((Int32)CmdPara >> 16) & 0xFFFF;
                            if ((Flag >> (ProductIndex[0]++) & 0x01) != 0)
                            {
                                GetProduct(ProductIndex[0], EnumProductType.SUPPORT);
                                if (ProductIndex[0] >= 6)
                                    ProductIndex[0] = 0;
                                ClearAllStep();
                            }
                        }
                        break;
                    case STEP.CmdGetProductPLC:    //抓取PLC
                        {
                            Int32 Flag = (Int32)CmdPara & 0xFFFF;
                            if (Flag == 0)  
                                PopAndPushStep(STEP.EXIT);

                            if ((Flag >> (ProductIndex[1]++) & 0x01) != 0)
                            {
                                GetProduct(ProductIndex[1], EnumProductType.PLC);
                                if (ProductIndex[1] >= 6)
                                    ProductIndex[1] = 0;
                                ClearAllStep();
                            }
                        }
                        break;
                    
                    case STEP.CmdFindLine:  //FindLine
                        lock (MonitorLock)
                        {
                            FindAndGetModelData();
                            Thread.Sleep(1000);
                        }
                        ClearAllStep();
                        break;
                   

                    case STEP.EXIT:
                        return 0;
                }
            }
            return 0;
        }

        #region Private
        private void HomeAll()
        {
            int nStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0:
                        MotionCard.Home(AXIS_CZ, 0, 500, 5, 10);
                        MotionCard.Home(AXIS_Z, 0, 500, 20, 50);
                        IOCard.WriteIoOutBit(TouchSensor, true);
                        nStep = 1;
                        break;
                    case 1:
                        if (MotionCard.IsHomeStop(AXIS_CZ) && MotionCard.IsHomeStop(AXIS_Z))
                        {
                            nStep = 2;
                        }
                        break;
                    case 2:
                        MotionCard.Home(AXIS_X, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_Y2, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_Y1, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_CY, 0, 500, 5, 10);
                        nStep = 3;
                        break;
                    case 3:
                        if (MotionCard.IsHomeStop(AXIS_X) && MotionCard.IsHomeStop(AXIS_Y1) && MotionCard.IsHomeStop(AXIS_Y2) && MotionCard.IsHomeStop(AXIS_CY))
                        {
                            nStep = 4;
                        }
                        break;
                    case 4:
                        ShowInfo("HomeOk");
                        return;
                    default:
                        return;

                }
            }
        }

        private void GetProduct(int Index, EnumProductType ProductType)
        {
            if (!GetAllPoint())
            {
                ShowError("获取点位出现错误");
                return;
            }
            if (Index < 1 || Index > 6)
                return;

            double DeltaX = 0.0f;  //DeltaX
            double TargetX = 0; //每次的吸取位置X
            double TargetY1 = 0;    //每次的吸取位置Y
            List<double> PtLeftTop = null;
            List<double> PtRightDown = null;
            List<double> PtDropDown = null;


            if (ProductType == EnumProductType.PLC)
            {
                PtLeftTop = PtLeftTop_PLC;
                PtRightDown = PtRightDown_PLC;
                PtDropDown = PtDropDown_PLC;
               
            }
            else
            {
                PtLeftTop = PtLeftTop_Support;
                PtRightDown = PtRightDown_Support;
                PtDropDown = PtDropDown_Support;
            }

            //2行3列

            DeltaX = (PtRightDown[PT_X] - PtLeftTop[PT_X]) / 2;
            if (Index >= 1 && Index <= 3)
            {
                TargetX= PtLeftTop[PT_X] + DeltaX * (Index - 1);
                TargetY1 =  PtLeftTop[PT_Y1] ;
            }
            else
            {
                TargetX = PtLeftTop[PT_X] + DeltaX * (Index - 4);
                TargetY1 = PtRightDown[PT_Y1];
            }
            
            int nStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0: //先将Z升起防止干涉
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, 0);
                        IOCard.WriteIoOutBit(VAC_PLC, false);
                        nStep = 1;
                        break;
                    case 1: //移动PT_X到取料点
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.MoveAbs(AXIS_X, 500, 100, TargetX);
                            nStep = 2;
                        }
                        break;
                    case 2: // PT_Y1到取料点
                        if (MotionCard.IsNormalStop(AXIS_X))
                        {
                            MotionCard.MoveAbs(AXIS_Y1, 500, 100, TargetY1);
                            nStep = 3;
                        }
                        break;
                    case 3:
                        if (MotionCard.IsNormalStop(AXIS_Y1))
                        {
                            nStep = 4;
                        }
                        break;
                    case 4: //下降PT_Z轴
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, PtLeftTop[PT_Z]);
                        nStep = 5;
                        break;
                    case 5:
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {

                            nStep = 6;
                        }
                        break;
                    case 6: //吸真空
                        IOCard.WriteIoOutBit(VAC_PLC, true);
                        nStep = 7;
                        break;    
                     case 7: //PT_Z轴抬起
                        Thread.Sleep(500);
                        MotionCard.MoveAbs(AXIS_Z,500,100,0);
                        nStep = 8;
                        break;
                    case 8:
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.MoveAbs(AXIS_X,500,100, PtDropDown[PT_X]);
                            nStep = 9;
                        }
                        break;

                    case 9: //移动PT_Y2过去
                        if (MotionCard.IsNormalStop(AXIS_X))
                        {
                            MotionCard.MoveAbs(AXIS_Y2, 500, 100, PtDropDown[PT_Y2]);
                            nStep = 10;
                        }
                        break;

                    case 10: //移动到放置点的PT_X位置并下降PT_Z到HSG表面，不要下去
                        if (MotionCard.IsNormalStop(AXIS_Y2))
                        {
                            MotionCard.MoveAbs(AXIS_Z, 500, 100, PtDropDown[PT_Z]);
                            nStep = 11;
                        }
                        break;
                    case 11: //
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            if (ProductType == EnumProductType.PLC)
                            {
                                PopAndPushStep(STEP.CmdGetProductPLC);
                                Work_PLC();
                            }
                            else
                            {
                                PopAndPushStep(STEP.CmdGetProductSupport);
                                Work_Support();
                            }
                            nStep = 12;
                        }
                        break;
                    case 12:    //等待调整位置
                        
                        return;
                    default:
                        return;
                }
            }

        }

        /// <summary>
        /// 找模板
        /// </summary>
        private void FindAndGetModelData()
        {
            int nStep = 0;
            ShowInfo("正在标记......");
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0: //移动CY
                        IOCard.WriteIoOutBit(VAC_HSG, true);
                        MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamBottom_Support[PT_CY]);
                        nStep = 1;
                        break;
                    case 1: //CZ到下表面
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamBottom_Support[PT_CZ]);
                            nStep = 2;
                           
                        }
                        break;
                   
                    case 2: //开始寻找模板
                        if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            Thread.Sleep(200);
                            HalconVision.Instance.GrabImage(0);

                            //找Hsg
                            string ModelFulllPathFileName = $"{File_ModelFilePath}{Config.ConfigMgr.Instance.ProcessData.HsgModelName}.shm";
                            Tool_StepFindHsgModel = new StepFindModel()
                            {
                                In_CamID = 0,
                                In_ModelNameFullPath = ModelFulllPathFileName
                            };
                            HalconVision.Instance.ProcessImage(Tool_StepFindHsgModel);
                            nStep = 3;
                        }
                        break;
                    case 3: //模板找到以后开始找下表面线
                        {
                            FindLineBottom();
                            MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamTop_PLC[PT_CZ]);       
                            nStep = 4;   
                        }
                        break;
                    case 4:
                        if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            {
                                MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamTop_PLC[PT_CY]);
                                nStep = 5;
                            }
                        }
                        break;
             
                    case 5: //升到上表面寻找上表面的线
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            Thread.Sleep(200);
                            HalconVision.Instance.GrabImage(0);
                            FindLineTop();
                            nStep = 6;                       
                        }
                        break;
                    case 6: // 完毕,等待工作
                        ShowInfo("标记完毕");
                        return;
                    default:
                        return;
                }
            }
            ShowInfo("标记被终止");
        }

        //顶部监视
        private void Work_PLC()
        {
            ShowInfo("Lens......");
            int nStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {        
                    case 0: //CZ到上表面    
                        StartMonitor(0);
                        MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamTop_PLC[PT_CZ]);
                        nStep = 1;
                        break;
                    case 1: //移动CY
                    if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamTop_PLC[PT_CY]);
                            nStep = 2;
                        }
                        break;
                    case 2: 
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            Thread.Sleep(200);
                            nStep = 3;
                        }
                        break;
                    case 3: //开始拍照并显示
                        StartMonitor(0);
                        var LineList = new List<Tuple<double, double, double, double>>();
                        foreach (var it in Tool_StepFindLineTopByModel.Out_Lines)
                            LineList.Add(new Tuple<double, double, double, double>(it.Item1.D, it.Item2.D, it.Item3.D, it.Item4.D));
                        Tool_ShowLineTop = new StepShowLineTop()
                        {
                            In_CamID = 0,
                            In_PixGainFactor = Tool_CalibImage.Out_PixGainFactor,
                            Line1 = LineList[0],
                            Line2 = LineList[1]
                        };
                        HalconVision.Instance.ProcessImage(Tool_ShowLineTop);

                        //同时显示Tia的Region
                        HalconVision.Instance.ShowRoi(0, TiaFlag);
                        if(GetCurStepCount()==0)
                            nStep = 4;
                        break;
                    case 4: // 完毕,等待工作完毕
                        IOCard.WriteIoOutBit(VAC_HSG, false);
                        BackToTempPos();
                        ShowInfo("Lens完毕");
                        return;
                    default:
                        return;
                }
            }
            ShowInfo("Lens被终止");
        }

        //底部
        private void Work_Support()
        {
            int nStep = 0;
            ShowInfo("Pad......");
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0: //移动CY
                        TiaFlag = null;
                        MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamBottom_Support[PT_CY]);
                        nStep = 1;
                        break;
                    case 1: //CZ到下表面
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamBottom_Support[PT_CZ]);
                            
                            nStep = 2;
                        }
                        break;
                    case 2: //开始拍照
                        if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            Thread.Sleep(200);//等待稳定
                            nStep = 3;
                        }
                        break;
                    case 3:
                        //HalconVision.Instance.GrabImage(0, true, true);
                        StartMonitor(0);
                        var LineList = new List<Tuple<double, double, double, double>>();
                        foreach (var it in Tool_StepFindLineBottomByModel.Out_Lines)
                            LineList.Add(new Tuple<double, double, double, double>(it.Item1.D, it.Item2.D, it.Item3.D, it.Item4.D));
                        Tool_ShowLineBottom = new StepShowLineBottom()
                        {
                            In_CamID = 0,
                            In_PixGainFactor = Tool_CalibImage.Out_PixGainFactor,
                            In_Lines= LineList
                        };
                        HalconVision.Instance.ProcessImage(Tool_ShowLineBottom);
                        if (GetCurStepCount() == 0)
                            nStep = 4;
                        break;
                    case 4: //去找Tia的Model和基准线，并画出region
                        if (TiaFlag == null)
                            FindLineTia();
                        else
                            HalconVision.Instance.ShowRoi(0, TiaFlag);

                         nStep = 5;
                        break;
                    case 5: //

                        nStep = 6;
                        break;
                    case 6:

                        nStep = 10;
                        break;
                    case 10: // 完毕,等待工作完毕
                        BackToTempPos();
                        ShowInfo("Pad完毕");
                        return;
                    default:
                        return;
                }
            }
            ShowInfo("Pad被终止");
        }
        #endregion

       
        private void BackToTempPos()
        {
            int nStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0: //Z轴抬起
                        IOCard.WriteIoOutBit(VAC_PLC, false);
                        Thread.Sleep(200);
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, 0);
                        nStep = 1;
                        break;
                    case 1: //X轴回到120的固定位置
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.MoveAbs(AXIS_X, 500, 100, 120);
                            nStep = 2;
                        }
                        break;
                    case 2:
                        return;
                }
            }
        }
         
   
        /// <summary>
        /// 找Bottom的基线
        /// </summary>
        /// <returns></returns>
        private bool FindLineBottom()
        {
            string ModelName = Config.ConfigMgr.Instance.ProcessData.HsgModelName;
            List<string> LineList = new List<string>();
            var fileList = FileHelper.GetProfileList(File_ToolParaPath);
            //既包含Pair也包含Line
            foreach (var file in fileList)
            {
                if (file.Contains("Bottom"))
                {
                    string text = File.ReadAllText($"{File_ToolParaPath}{file}.para");
                    LineList.Add(text);
                }
            }
            Tool_StepFindLineBottomByModel = new StepFindeLineByModel()
            {
                In_CamID = 0,
                In_ModelRow = Tool_StepFindHsgModel.Out_ModelRow,
                In_ModelCOl = Tool_StepFindHsgModel.Out_ModelCol,
                In_ModelPhi = Tool_StepFindHsgModel.Out_ModelPhi,
                In_LineRoiPara = LineList
            };
            HalconVision.Instance.ProcessImage(Tool_StepFindLineBottomByModel);
            //BottomLines = new List<object>();
            foreach (var it in Tool_StepFindLineBottomByModel.Out_Lines)
            {
                BottomLines.Add(it);
            }
            return true;
        }

        /// <summary>
        /// 找Top的基线
        /// </summary>
        /// <returns></returns>
        private bool FindLineTop()
        {
            string ModelName = Config.ConfigMgr.Instance.ProcessData.HsgModelName;
            List<string> LineList = new List<string>();
            var fileList = FileHelper.GetProfileList(File_ToolParaPath);
            foreach (var file in fileList)
            {
                if (file.Contains("Bottom"))
                {
                    string text = File.ReadAllText($"{File_ToolParaPath}{file}.para");
                    var l1 = text.Split('|');
                    var l2 = l1[1].Split('&');
                    string name = l2[l2.Length - 1];
                    if (!string.IsNullOrEmpty(ModelName) && name == ModelName)
                    {
                        LineList.Add(text);
                    }
                }
            }

            Tool_StepFindLineTopByModel = new StepFindeLineByModel()
            {
                In_CamID = 0,
                In_LineRoiPara = LineList,
                In_Hom_mat2D = Tool_StepFindHsgModel.Out_Hom_mat2D,
                In_ModelRow = Tool_StepFindHsgModel.Out_ModelRow,
                In_ModelCOl = Tool_StepFindHsgModel.Out_ModelCol,
                In_ModelPhi = Tool_StepFindHsgModel.Out_ModelPhi
            };
            HalconVision.Instance.ProcessImage(Tool_StepFindLineTopByModel);


            //找线
            var LinesForCalib = new List<Tuple<double, double, double, double>>();
            foreach (var it in Tool_StepFindLineTopByModel.Out_Lines)
            {
                TopLines.Add(it);
            }
            Tool_CalibImage = new StepCalibImage()
            {
                In_CamID = 0,
                In_RealDistance = 6600,
                In_Line1 = LinesForCalib[0],
                In_Line2 = LinesForCalib[1]
            };
            //顺便将图像尺寸标定了
            HalconVision.Instance.ProcessImage(Tool_CalibImage);
            return true;
        }

        /// <summary>
        /// 找Tia的基准线
        /// </summary>
        /// <param name="ModelName">只需要名称，不需要路径</param>
        /// <param name="lineList"></param>
        /// <returns></returns>
        private bool FindLineTia()
        {
            //找Model
            string ModelFulllPathFileName = $"{File_ModelFilePath}{Config.ConfigMgr.Instance.ProcessData.HsgModelName}.shm";
            StepFindModel FindTiaModelStep = new StepFindModel()
            {
                In_CamID = 0,
                In_ModelNameFullPath = ModelFulllPathFileName
            };
            HalconVision.Instance.ProcessImage(FindTiaModelStep);

            //准备Tia的FlagData,读取L1与L2
            List<string> LineList = new List<string>();
            FlagToolDaga FlagData = new FlagToolDaga();
            FlagData.FromString(File.ReadAllText(File_ToolParaPath + "Tia.para"));
            LineList.Add(File.ReadAllText(File_ToolParaPath + FlagData.L1Name + ".para"));
            LineList.Add(File.ReadAllText(File_ToolParaPath + FlagData.L2Name + ".para"));

            //找线
            StepFindeLineByModel FindTiaLine = new StepFindeLineByModel()
            {
                In_CamID = 0,
                In_Hom_mat2D = FindTiaModelStep.Out_Hom_mat2D,
                In_ModelRow = FindTiaModelStep.Out_ModelRow,
                In_ModelCOl = FindTiaModelStep.Out_ModelCol,
                In_ModelPhi = FindTiaModelStep.Out_ModelPhi,
                In_LineRoiPara = LineList
            };
            HalconVision.Instance.ProcessImage(FindTiaLine);

            //显示region
            //定义两条直线
            string RegionFullPathFileName = $"{File_ToolParaPath}Flag.reg";
            List<Tuple<double, double, double, double>> TiaLineList = new List<Tuple<double, double, double, double>>();
            foreach (var it in FindTiaLine.Out_Lines)
                TiaLineList.Add(new Tuple<double, double, double, double>(it.Item1.D, it.Item2.D, it.Item3.D, it.Item4.D));

            StepShowFlag ShowFlagStep = new StepShowFlag()
            {
                In_CamID = 0,
                In_CenterRow = FlagData.Halcon_Row,
                In_CenterCol = FlagData.Halcon_Col,
                In_Phi = FlagData.Halcon_Phi,
                In_HLine = TiaLineList[0],
                In_VLine = TiaLineList[1],
                In_RegionFullPathFileName = RegionFullPathFileName
            };
            HalconVision.Instance.ProcessImage(ShowFlagStep);

            TiaFlag = ShowFlagStep.Out_Region;
            return true;
        }

        private List<string> GetToolRoiDataByFileName(string ModelName,string SubString, int[] IndexList=null)
        {
            List<string> RoiDataList = new List<string>();
            var fileList = FileHelper.GetProfileList(File_ToolParaPath);
            string ExpectedName = "";
            if (IndexList == null)
                ExpectedName = SubString;
            foreach (var file in fileList)
            {
                string text = File.ReadAllText($"{File_ToolParaPath}{file}.para");
                var L1 = text.Split('|');
                var L2 = L1[1].Split('&');
                string modelName = L2[L2.Length - 1];
                if (!string.IsNullOrEmpty(modelName) && modelName == ModelName)
                {
                    RoiDataList.Add(text);
                }

            }
            return RoiDataList;
        }

      




        private bool GetAllPoint()
        {
            PTCamTop_Support = WorkFlowMgr.Instance.GetPoint("Pad相机顶部位置");
            PtCamBottom_Support = WorkFlowMgr.Instance.GetPoint("Pad相机底部位置");
            PtCamTop_PLC = WorkFlowMgr.Instance.GetPoint("Lens相机顶部位置");
            PtCamBottom_PLC = WorkFlowMgr.Instance.GetPoint("Lens相机底部位置");

            PtLeftTop_PLC = WorkFlowMgr.Instance.GetPoint("PLC左上吸取点");
            PtRightDown_PLC = WorkFlowMgr.Instance.GetPoint("PLC右下吸取点");
            PtDropDown_PLC = WorkFlowMgr.Instance.GetPoint("PLC放置点");

            PtLeftTop_Support = WorkFlowMgr.Instance.GetPoint("Support左上吸取点");
            PtRightDown_Support = WorkFlowMgr.Instance.GetPoint("Support右下吸取点");
            PtDropDown_Support = WorkFlowMgr.Instance.GetPoint("Support放置点");


            return  PTCamTop_Support != null &&
                    PtCamBottom_Support != null &&
                    PtCamTop_PLC != null &&
                    PtCamBottom_PLC != null &&
                    PtLeftTop_PLC != null &&
                    PtRightDown_PLC != null &&
                    PtDropDown_PLC != null &&
                    PtLeftTop_Support!=null &&
                    PtRightDown_Support!=null &&
                    PtDropDown_Support!=null;
        }
        private void StartMonitor(int nCamID)
        {
            if (GrabTask == null || GrabTask.IsCanceled || GrabTask.IsCompleted)
            {
                GrabTask = new Task(()=> {
                    while (!cts.IsCancellationRequested)
                    {
                        lock (MonitorLock)
                        {
                            HalconVision.Instance.GrabImage(nCamID,true,true);
                        }
                    }
                },cts.Token);
                GrabTask.Start();
            }
        }
    }
}
