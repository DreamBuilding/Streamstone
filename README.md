<p align="center">
  <img src="https://github.com/yevhen/Streamstone/blob/master/Logo.Wide.png?raw=true" alt="Streamstone's logo"/>
</p>

Streamstone is a small library targeted at building scalable event-sourced applications on top of Azure Table Storage. It has simple, functional style API, heavily inspired by Greg Young's Event Store.

## Features

+ Fully ACID compliant
+ Optimistic concurrency support
+ Duplicate event detection (based on identity)
+ Custom stream and event properties you can query on
+ Synchronous projections and snapshots
+ Friendly for multi-tenant designs

## Installing from NuGet [![NuGet](https://img.shields.io/nuget/v/Streamstone.svg?style=flat)](https://www.nuget.org/packages/Streamstone/)

To install Streamstone via NuGet, run this command in NuGet package manager console:

    PM> Install-Package Streamstone

## Building from source [![Build status](https://ci.appveyor.com/api/projects/status/3rsmwblor11b6inq/branch/master?svg=true)](https://ci.appveyor.com/project/yevhen/streamstone/branch/master)

To build Streamstone's binaries, just clone the repository and build solution from Visual Studio, Alternatively, run the following command from solution's root directory:

    > Nake.bat

For a list of available commands run `Nake.bat -T`. 

## Design

Streamstone is just a thin layer (library, not a server) on top of Windows Azure Table Storage. It implements low-level mechanics for dealing with event streams, and all heavy-weight lifting is done by underlying provider. 

The api is stateless and all exposed objects are immutable, once fully constructed. Streamstone doesn't dictate payload serialization protocol, so you are free to choose any protocol you want.

Optimistic concurrency is implemented by making version part of RowKey identifier. Duplicate event detection is done by automatically creating additional entity for every event, with RowKey value set to a unique identifier of a source event (consistent secondary index).     

## Schema

<a href="https://raw.githubusercontent.com/yevhen/Streamstone/master/Doc/Schema.png" target="_blank" title="Click to view full size"><img src="https://raw.githubusercontent.com/yevhen/Streamstone/master/Doc/Schema.png" alt="Schema" style="max-width:100%;"/></a>

---

<a href="https://raw.githubusercontent.com/yevhen/Streamstone/master/Schema_VP.png" target="_blank" title="Click to view full size"><img src="https://raw.githubusercontent.com/yevhen/Streamstone/master/Doc/Schema_VP.png" alt="Schema for virtual partitions" style="max-width:100%;"/></a>

## Usage

##### Essentials
+ Provisioning stream [[see](/Source/Example/Scenarios/S01_Provision_new_stream.cs)]
+ Opening stream [[see](Source/Example/Scenarios/S02_Open_stream_for_writing.cs)]
+ Writing to stream [[see](Source/Example/Scenarios/S04_Write_to_stream.cs)]
+ Reading from stream [[see](Source/Example/Scenarios/S05_Read_from_stream.cs)]
+ Additional entity includes [[see](Source/Example/Scenarios/S06_Include_additional_entities.cs)]
+ Optimistic concurrency [[see](Source/Example/Scenarios/S08_Concurrency_conflicts.cs)]
+ Handling duplicate events [[see](Source/Example/Scenarios/S09_Handling_duplicates.cs)]
+ Custom stream metadata [[see](Source/Example/Scenarios/S07_Custom_stream_metadata.cs)]
+ Virtual partitions [[see](Source/Streamstone.Tests/Scenarios/Virtual_partitions.cs)]

##### Application
+ Using snapshots [[see](Source/Example/Scenarios/S06_Include_additional_entities.cs)]
+ Creating projections [[see]()]
+ Querying events [[see]()]

## Limitations

The write batch size limit, imposed by Azure Table Storage, is 100 entities, therefore:

+ The maximum write batch size is 99 entities (100 - 1 header entity) 
+ With duplicate event detection enabled, maximum write batch size is 49 events (100/2 - 1 header entity) 

Other limitations of the underlying Azure Table Storage API:

+ Maximum size of batch is 4MB
+ Maximum size of entity is 1 MB
+ Maximum size of property is 64Kb 
+ Maximum length of property name is 255 chars
+ An entity can have up to 255 custom properties

> [WATS limitations on MSDN](http://msdn.microsoft.com/en-us/library/azure/dd179338.aspx) 

## Community

[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/yevhen/Streamstone?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

## License

Apache 2 License
