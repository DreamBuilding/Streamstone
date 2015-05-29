﻿using System;
using System.Linq;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Example
{
    using Scenarios;

    public static class Program
    {
        public static void Main()
        {
            var table = Prepare();

            var scenarios = new Scenario[]
            {
                new S01_Provision_new_stream(),
                new S02_Open_stream_for_writing(),
                new S03_Try_open_stream_for_writing(),
                new S04_Write_to_stream(),
                new S05_Read_from_stream(),
                new S06_Include_additional_entities(),
                new S07_Custom_stream_metadata(),
                new S08_Concurrency_conflicts(),
                new S09_Handling_duplicates(),
            };

            for (int i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];

                Console.WriteLine("{0}", scenario.GetType().Name.Replace("_", " "));
                Console.WriteLine(new string('-', 40));

                scenario.Initialize(table, i.ToString());
                scenario.Run();

                Console.WriteLine();
            }
            Console.WriteLine("You can check out the contents of Example table using Server Explorer in VS");
            Console.WriteLine("Press any key to exit ...");

            Console.ReadKey(true);
        }

        static CloudTable Prepare()
        {
            var table = CloudStorageAccount
                .DevelopmentStorageAccount
                .CreateCloudTableClient()
                .GetTableReference("Example");

            table.DeleteIfExists();
            table.CreateIfNotExists();

            return table;
        }
    }
}
