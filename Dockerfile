FROM mcr.microsoft.com/dotnet/core/sdk:3.0.100-alpine3.10

WORKDIR /opt

ADD BirthdaySpam /opt/BirthdaySpam

RUN chmod 777 -R /opt/BirthdaySpam

WORKDIR /opt/BirthdaySpam

EXPOSE 8080

ENTRYPOINT ["dotnet", "BirthdaySpam.dll"]

