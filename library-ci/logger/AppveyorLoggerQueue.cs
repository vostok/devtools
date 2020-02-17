using System.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.Extensions.Appveyor.TestLogger
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class AppveyorLoggerQueue
    {

        private readonly AsyncProducerConsumerCollection<Dictionary<string, string>> queue = new AsyncProducerConsumerCollection<Dictionary<string, string>>();
        private readonly Task consumeTask;
        private readonly CancellationTokenSource consumeTaskCancellationSource = new CancellationTokenSource();

        private int totalEnqueued;
        private readonly int totalSent = 0;

        public AppveyorLoggerQueue()
        {
            consumeTask = ConsumeItemsAsync(consumeTaskCancellationSource.Token);
        }

        public void Enqueue(Dictionary<string, string> json)
        {
            queue.Add(json);
            totalEnqueued++;
        }

        public void Flush()
        {
            // Cancel any idle consumers and let them return
            queue.Cancel();

            try
            {
                // any active consumer will circle back around and batch post the remaining queue.
                consumeTask.Wait(TimeSpan.FromSeconds(60));

                // Cancel any active HTTP requests if still hasn't finished flushing
                consumeTaskCancellationSource.Cancel();
                if (!consumeTask.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("cancellation didn't happen quickly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine("Appveyor.TestLogger: {0} test results reported ({1} enqueued).", totalSent, totalEnqueued);
        }

        private async Task ConsumeItemsAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var nextItems = await queue.TakeAsync();
                if (nextItems == null || nextItems.Length == 0) return; // Queue is cancelling and and empty.

                foreach (var nextItem in nextItems)
                {
                    await PostItemAsync(nextItem);
                }

                if (cancellationToken.IsCancellationRequested) return;
            }
        }

        private async Task PostItemAsync(Dictionary<string, string> dict)
        {
            var name = dict["name"];
            dict.Remove("name");

            var args = new StringBuilder($"AddTest {name}");
            foreach (var kvp in dict)
            {
                args.Append($" -{kvp.Key} ");
                var escaped = kvp.Value.Replace("\"", "\\\"");
                args.Append($"\"{escaped}\"");
            }

            try
            {
                Process.Start("appveyor", args.ToString())?.WaitForExit();
            }
            catch
            {

            }
        }
    }
}