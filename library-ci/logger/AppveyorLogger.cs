namespace Microsoft.VisualStudio.TestPlatform.Extensions.Appveyor.TestLogger
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    [FriendlyName(AppveyorLogger.FriendlyName)]
    [ExtensionUri(AppveyorLogger.ExtensionUri)]
    public class AppveyorLogger : ITestLogger
    {
        /// <summary>
        /// Uri used to uniquely identify the Appveyor logger.
        /// </summary>
        public const string ExtensionUri = "logger://Microsoft/TestPlatform/AppveyorLogger/v1";

        /// <summary>
        /// Alternate user friendly string to uniquely identify the Appveyor logger.
        /// </summary>
        public const string FriendlyName = "Appveyor";

        private AppveyorLoggerQueue queue;

        /// <summary>
        /// Initializes the Test Logger.
        /// </summary>
        /// <param name="events">Events that can be registered for.</param>
        /// <param name="testRunDirectory">Test Run Directory</param>
        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            NotNull(events, nameof(events));

            queue = new AppveyorLoggerQueue();

            // Register for the events.
            events.TestRunMessage += this.TestMessageHandler;
            events.TestResult += this.TestResultHandler;
            events.TestRunComplete += this.TestRunCompleteHandler;
        }

        /// <summary>
        /// Called when a test message is received.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// Event args
        /// </param>
        private void TestMessageHandler(object sender, TestRunMessageEventArgs e)
        {
            NotNull(sender, nameof(sender));
            NotNull(e, nameof(e));

            // Add code to handle message
        }

        /// <summary>
        /// Called when a test result is received.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The eventArgs.
        /// </param>
        private void TestResultHandler(object sender, TestResultEventArgs e)
        {
            var name = e.Result.TestCase.FullyQualifiedName;
            var filename = string.IsNullOrEmpty(e.Result.TestCase.Source) ? string.Empty : Path.GetFileName(e.Result.TestCase.Source);
            var outcome = e.Result.Outcome.ToString();

            var testResult = new Dictionary<string, string>();
            testResult.Add("name", name);
            testResult.Add("Framework", e.Result.TestCase.ExecutorUri.ToString());
            testResult.Add("Outcome", outcome);

            if (!string.IsNullOrEmpty(filename))
            {
                testResult.Add("FileName", filename);
            }

            if (e.Result.Outcome == TestOutcome.Passed || e.Result.Outcome == TestOutcome.Failed)
            {
                var duration = Convert.ToInt32(e.Result.Duration.TotalMilliseconds);

                var errorMessage = e.Result.ErrorMessage;
                var errorStackTrace = e.Result.ErrorStackTrace;

                var stdErr = new StringBuilder();
                var stdOut = new StringBuilder();

                foreach (var m in e.Result.Messages)
                {
                    if (TestResultMessage.StandardOutCategory.Equals(m.Category, StringComparison.OrdinalIgnoreCase))
                    {
                        stdOut.AppendLine(m.Text);
                    }
                    else if (TestResultMessage.StandardErrorCategory.Equals(m.Category, StringComparison.OrdinalIgnoreCase))
                    {
                        stdErr.AppendLine(m.Text);
                    }
                }

                testResult.Add("Duration", duration.ToString(CultureInfo.InvariantCulture));

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    testResult.Add("ErrorMessage", errorMessage);
                }
                if (!string.IsNullOrEmpty(errorStackTrace))
                {
                    testResult.Add("ErrorStackTrace", errorStackTrace);
                }
                if (!string.IsNullOrEmpty(stdOut.ToString()))
                {
                    testResult.Add("StdOut", stdOut.ToString());
                }
                if (!string.IsNullOrEmpty(stdErr.ToString()))
                {
                    testResult.Add("StdErr", stdErr.ToString());
                }
            }

            queue.Enqueue(testResult);
        }


        /// <summary>
        /// Called when a test run is completed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// Test run complete events arguments.
        /// </param>
        private void TestRunCompleteHandler(object sender, TestRunCompleteEventArgs e)
        {
            queue.Flush();
        }

        private static T NotNull<T>(T arg, string parameterName)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return arg;
        }
    }
}