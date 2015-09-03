﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StyleCopStageProcess.cs" company="http://stylecop.codeplex.com">
//   MS-PL
// </copyright>
// <license>
//   This source code is subject to terms and conditions of the Microsoft 
//   Public License. A copy of the license can be found in the License.html 
//   file at the root of this distribution. If you cannot locate the  
//   Microsoft Public License, please send an email to dlr@microsoft.com. 
//   By using this source code in any fashion, you are agreeing to be bound 
//   by the terms of the Microsoft Public License. You must not remove this 
//   notice, or any other, from this software.
// </license>
// <summary>
//   Stage Process that execute the Microsoft StyleCop against the
//   specified file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace StyleCop.ReSharper.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using JetBrains.Application.Settings;
    using JetBrains.ReSharper.Feature.Services.Daemon;
    using JetBrains.ReSharper.Psi;
    using JetBrains.ReSharper.Psi.CSharp.Tree;

    using StyleCop.Diagnostics;
    using StyleCop.ReSharper.Options;

    /// <summary>
    /// Stage Process that execute the Microsoft StyleCop against the specified file.
    /// </summary>
    /// <remarks>
    /// This type is created and executed every time a .cs file is modified in the IDE.
    /// </remarks>
    public class StyleCopStageProcess : IDaemonStageProcess
    {
        /// <summary>
        /// Defines the max performance value - this is used to reverse the settings.
        /// </summary>
        private const int MaxPerformanceValue = 9;

        /// <summary>
        /// Used to reduce the number of calls to StyleCop to help with performance.
        /// </summary>
        private static Stopwatch performanceStopWatch;

        /// <summary>
        /// Gets set to true after our first run.
        /// </summary>
        private static bool runOnce;

        private readonly StyleCopRunnerInt runner;

        /// <summary>
        /// The process we were started with.
        /// </summary>
        private readonly IDaemonProcess daemonProcess;

        private readonly ICSharpFile file;

        /// <summary>
        /// THe settings store we were constructed with.
        /// </summary>
        private readonly IContextBoundSettingsStore settingsStore;

        /// <summary>
        /// Initializes a new instance of the StyleCopStageProcess class, using the specified <see cref="IDaemonProcess"/> .
        /// </summary>
        /// <param name="runner">
        /// A reference to the StyleCop runner.
        /// </param>
        /// <param name="daemonProcess">
        /// <see cref="IDaemonProcess"/> to execute within. 
        /// </param>
        /// <param name="settingsStore">
        /// Our settings. 
        /// </param>
        /// <param name="file">
        /// The file to analyze.
        /// </param>
        public StyleCopStageProcess(StyleCopRunnerInt runner, IDaemonProcess daemonProcess, IContextBoundSettingsStore settingsStore, ICSharpFile file)
        {
            StyleCopTrace.In(daemonProcess, settingsStore, file);

            this.runner = runner;
            this.daemonProcess = daemonProcess;
            this.settingsStore = settingsStore;
            this.file = file;
            InitialiseTimers();

            StyleCopTrace.Out();
        }

        /// <summary>
        /// Gets the Daemon Process.
        /// </summary>
        public IDaemonProcess DaemonProcess
        {
            get
            {
                return this.daemonProcess;
            }
        }

        /// <summary>
        /// The execute.
        /// </summary>
        /// <param name="committer">
        /// The committer. 
        /// </param>
        public void Execute(Action<DaemonStageResult> committer)
        {
            StyleCopTrace.In();
            try
            {
                if (this.daemonProcess == null)
                {
                    return;
                }

                if (this.daemonProcess.InterruptFlag)
                {
                    return;
                }

                // inverse the performance value - to ensure that "more resources" actually evaluates to a lower number
                // whereas "less resources" actually evaluates to a higher number. If Performance is set to max, then execute as normal.
                int parsingPerformance = this.settingsStore.GetValue((StyleCopOptionsSettingsKey key) => key.ParsingPerformance);

                bool alwaysExecute = parsingPerformance == StyleCopStageProcess.MaxPerformanceValue;

                bool enoughTimeGoneByToExecuteNow = false;

                if (!alwaysExecute)
                {
                    enoughTimeGoneByToExecuteNow = performanceStopWatch.Elapsed > new TimeSpan(0, 0, 0, StyleCopStageProcess.MaxPerformanceValue - parsingPerformance);
                }

                if (!alwaysExecute && !enoughTimeGoneByToExecuteNow && runOnce)
                {
                    StyleCopTrace.Info("Not enough time gone by to execute.");
                    StyleCopTrace.Out();
                    return;
                }

                runOnce = true;

                this.runner.Execute(this.daemonProcess.SourceFile.ToProjectFile(), this.daemonProcess.Document, this.file);

                List<HighlightingInfo> violations =
                    (from info in this.runner.ViolationHighlights
                     let range = info.Range
                     let highlighting = info.Highlighting
                     select new HighlightingInfo(range, highlighting)).ToList();

                committer(new DaemonStageResult(violations));

                ResetPerformanceStopWatch();
            }
            catch (JetBrains.Application.Progress.ProcessCancelledException)
            {
            }

            StyleCopTrace.Out();
        }

        /// <summary>
        /// Initializes the static timers used to regulate performance of execution of StyleCop analysis.
        /// </summary>
        private static void InitialiseTimers()
        {
            if (performanceStopWatch == null)
            {
                performanceStopWatch = Stopwatch.StartNew();
                performanceStopWatch.Start();
            }
        }

        /// <summary>
        /// Resets the Performance Stopwatch.
        /// </summary>
        private static void ResetPerformanceStopWatch()
        {
            performanceStopWatch.Reset();
            performanceStopWatch.Start();
        }
    }
}