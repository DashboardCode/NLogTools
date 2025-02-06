using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using NLog;
using SmtpServer;
using SmtpServer.ComponentModel;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.IO;
using System.Buffers;
using System.Collections.Concurrent;

namespace DashboardCode.NLogTools.Test
{

    // TODO: do it more realistic N users authenticate and trace activities, verbose episodically, 
    // when 1 user verbose constantly.
    [TestClass]
    public class NLogMailUnitTest
    {
        const int usersCount = 2;
        const int operations = 3;
        const int puaseBetweenOperationsMs = 20;
        const int repeats = 3;

        public NLogMailUnitTest()
        {

        }
        class SampleMessageStore : MessageStore
        {
            public readonly ConcurrentBag<MimeKit.MimeMessage> ReceivedMessages;
            public SampleMessageStore(ConcurrentBag<MimeKit.MimeMessage> receivedMessages)
            {
                ReceivedMessages = receivedMessages;
            }

            public override async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
            {
                //await using var stream = new MemoryStream();

                //var position = buffer.GetPosition(0);
                //while (buffer.TryGet(ref position, out var memory))
                //{
                //    await stream.WriteAsync(memory, cancellationToken);
                //}

                using var stream = new MemoryStream(); // 🔄 Replace 'await using' with 'using'

                var position = buffer.GetPosition(0);
                while (buffer.TryGet(ref position, out var memory))
                {
                    byte[] bytes = memory.ToArray();
                    await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                }

                stream.Position = 0;

                var message = await MimeKit.MimeMessage.LoadAsync(stream, cancellationToken);
                ReceivedMessages.Add(message);
                return SmtpResponse.Ok;
            }
        }
        static async Task StartSmtpServer(ConcurrentBag<MimeKit.MimeMessage> receivedMessages)
        {
            var options = new SmtpServerOptionsBuilder()
                .ServerName("localhost")
                .Port(25)
                .Build();

            var serviceProvider = new ServiceProvider();
            serviceProvider.Add(new SampleMessageStore(receivedMessages));

            var smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);

            await smtpServer.StartAsync(CancellationToken.None);
        }

        [TestMethod]
        public void NlogSimpleLog()
        {
            var receivedMessages = new ConcurrentBag<MimeKit.MimeMessage>();
            Task.Run(() => StartSmtpServer(receivedMessages));

            var logEventInfo = new LogEventInfo()
            {
                Level = LogLevel.Info,
                Message = "Test"
            };

            var loggerSimple = NLog.LogManager.GetLogger("SimpleMail");
            loggerSimple.Log(logEventInfo);
        }

        [TestMethod]
        public void NlogMailTestWithCounter()
        {
            var receivedMessages = new ConcurrentBag<MimeKit.MimeMessage>();
            Task.Run(() => StartSmtpServer(receivedMessages));

            var logEventInfo = new LogEventInfo()
            {
                Level = LogLevel.Info,
                Message = "Test"
            };

            var loggerSimple = NLog.LogManager.GetLogger("MailTest:TEST");
            loggerSimple.Log(logEventInfo);
            loggerSimple.Log(logEventInfo);
            loggerSimple.Log(logEventInfo);
            loggerSimple.Log(logEventInfo);
            loggerSimple.Log(logEventInfo);
        }

        [TestMethod]
        public void NLogMail()
        {
            var receivedMessages = new ConcurrentBag<MimeKit.MimeMessage>();
            Task.Run(() => StartSmtpServer(receivedMessages));

            var verboseLogger = NLog.LogManager.GetLogger("MailTest:" + nameof(NLogMailUnitTest));
            var c = 0;
            var verboseMessages = LogTestManager.Generate(20);
            var at = DateTime.Now;
            foreach (var verbose in verboseMessages)
            {
                var logEventInfo = new LogEventInfo()
                {
                    Level = LogLevel.Debug,
                    TimeStamp = verbose.DateTime,
                    Message = verbose.Message,
                };
                logEventInfo.Properties["LoggedAt"] = verbose.DateTime;
                logEventInfo.Properties["Number"] = c++;
                verboseLogger.Log(logEventInfo);
            }

            // https://github.com/cmendible/netDumbster
            // TODO: check email in the storage

            if (receivedMessages.Count != 3)
                throw new ApplicationException("limited to 3 emails");
        }

        [TestMethod]
        public void NLogMailAssync()
        {
            var receivedMessages = new ConcurrentBag<MimeKit.MimeMessage>();
            Task.Run(() => StartSmtpServer(receivedMessages));

            var verboseLogger = NLog.LogManager.GetLogger("MailTest:" + nameof(NLogMailAssync));
            var c = 0;
            for (var r = 0; r <= repeats; r++)
            {
                var tasks = new List<Task>();
                for (var u = 0; u <= usersCount; u++)
                {
                    var task = Task.Run(
                        () =>
                        {
                            for (var o = 0; o <= operations; o++)
                            {
                                var verboseMessages = LogTestManager.Generate();
                                var count = verboseMessages.Count();
                                //var i = 0;
                                var at = DateTime.Now;
                                foreach (var verbose in verboseMessages.Take(20))
                                {
                                    var number = Interlocked.Increment(ref c);
                                    var logEventInfo = new LogEventInfo()
                                    {
                                        Level = LogLevel.Debug,
                                        TimeStamp = verbose.DateTime,
                                        Message = verbose.Message,
                                    };
                                    logEventInfo.Properties["LoggedAt"] = verbose.DateTime;
                                    logEventInfo.Properties["Number"] = number;
                                    verboseLogger.Log(logEventInfo);

                                }
                                Thread.Sleep(puaseBetweenOperationsMs);
                            }
                        }
                        );
                    tasks.Add(task);
                }
                Task.WaitAll(tasks.ToArray());
            }
        }
    }
}