﻿using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Streamstone
{
    public sealed class StreamProperties : PropertyMap
    {
        public static readonly StreamProperties None = new StreamProperties();

        StreamProperties()
        {}
        
        StreamProperties(IDictionary<string, Property> properties) 
            : base(properties)
        {}

        internal static StreamProperties ReadEntity(IDictionary<string, EntityProperty> properties)
        {
            Requires.NotNull(properties, "properties");
            return Build(properties);
        }

        public static StreamProperties From(ITableEntity entity)
        {
            Requires.NotNull(entity, "entity");
            return Build(entity.WriteEntity(new OperationContext()));
        }
        
        public static StreamProperties From(object obj)
        {
            Requires.NotNull(obj, "obj");
            return Build(TableEntity.WriteUserObject(obj, new OperationContext()));
        }

        public static StreamProperties From(IDictionary<string, EntityProperty> properties)
        {
            Requires.NotNull(properties, "properties");
            return Build(properties.Clone());
        }

        static StreamProperties Build(IEnumerable<KeyValuePair<string, EntityProperty>> properties)
        {
            return new StreamProperties(properties
                .Where(x => !IsReserved(x.Key))
                .ToDictionary(p => p.Key, p => new Property(p.Value))
            );
        }

        static bool IsReserved(string propertyName)
        {
            switch (propertyName)
            {
                case "PartitionKey":
                case "RowKey":
                case "ETag":
                case "Timestamp":
                case "Start":
                case "Count":
                case "Version":
                    return true;
                default:
                    return false;
            }
        }
    }
}