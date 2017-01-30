﻿using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common.Build;
using Cake.Common.Build.TFBuild.Data;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Cake.TFBuild.Module
{
    /// <summary>
    /// Represents a Cake engine for use with the TF Build engine.
    /// </summary>
    public partial class TFBuildEngine : ICakeEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TFBuildEngine"/> type.
        /// </summary>
        /// <param name="log">The log.</param>
        public TFBuildEngine(ICakeLog log)
        {
            _engine = new CakeEngine(log);
            _engine.Setup += BuildSetup;
            _engine.TaskSetup += OnTaskSetup;
            _engine.TaskTeardown += OnTaskTeardown;
            _engine.Teardown += OnBuildTeardown;
        }

        private void OnBuildTeardown(object sender, TeardownEventArgs e)
        {
            var b = e.TeardownContext.BuildSystem();
            if (b.IsRunningOnVSTS || b.IsRunningOnTFS)
            {
                b.TFBuild.Commands.UpdateRecord(_parentRecord, new TFBuildRecordData
                {
                    FinishTime = DateTime.Now,
                    Status = TFBuildTaskStatus.Completed,
                    Result = e.TeardownContext.Successful ? TFBuildTaskResult.Succeeded : TFBuildTaskResult.Failed,
                    Progress = TaskRecords.Count/_engine.Tasks.Count*100,
                });
            }
        }

        private void OnTaskTeardown(object sender, TaskTeardownEventArgs e)
        {
            var b = e.TaskTeardownContext.BuildSystem();
            if (b.IsRunningOnVSTS || b.IsRunningOnTFS)
            {
                var currentTask = _engine.Tasks.First(t => t.Name == e.TaskTeardownContext.Task.Name);
                var currentIndex = _engine.Tasks.ToList().IndexOf(currentTask);
                //b.TFBuild.UpdateProgress(_parentRecord, GetProgress(currentIndex, _engine.Tasks.Count));
                var g = TaskRecords[currentTask.Name];
                b.TFBuild.Commands.UpdateRecord(g,
                    new TFBuildRecordData
                    {
                        FinishTime = DateTime.Now,
                        Progress = 100,
                        Result = GetTaskResult(e.TaskTeardownContext)
                    });
            }
        }

        private TFBuildTaskResult? GetTaskResult(ITaskTeardownContext taskTeardownContext)
        {
            if (taskTeardownContext.Skipped) return TFBuildTaskResult.Skipped;

            // TODO: this logic should be improved but is difficult without task status in the context
            return TFBuildTaskResult.Succeeded;
        }

        private void OnTaskSetup(object sender, TaskSetupEventArgs e)
        {
            var b = e.TaskSetupContext.BuildSystem();
            if (b.IsRunningOnVSTS || b.IsRunningOnTFS)
            {
                var currentTask =
                    _engine.Tasks.First(t => t.Name == e.TaskSetupContext.Task.Name);
                var currentIndex = _engine.Tasks.ToList().IndexOf(currentTask);
                b.TFBuild.UpdateProgress(_parentRecord, GetProgress(currentIndex, _engine.Tasks.Count));
                var g = e.TaskSetupContext.TFBuild()
                    .Commands.CreateNewRecord(currentTask.Name, "build", currentIndex,
                        new TFBuildRecordData() {StartTime = DateTime.Now, ParentRecord = _parentRecord, Progress = 0});
                TaskRecords.Add(currentTask.Name, g);
            }
        }

        private int GetProgress(int currentTask, int count)
        {
            return currentTask/count*100;
        }

        private void BuildSetup(object sender, SetupEventArgs e)
        {
            var b = e.Context.BuildSystem();
            if (b.IsRunningOnTFS || b.IsRunningOnVSTS)
            {
                e.Context.TFBuild().Commands.SetProgress(0, "Build Setup");
                var g = e.Context.TFBuild()
                    .Commands.CreateNewRecord("Cake Build", "build", 0, new TFBuildRecordData {StartTime = DateTime.Now});
                _parentRecord = g;
            }
        }

        private readonly ICakeEngine _engine;
        private Guid _parentRecord;
        private Dictionary<string, Guid> TaskRecords { get; } = new Dictionary<string, Guid>();
    }
}