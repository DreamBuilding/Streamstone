﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Streamstone
{
    public sealed partial class Stream
    {
        class ProvisionOperation
        {
            readonly CloudTable table;
            readonly Stream stream;

            public ProvisionOperation(CloudTable table, Stream stream)
            {
                Requires.NotNull(table, "table");
                Requires.NotNull(stream, "stream");

                if (stream.IsStored)
                    throw new ArgumentException("Can't provision already stored stream", "stream");

                this.table  = table;
                this.stream = stream;
            }

            public Stream Execute()
            {
                var insert = new Insert(stream);

                try
                {
                    table.Execute(insert.Prepare());
                }
                catch (StorageException e)
                {
                    insert.Handle(table, e);
                }

                return insert.Result();
            }

            public async Task<Stream> ExecuteAsync()
            {
                var insert = new Insert(stream);

                try
                {
                    await table.ExecuteAsync(insert.Prepare()).Really();
                }
                catch (StorageException e)
                {
                    insert.Handle(table, e);
                }

                return insert.Result();
            }

            class Insert
            {
                readonly StreamEntity entity;

                public Insert(Stream stream)
                {
                    entity = stream.Entity();
                }

                public TableOperation Prepare()
                {
                    return TableOperation.Insert(entity);
                }

                internal void Handle(CloudTable table, StorageException e)
                {
                    if (e.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
                        throw ConcurrencyConflictException.StreamChangedOrExists(table, entity.PartitionKey);

                    throw e.PreserveStackTrace();
                }

                internal Stream Result()
                {
                    return From(entity);
                }
            }
        }

        class WriteOperation
        {
            readonly CloudTable table;
            readonly string partition;
            readonly Batch batch;
            
            public WriteOperation(CloudTable table, Stream stream, Event[] events, Include[] includes)
            {
                Requires.NotNull(table, "table");
                Requires.NotNull(stream, "stream");
                Requires.NotNull(events, "events");
                Requires.NotNull(events, "includes");

                if (events.Length == 0)
                    throw new ArgumentOutOfRangeException("events", "Events have 0 items");

                const int maxBatchSize = 100;
                const int entitiesPerEvent = 2;
                const int streamEntityPerBatch = 1;
                const int maxEntitiesPerBatch = (maxBatchSize / entitiesPerEvent) - streamEntityPerBatch;

                if (events.Length + includes.Length > maxEntitiesPerBatch)
                    throw new ArgumentOutOfRangeException("events",
                        "Maximum number of events per batch is " + maxEntitiesPerBatch);

                this.table = table;
                this.partition = stream.Partition;
                this.batch = Batch.Create(stream, events, includes);
            }

            public StreamWriteResult Execute()
            {
                batch.Prepare();

                try
                {
                    table.ExecuteBatch(batch);
                }
                catch (StorageException e)
                {
                    Handle(e);
                }

                return batch.Result();
            }

            public async Task<StreamWriteResult> ExecuteAsync()
            {
                batch.Prepare();

                try
                {
                    await table.ExecuteBatchAsync(batch).Really();
                }
                catch (StorageException e)
                {
                    Handle(e);
                }

                return batch.Result();        
            }

            class Batch
            {
                readonly TableBatchOperation batch = new TableBatchOperation();
                internal readonly List<ITableEntity> Items = new List<ITableEntity>();

                readonly Stream stream;
                internal readonly RecordedEvent[] Events;
                internal readonly Include[] Includes;

                Batch(Stream stream, RecordedEvent[] events, Include[] includes)
                {
                    this.stream = stream;
                    Events = events;
                    Includes = includes;
                }

                static internal Batch Create(Stream stream, ICollection<Event> events, Include[] includes)
                {
                    var start = stream.Start == 0
                        ? (events.Count != 0 ? 1 : 0)
                        : stream.Start;

                    var count = stream.Count + events.Count;
                    var version = stream.Version + events.Count;

                    var transient = events
                        .Select((e, i) => e.Record(stream.Version + i + 1))
                        .ToArray();

                    return new Batch(new Stream(stream.Partition, stream.properties, stream.ETag, start, count, version), transient, includes);
                }

                internal void Prepare()
                {
                    WriteStream();
                    WriteEvents();
                    WriteIncludes();
                }

                void WriteStream()
                {
                    var streamEntity = stream.Entity();

                    if (streamEntity.ETag == null)
                        batch.Insert(streamEntity);
                    else
                        batch.Replace(streamEntity);

                    Items.Add(streamEntity);
                }

                void WriteEvents()
                {
                    foreach (var e in Events)
                    {
                        var eventEntity = e.EventEntity(stream.Partition);
                        var eventIdEntity = e.IdEntity(stream.Partition);

                        batch.Insert(eventEntity);
                        batch.Insert(eventIdEntity);

                        Items.Add(eventEntity);
                        Items.Add(eventIdEntity);
                    }
                }

                void WriteIncludes()
                {
                    foreach (var include in Includes)
                    {
                        batch.Add(include.Apply(stream.Partition));
                        Items.Add(include.Entity);
                    }
                }

                public StreamWriteResult Result()
                {
                    var storedStream = From((StreamEntity)Items.First());

                    return new StreamWriteResult(storedStream, Events);
                }

                public static implicit operator TableBatchOperation(Batch arg)
                {
                    return arg.batch;
                }
            }
            
            void Handle(StorageException exception)
            {
                if (exception.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                    throw ConcurrencyConflictException.StreamChangedOrExists(table, partition);

                if (exception.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict)
                    throw exception.PreserveStackTrace();

                var error = exception.RequestInformation.ExtendedErrorInformation;
                if (error.ErrorCode != "EntityAlreadyExists")
                    throw UnexpectedStorageResponseException.ErrorCodeShouldBeEntityAlreadyExists(error);

                var position = ParseConflictingEntityPosition(error);
                Debug.Assert(position >= 0 && position < batch.Items.Count);

                var conflicting = batch.Items[position];
                if (conflicting is EventIdEntity)
                {
                    var duplicate = batch.Events[(position - 1) / 2];
                    throw new DuplicateEventException(table, partition, duplicate.Id);
                }
                
                if (conflicting is EventEntity)
                    throw ConcurrencyConflictException.EventVersionExists(
                        table, partition, new EventVersion(conflicting.RowKey));

                var include = Array.Find(batch.Includes, x => x.Entity == conflicting);
                if (include != null)
                    throw new IncludedOperationConflictException(table, partition, include);

                throw new WarningException("How did this happen? We've got conflict on entity which is neither event nor id or include");
            }

            static int ParseConflictingEntityPosition(StorageExtendedErrorInformation error)
            {
                var lines = error.ErrorMessage.Split('\n');
                if (lines.Length != 3)
                    throw UnexpectedStorageResponseException.ConflictExceptionMessageShouldHaveExactlyThreeLines(error);

                var semicolonIndex = lines[0].IndexOf(":", StringComparison.Ordinal);
                if (semicolonIndex == -1)
                    throw UnexpectedStorageResponseException.ConflictExceptionMessageShouldHaveSemicolonOnFirstLine(error);

                int position;
                if (!int.TryParse(lines[0].Substring(0, semicolonIndex), out position))
                    throw UnexpectedStorageResponseException.UnableToParseTextBeforeSemicolonToInteger(error);

                return position;
            }
        }

        class SetPropertiesOperation
        {
            readonly CloudTable table;
            readonly Stream stream;
            readonly StreamProperties properties;

            public SetPropertiesOperation(CloudTable table, Stream stream, StreamProperties properties)
            {
                Requires.NotNull(table, "table");
                Requires.NotNull(stream, "stream");
                Requires.NotNull(properties, "properties");

                if (stream.IsTransient)
                    throw new ArgumentException("Can't set properties on transient stream", "stream");

                this.table = table;
                this.stream = stream;
                this.properties = properties;                
            }

            public Stream Execute()
            {
                var replace = new Replace(stream, properties);

                try
                {
                    table.Execute(replace.Prepare());
                }
                catch (StorageException e)
                {
                    replace.Handle(table, e);
                }

                return replace.Result();
            }

            public async Task<Stream> ExecuteAsync()
            {
                var replace = new Replace(stream, properties);

                try
                {
                    await table.ExecuteAsync(replace.Prepare()).Really();
                }
                catch (StorageException e)
                {
                    replace.Handle(table, e);
                }

                return replace.Result();
            }

            class Replace
            {
                readonly StreamEntity entity;

                public Replace(Stream stream, StreamProperties properties)
                {
                    entity = stream.Entity();
                    entity.Properties = properties;
                }

                internal TableOperation Prepare()
                {                    
                    return TableOperation.Replace(entity);
                }

                internal void Handle(CloudTable table, StorageException e)
                {
                    if (e.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                        throw ConcurrencyConflictException.StreamChanged(table, entity.PartitionKey);

                    throw e.PreserveStackTrace();
                }

                internal Stream Result()
                {
                    return From(entity);
                }
            }
        }

        class OpenStreamOperation
        {
            readonly CloudTable table;
            readonly string partition;

            public OpenStreamOperation(CloudTable table, string partition)
            {
                Requires.NotNull(table, "table");
                Requires.NotNullOrEmpty(partition, "partition");

                this.table = table;
                this.partition = partition;
            }

            public StreamOpenResult Execute()
            {
                return Result(table.Execute(Prepare()));
            }

            public async Task<StreamOpenResult> ExecuteAsync()
            {
                return Result(await table.ExecuteAsync(Prepare()));
            }

            TableOperation Prepare()
            {
                return TableOperation.Retrieve<StreamEntity>(partition, StreamEntity.FixedRowKey);
            }

            static StreamOpenResult Result(TableResult result)
            {
                var entity = result.Result;

                return entity != null
                           ? new StreamOpenResult(true, From(((StreamEntity)entity)))
                           : StreamOpenResult.NotFound;
            }
        }

        class ReadOperation<T> where T : class, new()
        {
            readonly CloudTable table;
            readonly string partition;
            readonly int startVersion;
            readonly int sliceSize;

            public ReadOperation(CloudTable table, string partition, int startVersion, int sliceSize)
            {
                Requires.NotNull(table, "table");
                Requires.NotNullOrEmpty(partition, "partition");
                Requires.GreaterThanOrEqualToOne(startVersion, "startVersion");
                Requires.GreaterThanOrEqualToOne(sliceSize, "sliceSize");

                this.table = table;
                this.partition = partition;
                this.startVersion = startVersion;
                this.sliceSize = sliceSize;
            }

            public StreamSlice<T> Execute()
            {
                return Result(ExecuteQuery(PrepareQuery()));
            }

            public async Task<StreamSlice<T>> ExecuteAsync()
            {
                return Result(await ExecuteQueryAsync(PrepareQuery()));
            }

            StreamSlice<T> Result(ICollection<DynamicTableEntity> entities)
            {
                var streamEntity = FindStreamEntity(entities);
                entities.Remove(streamEntity);

                var stream = BuildStream(streamEntity);
                var events = BuildEvents(entities);

                return new StreamSlice<T>(stream, events, startVersion, sliceSize);
            }

            TableQuery<DynamicTableEntity> PrepareQuery()
            {
                var rowKeyStart = new EventKey(startVersion);
                var rowKeyEnd = new EventKey(startVersion + sliceSize - 1);

                var query = table
                    .CreateQuery<DynamicTableEntity>()
                    .Where(x =>
                           x.PartitionKey == partition
                           && (x.RowKey == StreamEntity.FixedRowKey
                               || (x.RowKey.CompareTo(rowKeyStart)  >= 0
                                   && x.RowKey.CompareTo(rowKeyEnd) <= 0)));

                return (TableQuery<DynamicTableEntity>) query;
            }

            List<DynamicTableEntity> ExecuteQuery(TableQuery<DynamicTableEntity> query)
            {
                var result = new List<DynamicTableEntity>();
                TableContinuationToken token = null;

                do
                {
                    var segment = table.ExecuteQuerySegmented(query, token);
                    token = segment.ContinuationToken;
                    result.AddRange(segment.Results);
                }
                while (token != null);

                return result;
            }

            async Task<List<DynamicTableEntity>> ExecuteQueryAsync(TableQuery<DynamicTableEntity> query)
            {
                var result = new List<DynamicTableEntity>();
                TableContinuationToken token = null;

                do
                {
                    var segment = await table.ExecuteQuerySegmentedAsync(query, token).Really();
                    token = segment.ContinuationToken;
                    result.AddRange(segment.Results);
                }
                while (token != null);

                return result;
            }

            static DynamicTableEntity FindStreamEntity(IEnumerable<DynamicTableEntity> entities)
            {
                return entities.Single(x => x.RowKey == StreamEntity.FixedRowKey);
            }

            static Stream BuildStream(DynamicTableEntity entity)
            {
                return From(StreamEntity.From(entity));
            }

            static T[] BuildEvents(IEnumerable<DynamicTableEntity> entities)
            {
                return entities.Select(e => e.Properties).Select(properties =>
                {
                    var t = new T();
                    TableEntity.ReadUserObject(t, properties, new OperationContext());
                    return t;
                })
                .ToArray();
            }
        }
    }
}
