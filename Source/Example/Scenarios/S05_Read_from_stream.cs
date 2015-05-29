﻿using System;
using System.Linq;

using Streamstone;

namespace Example.Scenarios
{
    public class S05_Read_from_stream : Scenario
    {
        public override void Run()
        {
            Prepare();

            ReadSlice();
            ReadAll();
        }

        void Prepare()
        {
            var events = Enumerable
                .Range(1, 10)
                .Select(Event)
                .ToArray();

            Stream.Write(Table, new Stream(Partition), events);
        }

        void ReadSlice()
        {
            Console.WriteLine("Reading single slice from specified start version and using specified slice size");

            var slice = Stream.Read<EventEntity>(Table, Partition, startVersion: 2, sliceSize: 2);
            foreach (var @event in slice.Events)
                Console.WriteLine("{0}: {1}-{2}", @event.Version, @event.Type, @event.Data);

            Console.WriteLine();
        }

        void ReadAll()
        {
            Console.WriteLine("Reading all events in a stream");
            Console.WriteLine("If slice size is > than WATS limit, continuation token will be managed automatically");

            StreamSlice<EventEntity> slice;
            int nextSliceStart = 1;

            do
            {
                slice = Stream.Read<EventEntity>(Table, Partition, nextSliceStart, sliceSize: 1);

                foreach (var @event in slice.Events)
                    Console.WriteLine("{0}:{1} {2}-{3}", @event.Id, @event.Version, @event.Type, @event.Data);

                nextSliceStart = slice.NextEventNumber;
            }
            while (!slice.IsEndOfStream);
        }

        static Event Event(int id)
        {
            var data = new
            {
                Id = id,
                Type = "<type>",
                Data = "{some}"
            };

            return new Event(id.ToString(), data.Props());
        }

        class EventEntity
        {
            public string Id   { get; set; }
            public string Type { get; set; }
            public string Data { get; set; }
            public int Version { get; set; }
        }
    }
}
