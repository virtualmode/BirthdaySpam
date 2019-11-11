# BirthdaySpam
Birthday fee service

Installation
============

Build package
-------------

To build .NET Core application:

    cd $(ProjectDir)
    dotnet publish -c Release

Optionally provide execute permissions:

    chmod 777 ./appname

To run console application write:

    dotnet BirthdaySpam.dll

Docker command line
-------------------

Don't forget this:

	docker run -p 9090:8080 -v $(pwd)/BirthdaySpam:/opt/BirthdaySpam -w="/opt/BirthdaySpam" -it mcr.microsoft.com/dotnet/core/sdk:3.0.100-alpine3.10 dotnet /opt/BirthdaySpam/BirthdaySpam.dll

