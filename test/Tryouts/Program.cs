﻿using System;
using System.Diagnostics;
using System.IO;
using FastTests.Client.Attachments;
using FastTests.Smuggler;
using System.Threading.Tasks;
using FastTests.Client.Subscriptions;
using FastTests.Issues;
using FastTests.Server.Documents;
using FastTests.Server.Documents.Indexing;
using FastTests.Server.Documents.PeriodicExport;
using FastTests.Server.OAuth;
using FastTests.Server.Replication;
using SlowTests.Issues;
using Sparrow;
using Sparrow.Logging;

namespace Tryouts
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(Process.GetCurrentProcess().Id);
            Console.WriteLine();

            LoggingSource.Instance.SetupLogMode(LogMode.Information, "logs");

            for (int i = 0; i < 1000; i++)
            {
                Console.WriteLine(i);
                using (var a = new DocumentsCrud())
                {
                    a.CanDelete("USERs/1");
                }
            }
        }
    }

    
}