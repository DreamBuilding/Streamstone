## Streamstone

Streamstone is a small library focused around building scalable event-sourced solutions on top of Windows Azure Table Storage. The API is specifically tailored for ease of consumption from within DDD/CQRS style applications.

## Main features

+ All stream operations are fully consistent
+ Duplicate event detection (based on unique id of an event)
+ Supports custom event and stream metadata
+ Batching support for writes
+ Allows to include custom table operations within writes 
+ Serialization and identity is a client prerogative
+ Easy-to-use, functional style immutable API

## Design

TODO

## Usage

TODO

## Limitations

Same as for underlying Windows Azure Table Storage:
- Maximum size of batch is 4MB
- Maximum size of event is 1 MB
- Maximum size of payload and metadata properties is 64Kb 
- Maximum length of property name is 255 chars
- An event can have up to 255 custom properties

> [WATS limitations on MSDN](http://msdn.microsoft.com/en-us/library/azure/dd179338.aspx)
> [Entity size calculation](http://blogs.msdn.com/b/avkashchauhan/archive/2011/11/30/how-the-size-of-an-entity-is-caclulated-in-windows-azure-table-storage.aspx)
> [SO Answer on Entity size limit](http://stackoverflow.com/a/8967266/1188209)

## License

Apache 2 License