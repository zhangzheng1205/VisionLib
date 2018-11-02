﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using JPT_TosaTest.Config.SoftwareManager;
using JPT_TosaTest.Config.SystemCfgManager;

namespace JPT_TosaTest.WorkFlow
{
    public delegate void StationInfoHandler(int Index, string StationName, string Msg);
    public class WorkFlowBase
    {
        public bool Enable;
        public string StationName;
        public int StationIndex;
        protected bool bPause = false;
        public event StationInfoHandler OnStationInfoChanged;
        protected WorkFlowConfig cfg = null;
        protected CancellationTokenSource cts =new CancellationTokenSource();
        protected Stack<object> nStepStack=new Stack<object>();
        protected Task t = null; 
        protected object Step { get; set; }
        private object _lock = new object();
        protected object PeekStep()
        {
            try
            {
                lock (_lock)
                {
                    return nStepStack.Peek();
                }
            }
            catch
            {
                return null;
            }

        }
        protected void PushStep(object Step) {
            lock (_lock)
            {
                nStepStack.Push(Step);
            }
        }
        protected void PopAndPushStep(object Step)
        {
            lock (_lock)
            {
                nStepStack.Pop();
                nStepStack.Push(Step);
            }
        }
        protected void PushBatchStep(object[] nSteps)
        {
            foreach (var step in nSteps)
                nStepStack.Push(step);
        }
        protected void PopStep()
        {
            lock (_lock)
            {
                nStepStack.Pop();
            }
        }
        protected void ClearAllStep()
        {
            lock (_lock)
            {
                nStepStack.Clear();
            }
        }
        protected int GetCurStepCount()
        {
            lock (_lock)
            {
                return nStepStack.Count;
            }
        }
        protected virtual bool UserInit() { return true; }
        public WorkFlowBase(WorkFlowConfig cfg) { this.cfg = cfg; }
        public void ShowInfo(string strInfo=null)    //int msg, int iPara, object lParam
        {
            if (strInfo == null || strInfo.Trim().ToString() == "")
                strInfo = Step.ToString();
            DateTime dt = DateTime.Now;
            OnStationInfoChanged?.Invoke(StationIndex, cfg.Name, string.Format("{0:D2}:{1:D2}:{2:D2}  {3:D2}", dt.Hour, dt.Minute, dt.Second, strInfo));
        }
        public bool Start()
        {
            bPause = false;
            if (!UserInit())
            {
                return false;
            }
            else if (t==null || t.Status == TaskStatus.Canceled || t.Status == TaskStatus.RanToCompletion)
            {
                cts = new CancellationTokenSource();
                t = new Task(() => ThreadFunc(this), cts.Token);
                t.Start();
            }
            return true;
        }
        public bool Stop()
        {
            cts.Cancel();
            return true;
        }
        public bool Pause()
        {
            this.bPause = true;
            return true;
        }
        private static int ThreadFunc(object o) { return (o as WorkFlowBase).WorkFlow(); }
        protected virtual int WorkFlow() { return 0; }
        public void WaitComplete()
        {
            //if (t != null)
            //    t.Wait(5000);
        }
        protected SystemParaModel SysPara = null;
        protected void ShowError(string ErrorMsg)
        {
            Messenger.Default.Send<string>(ErrorMsg, "Error");
        }
        public void SetCmd(STEP step)
        {
            PushStep(step);
        }
    }
}
