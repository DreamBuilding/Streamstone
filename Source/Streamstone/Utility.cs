﻿// ReSharper disable StringCompareToIsCultureSpecific

using System;
using System.Linq;

using Microsoft.WindowsAzure.Storage.Table;

namespace Streamstone
{
    namespace Utility
    {
        public static class TableQueryExtensions
        {
            public static IQueryable<TEntity> RowKeyPrefixQuery<TEntity>(this Partition partition, string prefix) where TEntity : ITableEntity, new()
            {
                var table = partition.Table;
                return table.CreateQuery<TEntity>()
                            .Where(x => x.PartitionKey == partition.PartitionKey)
                            .WhereRowKeyPrefix(prefix);
            }

            public static IQueryable<TEntity> WhereRowKeyPrefix<TEntity>(this IQueryable<TEntity> queryable, string prefix) where TEntity : ITableEntity, new()
            {
                var range = new PrefixRange(prefix);

                return queryable.Where(x => 
                            x.RowKey.CompareTo(range.Start) >= 0
                            && x.RowKey.CompareTo(range.End) < 0);
            }
        }

        public struct PrefixRange
        {
            public readonly string Start;
            public readonly string End;

            public PrefixRange(string prefix)
            {
                Start = prefix;

                var length = prefix.Length - 1;
                var lastChar = prefix[length];
                var nextLastChar = (char)(lastChar + 1);

                End = prefix.Substring(0, length) + nextLastChar;
            }
        }
    }
}