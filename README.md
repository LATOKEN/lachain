[![Build Status](https://travis-ci.org/neo-project/neo-cli.svg?branch=master)](https://travis-ci.org/neo-project/neo-cli)

## Prerequisites

You will need Window or Linux. Use a virtual machine if you have a Mac. Ubuntu 14 and 16 are supported. Ubuntu 17 is not supported.

Install [.NET Core](https://www.microsoft.com/net/download/core).

On Linux, install the LevelDB and SQLite3 dev packages. E.g. on Ubuntu:

```sh
sudo apt-get install libleveldb-dev sqlite3 libsqlite3-dev libunwind8-dev

```

On Windows, use the [Neo version of LevelDB](https://github.com/neo-project/leveldb).

## Download pre-compiled binaries

See also [official docs](http://docs.neo.org/en-us/node/introduction.html). Download and unzip [latest release](https://github.com/neo-project/neo-cli/releases).

```sh
dotnet neo-cli.dll
```

## Compile from source

Clone the neo-cli repository.

```sh
cd neo-cli
dotnet restore
dotnet publish -c Release
```
In order to run, you need .NET Core. Download the SDK [binary](https://www.microsoft.com/net/download/linux).

Assuming you extracted .NET in the parent folder:

```sh
../dotnet bin/Release/netcoreapp1.0/neo-cli.dll .
```

## Logging

To enable logs in neo-cli, you need to add the ApplicationLogs plugin. Please check [here](https://github.com/neo-project/neo-plugins) for more information.


## Bootstrapping the network.
In order to synchronize the network faster, please check [here](http://docs.neo.org/en-us/network/syncblocks.html).


## Usage

See [documentation](http://docs.neo.org/en-us/node/cli.html). E.g. try `show state` or `create wallet wallet.json`.
